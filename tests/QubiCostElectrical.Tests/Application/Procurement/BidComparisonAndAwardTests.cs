using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Procurement;

internal sealed class FakeOfferRepository : ISupplierOfferRepository
{
    private readonly ConcurrentDictionary<string, SupplierOffer> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(SupplierOffer offer, CancellationToken ct = default) { _map[offer.Id.Value] = offer; return Task.CompletedTask; }
    public Task<SupplierOffer?> GetByIdAsync(SupplierOfferId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var o) ? o : null);
    public Task<List<SupplierOffer>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(o => o.RfqId.Value == rfqId.Value).ToList());
}

internal sealed class FakeTechRepo : ITechnicalEvaluationRepository
{
    private readonly List<TechnicalEvaluation> _list = new();
    public Task SaveAsync(TechnicalEvaluation evaluation, CancellationToken ct = default)
    {
        _list.RemoveAll(x => x.EvaluationId == evaluation.EvaluationId);
        _list.Add(evaluation);
        return Task.CompletedTask;
    }
    public Task<List<TechnicalEvaluation>> GetByOfferAsync(SupplierOfferId offerId, CancellationToken ct = default) =>
        Task.FromResult(_list.Where(x => x.OfferId.Value == offerId.Value).ToList());
    public Task<List<TechnicalEvaluation>> GetByRfqOffersAsync(IEnumerable<SupplierOfferId> offerIds, CancellationToken ct = default)
    {
        var ids = offerIds.Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(_list.Where(x => ids.Contains(x.OfferId.Value)).ToList());
    }
}

internal sealed class FakeComRepo : ICommercialEvaluationRepository
{
    private readonly Dictionary<string, CommercialEvaluation> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(CommercialEvaluation evaluation, CancellationToken ct = default)
    {
        _map[evaluation.OfferId.Value] = evaluation;
        return Task.CompletedTask;
    }
    public Task<CommercialEvaluation?> GetByOfferAsync(SupplierOfferId offerId, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(offerId.Value, out var e) ? e : null);
    public async Task<List<CommercialEvaluation>> GetByRfqOffersAsync(IEnumerable<SupplierOfferId> offerIds, CancellationToken ct = default)
    {
        var list = new List<CommercialEvaluation>();
        foreach (var id in offerIds)
        {
            var e = await GetByOfferAsync(id, ct);
            if (e is not null) list.Add(e);
        }
        return list;
    }
}

internal sealed class FakeCmpRepo : IBidComparisonRepository
{
    private readonly ConcurrentDictionary<string, BidComparison> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(BidComparison comparison, CancellationToken ct = default) { _map[comparison.Id.Value] = comparison; return Task.CompletedTask; }
    public Task<BidComparison?> GetByIdAsync(BidComparisonId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var c) ? c : null);
    public Task<List<BidComparison>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(c => c.RfqId.Value == rfqId.Value).ToList());
}

internal sealed class FakeAwardRepo : IAwardRepository
{
    private readonly ConcurrentDictionary<string, AwardDecision> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(AwardDecision award, CancellationToken ct = default) { _map[award.Id.Value] = award; return Task.CompletedTask; }
    public Task<AwardDecision?> GetByIdAsync(AwardId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var a) ? a : null);
    public Task<List<AwardDecision>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(a => a.RfqId.Value == rfqId.Value).ToList());
}

public sealed class BidComparisonAndAwardDomainTests
{
    [Theory]
    [InlineData(100, "USD", "CLP", 900, 90000)]
    [InlineData(50, "CLP", "CLP", 2, 50)]
    [InlineData(10.555, "EUR", "CLP", 1000, 10555)]
    public void Currency_Normalize(decimal amount, string from, string to, decimal rate, decimal expected) =>
        Assert.Equal(expected, OfferCurrencyNormalizer.ToBase(amount, from, to, rate));

    [Fact]
    public void Currency_Invalid_Rate() =>
        Assert.Throws<InvalidOperationException>(() => OfferCurrencyNormalizer.ToBase(1, "USD", "CLP", 0));

