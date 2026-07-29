using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Procurement;

public sealed class AwardPersistenceAndMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public AwardPersistenceAndMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qce-award-" + Guid.NewGuid().ToString("N") + ".db");
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    public void Dispose()
    {
        try { SqliteConnection.ClearAllPools(); if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void Schema_Is_47()
    {
        Assert.True(ProductVersionInfo.DatabaseSchemaVersion >= 47);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, SQLiteSchemaMigrator.GetCurrentUserVersion(conn));
    }

    [Theory]
    [InlineData("procurement_offers")]
    [InlineData("procurement_offer_items")]
    [InlineData("procurement_offer_conditions")]
    [InlineData("procurement_tech_evaluations")]
    [InlineData("procurement_commercial_evaluations")]
    [InlineData("procurement_bid_comparisons")]
    [InlineData("procurement_awards")]
    [InlineData("procurement_award_items")]
    public void Migration_Creates_Table(string table)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", table);
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public async Task Offer_Comparison_Award_Roundtrip()
    {
        var perm = new AllowAllProcurementPermissionChecker();
        var audit = new SqliteProcurementAuditRepository(_dbPath);
        var rfqs = new SqliteRequestForQuotationRepository(_dbPath);
        var offers = new SqliteSupplierOfferRepository(_dbPath);
        var tech = new SqliteTechnicalEvaluationRepository(_dbPath);
        var com = new SqliteCommercialEvaluationRepository(_dbPath);
        var cmp = new SqliteBidComparisonRepository(_dbPath);
        var awards = new SqliteAwardRepository(_dbPath);
        var rfqId = RequestForQuotationId.New();
        var rfq = new RequestForQuotation
        {
            Id = rfqId, Number = "RFQ-T", ProjectPath = "p", DeadlineUtc = DateTime.UtcNow.AddDays(3), ComparisonCurrency = "CLP",
            Items = { new RfqItem { ItemId = "i1", RfqId = rfqId, Code = "C", Description = "D", Quantity = 3, Unit = "UN" } }
        };
        await rfqs.SaveAsync(rfq);
        var offerSvc = new SupplierOfferService(offers, tech, com, rfqs, audit, perm);
        var offer = await offerSvc.RegisterAsync(new SupplierOffer
        {
            RfqId = rfqId, SupplierId = SupplierId.New(), CurrencyCode = "CLP",
            Lines = { new OfferLine { LineId = "l1", OfferId = SupplierOfferId.New(), RfqItemId = "i1", Code = "C", Description = "D", Quantity = 3, UnitPriceOriginal = 12 } }
        }, "u");
        await offerSvc.SubmitAsync(offer.Id, offer.RowVersion, "u");
        var comparison = await new BidComparisonService(cmp, rfqs, offers, tech, com, audit, perm)
            .CompareAsync(rfqId, "CLP", 1m, DateTime.Today, "u");
        var award = await new AwardService(awards, cmp, offers, audit, perm)
            .CreateFromRecommendationAsync(comparison.Id, AwardKind.Total, "ok", "u");
        Assert.NotNull(await offers.GetByIdAsync(offer.Id));
        Assert.NotNull(await cmp.GetByIdAsync(comparison.Id));
        Assert.NotNull(await awards.GetByIdAsync(award.Id));
        Assert.Single(await awards.GetByRfqAsync(rfqId));
    }
}

public sealed class AwardArchitectureAndPermissionCoverageTests
{
    [Theory]
    [InlineData(ProcurementPermissions.QuotationView)]
    [InlineData(ProcurementPermissions.QuotationRegister)]
    [InlineData(ProcurementPermissions.QuotationEvaluate)]
    [InlineData(ProcurementPermissions.CommercialComparisonView)]
    [InlineData(ProcurementPermissions.TechnicalEvaluationManage)]
    [InlineData(ProcurementPermissions.AwardCreate)]
    [InlineData(ProcurementPermissions.AwardApprove)]
    [InlineData(ProcurementPermissions.AwardReject)]
    [InlineData(ProcurementPermissions.AwardAuditView)]
    public void Permission_Defined(string p) => Assert.StartsWith("Procurement.", p);

    [Theory]
    [InlineData(typeof(ISupplierOfferRepository))]
    [InlineData(typeof(ITechnicalEvaluationRepository))]
    [InlineData(typeof(ICommercialEvaluationRepository))]
    [InlineData(typeof(IBidComparisonRepository))]
    [InlineData(typeof(IAwardRepository))]
    [InlineData(typeof(ISupplierOfferService))]
    [InlineData(typeof(IBidComparisonService))]
    [InlineData(typeof(IAwardService))]
    public void Contracts_Exist(Type t) => Assert.True(t.IsInterface);

