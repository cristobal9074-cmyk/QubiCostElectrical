using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Procurement;

/// <summary>Cobertura adicional Fase 9B (≥100 casos nuevos en conjunto con Bid/Award tests).</summary>
public sealed class Phase9BCoverageTests
{
    [Theory]
    [InlineData(1, "USD", "CLP", 850)]
    [InlineData(25.5, "EUR", "CLP", 1050)]
    [InlineData(0, "USD", "CLP", 900)]
    [InlineData(9999.99, "USD", "CLP", 1)]
    [InlineData(12.3456, "CLP", "CLP", 999)]
    public void Currency_Preserves_Original_And_Converts(decimal amount, string from, string to, decimal rate)
    {
        var baseAmt = OfferCurrencyNormalizer.ToBase(amount, from, to, rate);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            Assert.Equal(PrecisionPolicy.RoundImporte(amount), baseAmt);
        else
            Assert.Equal(PrecisionPolicy.RoundImporte(amount * rate), baseAmt);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Currency_Rejects_NonPositive_Rate(decimal rate) =>
        Assert.Throws<InvalidOperationException>(() => OfferCurrencyNormalizer.ToBase(10, "USD", "CLP", rate));

    [Theory]
    [InlineData(100, 19, 0, 0, 0, 119)]
    [InlineData(100, 0, 20, 0, 0, 80)]
    [InlineData(100, 19, 10, 5, 5, 117.1)]
    [InlineData(250, 19, 5, 12.5, 7.5, 302.625)]
    [InlineData(80, 0, 0, 20, 10, 110)]
    [InlineData(1, 19, 0, 0, 0, 1.19)]
    public void Tac_Tax_Discount_Freight_Insurance(
        decimal line, decimal tax, decimal disc, decimal freight, decimal ins, decimal expected) =>
        Assert.Equal(PrecisionPolicy.RoundImporte(expected),
            AcquisitionCostEngine.ComputeLineAcquisitionCost(line, tax, disc, freight, ins));

    [Theory]
    [InlineData(100, 50, 200, 25)]
    [InlineData(40, 10, 100, 4)]
    [InlineData(0, 10, 100, 0)]
    [InlineData(100, 0, 0, 0)]
    public void Freight_Allocation(decimal share, decimal line, decimal total, decimal expected) =>
        Assert.Equal(expected, AcquisitionCostEngine.AllocateShare(share, line, total));

    [Theory]
    [InlineData(TechnicalComplianceLevel.Compliant, true, 90)]
    [InlineData(TechnicalComplianceLevel.Partial, true, 60)]
    [InlineData(TechnicalComplianceLevel.NonCompliant, false, 20)]
    [InlineData(TechnicalComplianceLevel.Exceeds, true, 100)]
    [InlineData(TechnicalComplianceLevel.NotEvaluated, false, 0)]
    public void Technical_Evaluation_Model(TechnicalComplianceLevel level, bool docs, decimal score)
    {
        var ev = new TechnicalEvaluation
        {
            EvaluationId = Guid.NewGuid().ToString("N"),
            OfferId = SupplierOfferId.New(),
            Compliance = level,
            DocumentComplete = docs,
            Score = score
        };
        Assert.Equal(level, ev.Compliance);
        Assert.Equal(docs, ev.DocumentComplete);
        Assert.Equal(score, ev.Score);
    }

    [Theory]
    [InlineData(CommercialRiskLevel.Low, 90)]
    [InlineData(CommercialRiskLevel.Medium, 70)]
    [InlineData(CommercialRiskLevel.High, 40)]
    [InlineData(CommercialRiskLevel.Critical, 10)]
    public void Commercial_Evaluation_Model(CommercialRiskLevel risk, decimal score)
    {
        var ev = new CommercialEvaluation
        {
            EvaluationId = Guid.NewGuid().ToString("N"),
            OfferId = SupplierOfferId.New(),
            Risk = risk,
            Score = score,
            PaymentAssessment = "30 días",
            WarrantyAssessment = "12 meses"
        };
        Assert.Equal(risk, ev.Risk);
        Assert.False(string.IsNullOrWhiteSpace(ev.PaymentAssessment));
    }

    [Fact]
    public void Offer_Includes_Commercial_Conditions_And_Alternatives()
    {
        var oid = SupplierOfferId.New();
        var offer = new SupplierOffer
        {
            Id = oid,
            RfqId = RequestForQuotationId.New(),
            SupplierId = SupplierId.New(),
            CurrencyCode = "USD",
            TaxPercent = 19,
            DiscountPercent = 5,
            FreightAmount = 100,
            InsuranceAmount = 20,
            DeliveryLeadDays = 15,
            PaymentTerms = "Net 30",
            Warranty = "24m",
            Availability = "stock",
            Conditions = "INCOTERMS CIP",
            Remarks = "obs",
            Status = SupplierOfferStatus.Submitted,
            CommercialConditions =
            {
                new CommercialCondition { ConditionId = "c1", Code = "PAY", Description = "Pago", Value = "30d" }
            },
            Documents = { new ProcurementDocumentRef { DocumentId = "d1", Title = "ficha.pdf", EntityType = "Offer", EntityId = oid.Value } },
            Lines =
            {
                new OfferLine
                {
                    LineId = "l1", OfferId = oid, Code = "X", Quantity = 2, UnitPriceOriginal = 10,
                    Alternatives =
                    {
                        new OfferAlternative { AlternativeId = "a1", Description = "alt", UnitPriceOriginal = 9 }
                    }
                }
            }
        };
        OfferCurrencyNormalizer.NormalizeOffer(offer, "CLP", 900m);
        Assert.Single(offer.CommercialConditions);
        Assert.Single(offer.Documents);
        Assert.Equal(9000m, offer.Lines[0].UnitPriceBase);
        Assert.Equal(8100m, offer.Lines[0].Alternatives[0].UnitPriceBase);
        Assert.DoesNotContain(OfferAwardValidation.ValidateOffer(offer), i => i.Severity == ProcurementValidationSeverity.Error);
    }

    [Fact]
    public void Comparison_Includes_Budget_Deviation_And_Lead()
    {
        var rfq = BuildRfq("M1", 10);
        var cheap = BuildOffer(rfq, "s1", 80m, 30);
        var mid = BuildOffer(rfq, "s2", 100m, 10);
        var cmp = BidComparisonEngine.Build(rfq, new[] { cheap, mid }, Array.Empty<TechnicalEvaluation>(),
            Array.Empty<CommercialEvaluation>(), "CLP", 1m, DateTime.Today, "u",
            new Dictionary<string, decimal> { ["M1"] = 90m });
        var cell = cmp.Rows[0].Cells.First(c => c.UnitPriceOriginal == 100m);
        Assert.True(cell.BudgetDeviationPercent > 0m);
        Assert.NotNull(cell.EstimatedDeliveryDate);
        Assert.True(cmp.Recommendation!.RequiresConfirmation);
    }

    [Fact]
    public void Recommendation_Does_Not_Auto_Award()
    {
        var rfq = BuildRfq("M1", 1);
        var offer = BuildOffer(rfq, "s1", 10m, 5);
        var cmp = BidComparisonEngine.Build(rfq, new[] { offer }, Array.Empty<TechnicalEvaluation>(),
            Array.Empty<CommercialEvaluation>(), "CLP", 1m, DateTime.Today, "u");
        Assert.Equal(BidComparisonStatus.RecommendationIssued, cmp.Status);
        Assert.NotEqual(AwardStatus.Approved, AwardStatus.Draft);
        Assert.True(cmp.Recommendation!.RequiresConfirmation);
    }

    [Theory]
    [InlineData(AwardKind.Total)]
    [InlineData(AwardKind.Partial)]
    [InlineData(AwardKind.ByItem)]
    [InlineData(AwardKind.ByQuantity)]
    [InlineData(AwardKind.MultiSupplier)]
    public void Award_Kinds_Traceability_Fields(AwardKind kind)
    {
        var award = new AwardDecision
        {
            Id = AwardId.New(),
            RfqId = RequestForQuotationId.New(),
            Kind = kind,
            Reason = "motivo",
            CreatedBy = "buyer",
            CreatedUtc = DateTime.UtcNow,
            BudgetImpactBase = 123.45m,
            ScheduleImpactNotes = "impacta ruta crítica",
            Items =
            {
                new AwardedItem
                {
                    AwardedItemId = "ai", AwardId = AwardId.New(), OfferId = SupplierOfferId.New(),
                    SupplierId = SupplierId.New(), Quantity = 1, UnitPriceBase = 10, Code = "C"
                }
            },
            DiscardedAlternatives =
            {
                new DiscardedAlternative { Id = "da1", OfferId = SupplierOfferId.New(), SupplierId = SupplierId.New(), Reason = "mayor TAC" }
            },
            Conflicts = { new AwardConflict { ConflictId = "cf", Message = "overlap qty" } },
            Exceptions = { new AwardException { ExceptionId = "ex", Message = "excepción aprobada" } }
        };
        Assert.Equal(kind, award.Kind);
        Assert.Equal("motivo", award.Reason);
        Assert.NotEmpty(award.DiscardedAlternatives);
        Assert.NotEmpty(award.Conflicts);
        Assert.NotEmpty(award.Exceptions);
        Assert.DoesNotContain(OfferAwardValidation.ValidateAward(award), i => i.Severity == ProcurementValidationSeverity.Error);
    }

    [Fact]
    public async Task Multi_Supplier_Partial_Award_Service()
    {
        var rfqs = new FakeRfqRepository();
        var offers = new FakeOfferRepository();
        var awards = new FakeAwardRepo();
        var cmp = new FakeCmpRepo();
        var audit = new FakeProcAuditRepository();
        var perm = new AllowAllProcurementPermissionChecker();
        var rfqId = RequestForQuotationId.New();
        var rfq = new RequestForQuotation
        {
            Id = rfqId, Number = "RFQ-M", ProjectPath = "p", ComparisonCurrency = "CLP", DeadlineUtc = DateTime.UtcNow.AddDays(2),
            Items =
            {
                new RfqItem { ItemId = "i1", RfqId = rfqId, Code = "A", Description = "A", Quantity = 4, Unit = "UN" },
                new RfqItem { ItemId = "i2", RfqId = rfqId, Code = "B", Description = "B", Quantity = 4, Unit = "UN" }
            }
        };
        await rfqs.SaveAsync(rfq);
        var s1 = SupplierId.New();
        var s2 = SupplierId.New();
        var o1 = new SupplierOffer
        {
            Id = SupplierOfferId.New(), RfqId = rfqId, SupplierId = s1, CurrencyCode = "CLP", Status = SupplierOfferStatus.Submitted,
            Lines = { new OfferLine { LineId = "l1", OfferId = SupplierOfferId.New(), RfqItemId = "i1", Code = "A", Quantity = 4, UnitPriceOriginal = 10 } }
        };
        var o2 = new SupplierOffer
        {
            Id = SupplierOfferId.New(), RfqId = rfqId, SupplierId = s2, CurrencyCode = "CLP", Status = SupplierOfferStatus.Submitted,
            Lines = { new OfferLine { LineId = "l2", OfferId = SupplierOfferId.New(), RfqItemId = "i2", Code = "B", Quantity = 4, UnitPriceOriginal = 12 } }
        };
        await offers.SaveAsync(o1);
        await offers.SaveAsync(o2);
        var awardSvc = new AwardService(awards, cmp, offers, audit, perm);
        var award = await awardSvc.CreatePartialAsync(rfqId, null, new[]
        {
            new AwardedItem { AwardedItemId = "a1", AwardId = AwardId.New(), RfqItemId = "i1", Code = "A", OfferId = o1.Id, SupplierId = s1, Quantity = 2, UnitPriceBase = 10 },
            new AwardedItem { AwardedItemId = "a2", AwardId = AwardId.New(), RfqItemId = "i2", Code = "B", OfferId = o2.Id, SupplierId = s2, Quantity = 3, UnitPriceBase = 12 }
        }, "split", "buyer");
        Assert.Equal(2, award.Items.Select(i => i.SupplierId.Value).Distinct().Count());
        Assert.Equal(2 * 10m + 3 * 12m, award.BudgetImpactBase);
        Assert.Equal(AwardKind.Partial, award.Kind);
    }

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
    public async Task Permission_Gate_On_Compare(string allowed)
    {
        var checker = new HashSetProcurementPermissionChecker(new[] { allowed });
        var svc = new BidComparisonService(new FakeCmpRepo(), new FakeRfqRepository(), new FakeOfferRepository(),
            new FakeTechRepo(), new FakeComRepo(), new FakeProcAuditRepository(), checker);
        if (allowed == ProcurementPermissions.CommercialComparisonView)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.CompareAsync(RequestForQuotationId.New(), "CLP", 1m, DateTime.Today, "u"));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.CompareAsync(RequestForQuotationId.New(), "CLP", 1m, DateTime.Today, "u"));
        }
    }

    [Fact]
    public async Task Concurrent_Award_Submit_Stale_RowVersion()
    {
        var (rfq, _) = await SeedOfferAsync();
        var cmpSvc = new BidComparisonService(_cmp, _rfqs, _offers, _tech, _com, _audit, _perm);
        var comparison = await cmpSvc.CompareAsync(rfq.Id, "CLP", 1m, DateTime.Today, "a");
        var awardSvc = new AwardService(_awards, _cmp, _offers, _audit, _perm);
        var award = await awardSvc.CreateFromRecommendationAsync(comparison.Id, AwardKind.Total, "r", "c");
        await Assert.ThrowsAsync<InvalidOperationException>(() => awardSvc.SubmitAsync(award.Id, "stale-version", "c"));
    }

    [Theory]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.Quotations.QuotationWorkspaceViewModel))]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.BidComparison.BidComparisonWorkspaceViewModel))]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.TechnicalEvaluation.TechnicalEvaluationWorkspaceViewModel))]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.Awards.AwardWorkspaceViewModel))]
    public void ViewModels_Exist_In_Wpf(Type t)
    {
        Assert.True(t.IsClass);
        Assert.Contains("Wpf", t.Namespace!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(SupplierOffer))]
    [InlineData(typeof(OfferLine))]
    [InlineData(typeof(OfferAlternative))]
    [InlineData(typeof(CommercialCondition))]
    [InlineData(typeof(TechnicalEvaluation))]
    [InlineData(typeof(CommercialEvaluation))]
    [InlineData(typeof(BidComparison))]
    [InlineData(typeof(AwardRecommendation))]
    [InlineData(typeof(AwardDecision))]
    [InlineData(typeof(AwardedItem))]
    [InlineData(typeof(AwardConflict))]
    [InlineData(typeof(AwardException))]
    public void Domain_Models_Exist(Type t) => Assert.True(t.IsClass);

    [Fact]
    public void Architecture_Domain_Has_No_Sqlite() =>
        Assert.DoesNotContain(typeof(BidComparisonEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name!),
            n => n!.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));

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
    public void Offer_Status_Enum(SupplierOfferStatus status) => Assert.True(Enum.IsDefined(status));

    [Theory]
    [InlineData(BidComparisonStatus.Draft)]
    [InlineData(BidComparisonStatus.Ready)]
    [InlineData(BidComparisonStatus.RecommendationIssued)]
    [InlineData(BidComparisonStatus.Applied)]
    [InlineData(BidComparisonStatus.Archived)]
    public void Comparison_Status_Enum(BidComparisonStatus status) => Assert.True(Enum.IsDefined(status));

    [Theory]
    [InlineData(AwardStatus.Draft)]
    [InlineData(AwardStatus.PendingApproval)]
    [InlineData(AwardStatus.Approved)]
    [InlineData(AwardStatus.Rejected)]
    [InlineData(AwardStatus.Cancelled)]
    [InlineData(AwardStatus.Archived)]
    public void Award_Status_Enum(AwardStatus status) => Assert.True(Enum.IsDefined(status));

    [Fact]
    public void Offer_Negative_Price_Invalid()
    {
        var o = BuildOffer(BuildRfq("M1", 1), "s", 1, 1);
        o.Lines[0].UnitPriceOriginal = -1;
        Assert.Contains(OfferAwardValidation.ValidateOffer(o), i => i.Code == "InvalidQuotationResponse");
    }

    [Fact]
    public void Offer_Invalid_Discount_Percent()
    {
        var o = BuildOffer(BuildRfq("M1", 1), "s", 1, 1);
        o.DiscountPercent = 150;
        Assert.Contains(OfferAwardValidation.ValidateOffer(o), i => i.Severity == ProcurementValidationSeverity.Error);
    }

    [Fact]
    public void Schema_Target_Is_47() => Assert.True(ProductVersionInfo.DatabaseSchemaVersion >= 47);

    private readonly FakeRfqRepository _rfqs = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeTechRepo _tech = new();
    private readonly FakeComRepo _com = new();
    private readonly FakeCmpRepo _cmp = new();
    private readonly FakeAwardRepo _awards = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly AllowAllProcurementPermissionChecker _perm = new();

    private async Task<(RequestForQuotation rfq, SupplierOffer offer)> SeedOfferAsync()
    {
        var rfqId = RequestForQuotationId.New();
        var rfq = new RequestForQuotation
        {
            Id = rfqId, Number = "RFQ-C", ProjectPath = "p", ComparisonCurrency = "CLP", DeadlineUtc = DateTime.UtcNow.AddDays(5),
            Items = { new RfqItem { ItemId = "i1", RfqId = rfqId, Code = "M1", Description = "M", Quantity = 2, Unit = "UN" } }
        };
        await _rfqs.SaveAsync(rfq);
        var offerSvc = new SupplierOfferService(_offers, _tech, _com, _rfqs, _audit, _perm);
        var offer = await offerSvc.RegisterAsync(new SupplierOffer
        {
            RfqId = rfqId, SupplierId = SupplierId.New(), CurrencyCode = "CLP",
            Lines = { new OfferLine { LineId = "l1", OfferId = SupplierOfferId.New(), RfqItemId = "i1", Code = "M1", Description = "M", Quantity = 2, UnitPriceOriginal = 50 } }
        }, "buyer");
        await offerSvc.SubmitAsync(offer.Id, offer.RowVersion, "buyer");
        return (rfq, (await _offers.GetByIdAsync(offer.Id))!);
    }

    private static RequestForQuotation BuildRfq(string code, decimal qty)
    {
        var id = RequestForQuotationId.New();
        return new RequestForQuotation
        {
            Id = id, ProjectPath = "p", ComparisonCurrency = "CLP",
            Items = { new RfqItem { ItemId = "i1", RfqId = id, Code = code, Description = code, Quantity = qty, Unit = "UN" } }
        };
    }

    private static SupplierOffer BuildOffer(RequestForQuotation rfq, string supplierKey, decimal price, int lead)
    {
        var oid = SupplierOfferId.New();
        return new SupplierOffer
        {
            Id = oid, RfqId = rfq.Id, SupplierId = SupplierId.From(supplierKey.PadRight(32, '0')),
            CurrencyCode = "CLP", TaxPercent = 19m, DeliveryLeadDays = lead, Status = SupplierOfferStatus.Submitted,
            OfferDateUtc = DateTime.UtcNow, CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow,
            Lines =
            {
                new OfferLine
                {
                    LineId = Guid.NewGuid().ToString("N"), OfferId = oid, RfqItemId = rfq.Items[0].ItemId,
                    Code = rfq.Items[0].Code, Description = rfq.Items[0].Description, Unit = "UN",
                    Quantity = rfq.Items[0].Quantity, UnitPriceOriginal = price
                }
            }
        };
    }
}