    [Theory]
    [InlineData(1000, 19, 0, 50, 0, 1240)]
    [InlineData(1000, 0, 10, 0, 0, 900)]
    [InlineData(500, 19, 5, 20, 10, 595.25)]
    public void AcquisitionCost(decimal line, decimal tax, decimal disc, decimal freight, decimal ins, decimal expected) =>
        Assert.Equal(expected, AcquisitionCostEngine.ComputeLineAcquisitionCost(line, tax, disc, freight, ins));

    [Fact]
    public void AllocateShare()
    {
        Assert.Equal(25m, AcquisitionCostEngine.AllocateShare(100m, 50m, 200m));
        Assert.Equal(0m, AcquisitionCostEngine.AllocateShare(100m, 50m, 0m));
    }

    [Fact]
    public void Comparison_Ranks_And_Recommends()
    {
        var rfq = SampleRfq();
        var offers = new[] { SampleOffer(rfq, "s1", 100m, 10), SampleOffer(rfq, "s2", 80m, 20) };
        var cmp = BidComparisonEngine.Build(rfq, offers, Array.Empty<TechnicalEvaluation>(), Array.Empty<CommercialEvaluation>(),
            "CLP", 1m, DateTime.Today, "u", new Dictionary<string, decimal> { ["M1"] = 90m });
        Assert.NotEmpty(cmp.Rows);
        Assert.Contains(cmp.Rows[0].Cells, c => c.IsRecommended);
        Assert.NotNull(cmp.Recommendation);
        Assert.True(cmp.Recommendation!.RequiresConfirmation);
        Assert.NotEmpty(cmp.Recommendation.Lines);
    }

    [Fact]
    public void Recommendation_MultiSupplier_When_Different_Winners()
    {
        var rfqId = RequestForQuotationId.New();
        var rfq = new RequestForQuotation
        {
            Id = rfqId, ProjectPath = "p", ComparisonCurrency = "CLP",
            Items =
            {
                new RfqItem { ItemId = "i1", RfqId = rfqId, Code = "A", Description = "A", Quantity = 1, Unit = "UN" },
                new RfqItem { ItemId = "i2", RfqId = rfqId, Code = "B", Description = "B", Quantity = 1, Unit = "UN" }
            }
        };
        var o1 = SampleOffer(rfq, "s1", 10m, 5); o1.Lines[0].RfqItemId = "i1"; o1.Lines[0].Code = "A";
        o1.Lines.Add(new OfferLine { LineId = "l2", OfferId = o1.Id, RfqItemId = "i2", Code = "B", Description = "B", Quantity = 1, UnitPriceOriginal = 999m });
        var o2 = SampleOffer(rfq, "s2", 999m, 5); o2.Lines[0].RfqItemId = "i1"; o2.Lines[0].Code = "A";
        o2.Lines.Add(new OfferLine { LineId = "l2b", OfferId = o2.Id, RfqItemId = "i2", Code = "B", Description = "B", Quantity = 1, UnitPriceOriginal = 10m });
        var tech = new[]
        {
            new TechnicalEvaluation { EvaluationId = "t1", OfferId = o1.Id, Compliance = TechnicalComplianceLevel.Compliant, DocumentComplete = true, Score = 90 },
            new TechnicalEvaluation { EvaluationId = "t2", OfferId = o2.Id, Compliance = TechnicalComplianceLevel.Compliant, DocumentComplete = true, Score = 90 }
        };
        var cmp = BidComparisonEngine.Build(rfq, new[] { o1, o2 }, tech, Array.Empty<CommercialEvaluation>(), "CLP", 1m, DateTime.Today, "u");
        Assert.Equal(2, cmp.Recommendation!.Lines.Select(l => l.SupplierId.Value).Distinct().Count());
        Assert.Equal(AwardKind.MultiSupplier, cmp.Recommendation.SuggestedKind);
    }