    [Fact]
    public void Infrastructure_Implements_Offer_Repos() =>
        Assert.True(typeof(ISupplierOfferRepository).IsAssignableFrom(typeof(SqliteSupplierOfferRepository)));

    [Fact]
    public void Domain_Has_No_Wpf() =>
        Assert.DoesNotContain(typeof(SupplierOffer).Assembly.GetReferencedAssemblies().Select(a => a.Name!),
            n => n!.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase));

    [Theory]
    [InlineData(AwardKind.Total)]
    [InlineData(AwardKind.Partial)]
    [InlineData(AwardKind.ByItem)]
    [InlineData(AwardKind.ByQuantity)]
    [InlineData(AwardKind.MultiSupplier)]
    public void AwardKinds(AwardKind k) => Assert.True(Enum.IsDefined(k));

    [Theory]
    [InlineData(SupplierOfferStatus.Draft)]
    [InlineData(SupplierOfferStatus.Submitted)]
    [InlineData(SupplierOfferStatus.UnderEvaluation)]
    [InlineData(SupplierOfferStatus.Compared)]
    [InlineData(SupplierOfferStatus.Awarded)]
    [InlineData(SupplierOfferStatus.PartiallyAwarded)]
    [InlineData(SupplierOfferStatus.Rejected)]
    [InlineData(SupplierOfferStatus.Expired)]
    [InlineData(SupplierOfferStatus.Archived)]
    public void OfferStatuses(SupplierOfferStatus s) => Assert.True(Enum.IsDefined(s));

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(1, 1)]
    [InlineData(2.345678, 2.345678)]
    [InlineData(100, 950)]
    [InlineData(0, 1)]
    public void Rate_Roundtrip_Normalize(decimal amount, decimal rate)
    {
        if (rate <= 0) { Assert.Throws<InvalidOperationException>(() => OfferCurrencyNormalizer.ToBase(amount, "USD", "CLP", rate)); return; }
        var converted = OfferCurrencyNormalizer.ToBase(amount, "USD", "CLP", rate);
        Assert.Equal(PrecisionPolicy.RoundImporte(amount * rate), converted);
    }

    [Theory]
    [InlineData(CommercialRiskLevel.Low)]
    [InlineData(CommercialRiskLevel.Medium)]
    [InlineData(CommercialRiskLevel.High)]
    [InlineData(CommercialRiskLevel.Critical)]
    public void Risks(CommercialRiskLevel r) => Assert.True(Enum.IsDefined(r));

    [Fact]
    public void SoD_Award_Flag_Default() => Assert.True(new ProcurementSoDPolicy().AwardCreatorCannotApprove);

    [Fact]
    public void HashSet_Permission_Checker_Works()
    {
        var c = new HashSetProcurementPermissionChecker(new[] { ProcurementPermissions.AwardApprove });
        Assert.True(c.Has(ProcurementPermissions.AwardApprove));
        Assert.False(c.Has(ProcurementPermissions.AwardCreate));
    }

    [Theory]
    [InlineData(10, 19, 0, 0, 0)]
    [InlineData(100, 0, 0, 0, 0)]
    [InlineData(200, 19, 10, 5, 5)]
    [InlineData(50.5, 19, 2, 1, 1)]
    [InlineData(1000, 19, 15, 100, 50)]
    public void Acquisition_NonNegative(decimal line, decimal tax, decimal disc, decimal f, decimal i)
    {
        var cost = AcquisitionCostEngine.ComputeLineAcquisitionCost(line, tax, disc, f, i);
        Assert.True(cost >= 0m);
    }

    [Fact]
    public void Award_Empty_Items_Invalid()
    {
        var issues = OfferAwardValidation.ValidateAward(new AwardDecision { Id = AwardId.New(), RfqId = RequestForQuotationId.New() });
        Assert.Contains(issues, x => x.Severity == ProcurementValidationSeverity.Error);
    }

    [Fact]
    public void NormalizeOffer_Sets_Base_Prices()
    {
        var offer = new SupplierOffer
        {
            Id = SupplierOfferId.New(), RfqId = RequestForQuotationId.New(), SupplierId = SupplierId.New(),
            CurrencyCode = "USD",
            Lines = { new OfferLine { LineId = "1", OfferId = SupplierOfferId.New(), Quantity = 2, UnitPriceOriginal = 10 } }
        };
        OfferCurrencyNormalizer.NormalizeOffer(offer, "CLP", 800m);
        Assert.Equal(8000m, offer.Lines[0].UnitPriceBase);
        Assert.Equal(16000m, offer.Lines[0].LineTotalBase);
    }
}