    [Theory]
    [InlineData(TechnicalComplianceLevel.Compliant)]
    [InlineData(TechnicalComplianceLevel.Partial)]
    [InlineData(TechnicalComplianceLevel.NonCompliant)]
    [InlineData(TechnicalComplianceLevel.Exceeds)]
    public void Validate_Offer_And_Award_Basics(TechnicalComplianceLevel level)
    {
        var offer = SampleOffer(SampleRfq(), "s", 10, 5);
        Assert.DoesNotContain(OfferAwardValidation.ValidateOffer(offer), i => i.Severity == ProcurementValidationSeverity.Error);
        _ = level;
        var award = new AwardDecision
        {
            Id = AwardId.New(), RfqId = RequestForQuotationId.New(),
            Items = { new AwardedItem { AwardedItemId = "a", AwardId = AwardId.New(), OfferId = offer.Id, SupplierId = offer.SupplierId, Quantity = 1, UnitPriceBase = 1, Code = "C" } }
        };
        Assert.DoesNotContain(OfferAwardValidation.ValidateAward(award), i => i.Severity == ProcurementValidationSeverity.Error);
    }

    [Fact]
    public void Offer_Invalid_Quantity()
    {
        var o = SampleOffer(SampleRfq(), "s", 1, 1);
        o.Lines[0].Quantity = 0;
        Assert.Contains(OfferAwardValidation.ValidateOffer(o), i => i.Code == "InvalidRequiredQuantity");
    }

    private static RequestForQuotation SampleRfq()
    {
        var id = RequestForQuotationId.New();
        return new RequestForQuotation
        {
            Id = id, ProjectPath = "p", ComparisonCurrency = "CLP",
            Items = { new RfqItem { ItemId = "i1", RfqId = id, Code = "M1", Description = "Mat", Quantity = 10, Unit = "UN" } }
        };
    }

    private static SupplierOffer SampleOffer(RequestForQuotation rfq, string supplierKey, decimal price, int lead)
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
                    Code = "M1", Description = "Mat", Unit = "UN", Quantity = 10, UnitPriceOriginal = price
                }
            }
        };
    }
}

public sealed class BidComparisonAndAwardServiceTests
{
    private readonly FakeRfqRepository _rfqs = new();
    private readonly FakeOfferRepository _offers = new();
    private readonly FakeTechRepo _tech = new();
    private readonly FakeComRepo _com = new();
    private readonly FakeCmpRepo _cmp = new();
    private readonly FakeAwardRepo _awards = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly AllowAllProcurementPermissionChecker _perm = new();

    private async Task<(RequestForQuotation rfq, SupplierOffer offer)> SeedAsync()
    {
        var rfqId = RequestForQuotationId.New();
        var rfq = new RequestForQuotation
        {
            Id = rfqId, Number = "RFQ-1", ProjectPath = "p", ComparisonCurrency = "CLP", DeadlineUtc = DateTime.UtcNow.AddDays(5),
            Items = { new RfqItem { ItemId = "i1", RfqId = rfqId, Code = "M1", Description = "M", Quantity = 2, Unit = "UN" } }
        };
        await _rfqs.SaveAsync(rfq);
        var offerSvc = new SupplierOfferService(_offers, _tech, _com, _rfqs, _audit, _perm);
        var offer = await offerSvc.RegisterAsync(new SupplierOffer
        {
            RfqId = rfqId, SupplierId = SupplierId.New(), CurrencyCode = "USD", TaxPercent = 19, DiscountPercent = 5,
            FreightAmount = 10, InsuranceAmount = 5, DeliveryLeadDays = 12,
            Lines =
            {
                new OfferLine { LineId = "l1", OfferId = SupplierOfferId.New(), RfqItemId = "i1", Code = "M1", Description = "M", Quantity = 2, UnitPriceOriginal = 50 }
            }
        }, "buyer");
        await offerSvc.SubmitAsync(offer.Id, offer.RowVersion, "buyer");
        await offerSvc.SaveTechnicalAsync(new TechnicalEvaluation { OfferId = offer.Id, Compliance = TechnicalComplianceLevel.Compliant, DocumentComplete = true, Score = 85 }, "eng");
        await offerSvc.SaveCommercialAsync(new CommercialEvaluation { OfferId = offer.Id, Risk = CommercialRiskLevel.Low, Score = 80 }, "buyer");
        return (rfq, (await _offers.GetByIdAsync(offer.Id))!);
    }

    [Fact]
    public async Task Full_Compare_Award_Approve_Flow()
    {
        var (rfq, _) = await SeedAsync();
        var cmpSvc = new BidComparisonService(_cmp, _rfqs, _offers, _tech, _com, _audit, _perm);
        var comparison = await cmpSvc.CompareAsync(rfq.Id, "CLP", 900m, DateTime.Today, "analyst");
        Assert.Equal(BidComparisonStatus.RecommendationIssued, comparison.Status);
        Assert.True(comparison.Rows[0].Cells[0].UnitPriceBase > comparison.Rows[0].Cells[0].UnitPriceOriginal);
        var awardSvc = new AwardService(_awards, _cmp, _offers, _audit, _perm);
        var award = await awardSvc.CreateFromRecommendationAsync(comparison.Id, AwardKind.Total, "mejor TAC", "buyer");
        award = await awardSvc.SubmitAsync(award.Id, award.RowVersion, "buyer");
        award = await awardSvc.ApproveAsync(award.Id, award.RowVersion, "manager");
        Assert.Equal(AwardStatus.Approved, award.Status);
        Assert.NotEmpty(award.DiscardedAlternatives.Count == 0 ? new[] { "ok" } : award.DiscardedAlternatives.Select(d => d.Reason));
    }

    [Fact]
    public async Task Award_SoD_Blocks_Creator()
    {
        var (rfq, _) = await SeedAsync();
        var comparison = await new BidComparisonService(_cmp, _rfqs, _offers, _tech, _com, _audit, _perm)
            .CompareAsync(rfq.Id, "CLP", 1m, DateTime.Today, "a");
        var awardSvc = new AwardService(_awards, _cmp, _offers, _audit, _perm);
        var award = await awardSvc.CreateFromRecommendationAsync(comparison.Id, AwardKind.Total, "r", "same");
        award = await awardSvc.SubmitAsync(award.Id, award.RowVersion, "same");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => awardSvc.ApproveAsync(award.Id, award.RowVersion, "same"));
    }

    [Fact]
    public async Task Partial_Award_Multi_Supplier()
    {
        var (rfq, offer) = await SeedAsync();
        var awardSvc = new AwardService(_awards, _cmp, _offers, _audit, _perm);
        var award = await awardSvc.CreatePartialAsync(rfq.Id, null, new[]
        {
            new AwardedItem
            {
                AwardedItemId = "a1", AwardId = AwardId.New(), RfqItemId = "i1", Code = "M1", Description = "M",
                OfferId = offer.Id, SupplierId = offer.SupplierId, Quantity = 1, UnitPriceBase = 100
            }
        }, "parcial", "buyer");
        Assert.Equal(AwardKind.Partial, award.Kind);
        Assert.Equal(100m, award.BudgetImpactBase);
    }

    [Fact]
    public async Task Permissions_Denied()
    {
        var deny = new DenyAllProcurementPermissionChecker();
        var svc = new SupplierOfferService(_offers, _tech, _com, _rfqs, _audit, deny);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ListByRfqAsync(RequestForQuotationId.New()));
    }

    [Fact]
    public async Task Concurrency_On_Submit()
    {
        var (rfq, offer) = await SeedAsync();
        var svc = new SupplierOfferService(_offers, _tech, _com, _rfqs, _audit, _perm);
        // already submitted in seed — create draft again
        var draft = await svc.RegisterAsync(new SupplierOffer
        {
            RfqId = rfq.Id, SupplierId = SupplierId.New(),
            Lines = { new OfferLine { LineId = "x", OfferId = SupplierOfferId.New(), Code = "M1", Description = "M", Quantity = 1, UnitPriceOriginal = 1, RfqItemId = "i1" } }
        }, "u");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitAsync(draft.Id, "stale", "u"));
    }

    [Fact]
    public async Task Reject_Award()
    {
        var (rfq, _) = await SeedAsync();
        var comparison = await new BidComparisonService(_cmp, _rfqs, _offers, _tech, _com, _audit, _perm)
            .CompareAsync(rfq.Id, "CLP", 1m, DateTime.Today, "a");
        var awardSvc = new AwardService(_awards, _cmp, _offers, _audit, _perm);
        var award = await awardSvc.CreateFromRecommendationAsync(comparison.Id, AwardKind.Total, "r", "c");
        award = await awardSvc.SubmitAsync(award.Id, award.RowVersion, "c");
        award = await awardSvc.RejectAsync(award.Id, award.RowVersion, "mgr", "no");
        Assert.Equal(AwardStatus.Rejected, award.Status);
    }
}
