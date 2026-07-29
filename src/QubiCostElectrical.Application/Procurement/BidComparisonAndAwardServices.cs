using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IBidComparisonService
{
    Task<BidComparison> CompareAsync(
        RequestForQuotationId rfqId,
        string baseCurrency,
        decimal exchangeRate,
        DateTime exchangeRateDate,
        string actor,
        IReadOnlyDictionary<string, decimal>? budgetUnitPrices = null,
        CancellationToken ct = default);
    Task<BidComparison?> GetAsync(BidComparisonId id, CancellationToken ct = default);
    Task<List<BidComparison>> ListByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}

public sealed class BidComparisonService : IBidComparisonService
{
    private readonly IBidComparisonRepository _repo;
    private readonly IRequestForQuotationRepository _rfqs;
    private readonly ISupplierOfferRepository _offers;
    private readonly ITechnicalEvaluationRepository _tech;
    private readonly ICommercialEvaluationRepository _commercial;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;

    public BidComparisonService(
        IBidComparisonRepository repo,
        IRequestForQuotationRepository rfqs,
        ISupplierOfferRepository offers,
        ITechnicalEvaluationRepository tech,
        ICommercialEvaluationRepository commercial,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions)
    {
        _repo = repo;
        _rfqs = rfqs;
        _offers = offers;
        _tech = tech;
        _commercial = commercial;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<BidComparison> CompareAsync(
        RequestForQuotationId rfqId,
        string baseCurrency,
        decimal exchangeRate,
        DateTime exchangeRateDate,
        string actor,
        IReadOnlyDictionary<string, decimal>? budgetUnitPrices = null,
        CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.CommercialComparisonView, actor);
        if (exchangeRate <= 0m) throw new InvalidOperationException("InvalidExchangeRate");
        var rfq = await _rfqs.GetByIdAsync(rfqId, ct) ?? throw new InvalidOperationException("RfqNotFound");
        var offers = await _offers.GetByRfqAsync(rfqId, ct);
        if (offers.Count == 0) throw new InvalidOperationException("InvalidQuotationResponse");
        var offerIds = offers.Select(o => o.Id);
        var tech = await _tech.GetByRfqOffersAsync(offerIds, ct);
        var commercial = await _commercial.GetByRfqOffersAsync(offerIds, ct);
        var comparison = BidComparisonEngine.Build(
            rfq, offers, tech, commercial,
            string.IsNullOrWhiteSpace(baseCurrency) ? rfq.ComparisonCurrency : baseCurrency,
            exchangeRate, exchangeRateDate, actor, budgetUnitPrices);
        await _repo.SaveAsync(comparison, ct);
        foreach (var o in offers)
        {
            o.Status = SupplierOfferStatus.Compared;
            o.RowVersion = Guid.NewGuid().ToString("N");
            await _offers.SaveAsync(o, ct);
        }
        await _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = "BidComparison", EntityId = comparison.Id.Value,
            Action = "Compare", Actor = actor, UtcTimestamp = DateTime.UtcNow,
            NewState = comparison.Status.ToString(), Result = "Ok", ProjectPath = rfq.ProjectPath
        }, ct);
        return comparison;
    }

    public Task<BidComparison?> GetAsync(BidComparisonId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.CommercialComparisonView, "system");
        return _repo.GetByIdAsync(id, ct);
    }

    public Task<List<BidComparison>> ListByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.CommercialComparisonView, "system");
        return _repo.GetByRfqAsync(rfqId, ct);
    }

    private void Ensure(string permission, string actor)
    {
        if (_permissions.Has(permission)) return;
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }
}

public interface IAwardService
{
    Task<AwardDecision> CreateFromRecommendationAsync(BidComparisonId comparisonId, AwardKind kind, string reason, string actor, CancellationToken ct = default);
    Task<AwardDecision> CreatePartialAsync(RequestForQuotationId rfqId, BidComparisonId? comparisonId, IReadOnlyList<AwardedItem> items, string reason, string actor, CancellationToken ct = default);
    Task<AwardDecision> SubmitAsync(AwardId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<AwardDecision> ApproveAsync(AwardId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<AwardDecision> RejectAsync(AwardId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<AwardDecision?> GetAsync(AwardId id, CancellationToken ct = default);
}

public sealed class AwardService : IAwardService
{
    private readonly IAwardRepository _repo;
    private readonly IBidComparisonRepository _comparisons;
    private readonly ISupplierOfferRepository _offers;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;
    private readonly ProcurementSoDPolicy _sod;

    public AwardService(
        IAwardRepository repo,
        IBidComparisonRepository comparisons,
        ISupplierOfferRepository offers,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions,
        ProcurementSoDPolicy? sod = null)
    {
        _repo = repo;
        _comparisons = comparisons;
        _offers = offers;
        _audit = audit;
        _permissions = permissions;
        _sod = sod ?? new ProcurementSoDPolicy();
    }

    public async Task<AwardDecision> CreateFromRecommendationAsync(
        BidComparisonId comparisonId, AwardKind kind, string reason, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.AwardCreate, actor);
        var comparison = await _comparisons.GetByIdAsync(comparisonId, ct) ?? throw new InvalidOperationException("RfqNotFound");
        var rec = comparison.Recommendation ?? throw new InvalidOperationException("InvalidQuotationResponse");
        if (!rec.RequiresConfirmation) throw new InvalidOperationException("PermissionDenied:AutoAwardBlocked");
        var items = rec.Lines.Select(l => new AwardedItem
        {
            AwardedItemId = Guid.NewGuid().ToString("N"),
            AwardId = AwardId.From("pending"),
            RfqItemId = l.RfqItemId,
            Code = l.Code,
            Description = l.Code,
            OfferId = l.OfferId,
            SupplierId = l.SupplierId,
            Quantity = l.Quantity,
            UnitPriceBase = l.UnitPriceBase,
            TotalBase = PrecisionPolicy.RoundImporte(l.Quantity * l.UnitPriceBase)
        }).ToList();
        return await PersistNew(comparison.RfqId, comparison.Id, comparison.ProjectPath, kind, reason, actor, items, BuildDiscarded(comparison, items), ct);
    }

    public Task<AwardDecision> CreatePartialAsync(
        RequestForQuotationId rfqId, BidComparisonId? comparisonId, IReadOnlyList<AwardedItem> items, string reason, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.AwardCreate, actor);
        return PersistNew(rfqId, comparisonId, "", AwardKind.Partial, reason, actor, items.ToList(), new List<DiscardedAlternative>(), ct);
    }

    public async Task<AwardDecision> SubmitAsync(AwardId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.AwardCreate, actor);
        var award = await LoadWritable(id, expectedRowVersion, ct);
        ThrowIf(OfferAwardValidation.ValidateAward(award));
        award.Status = AwardStatus.PendingApproval;
        award.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(award, ct);
        await Audit(award, "Submit", actor, AwardStatus.Draft.ToString(), award.Status.ToString(), ct);
        return award;
    }

    public async Task<AwardDecision> ApproveAsync(AwardId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.AwardApprove, actor);
        var award = await LoadWritable(id, expectedRowVersion, ct);
        if (_sod.AwardCreatorCannotApprove && string.Equals(award.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("PermissionDenied:SoD");
        if (award.Status != AwardStatus.PendingApproval)
            throw new InvalidOperationException("RequisitionNotApproved");
        award.Status = AwardStatus.Approved;
        award.Approver = actor;
        award.ApprovedUtc = DateTime.UtcNow;
        award.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(award, ct);
        foreach (var group in award.Items.GroupBy(i => i.OfferId.Value))
        {
            var offer = await _offers.GetByIdAsync(SupplierOfferId.From(group.Key), ct);
            if (offer is null) continue;
            offer.Status = award.Kind is AwardKind.Partial or AwardKind.ByQuantity or AwardKind.ByItem
                ? SupplierOfferStatus.PartiallyAwarded
                : SupplierOfferStatus.Awarded;
            offer.RowVersion = Guid.NewGuid().ToString("N");
            await _offers.SaveAsync(offer, ct);
        }
        await Audit(award, "Approve", actor, AwardStatus.PendingApproval.ToString(), award.Status.ToString(), ct);
        return award;
    }

    public async Task<AwardDecision> RejectAsync(AwardId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.AwardReject, actor);
        var award = await LoadWritable(id, expectedRowVersion, ct);
        award.Status = AwardStatus.Rejected;
        award.RejectionReason = reason;
        award.Approver = actor;
        award.ApprovedUtc = DateTime.UtcNow;
        award.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(award, ct);
        await Audit(award, "Reject", actor, AwardStatus.PendingApproval.ToString(), award.Status.ToString(), ct);
        return award;
    }

    public Task<AwardDecision?> GetAsync(AwardId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.AwardAuditView, "system");
        return _repo.GetByIdAsync(id, ct);
    }

    private async Task<AwardDecision> PersistNew(
        RequestForQuotationId rfqId,
        BidComparisonId? comparisonId,
        string projectPath,
        AwardKind kind,
        string reason,
        string actor,
        List<AwardedItem> items,
        List<DiscardedAlternative> discarded,
        CancellationToken ct)
    {
        var id = AwardId.New();
        var award = new AwardDecision
        {
            Id = id,
            RfqId = rfqId,
            ComparisonId = comparisonId,
            ProjectPath = projectPath,
            Kind = kind,
            Status = AwardStatus.Draft,
            Reason = reason ?? string.Empty,
            CreatedBy = actor,
            CreatedUtc = DateTime.UtcNow,
            Items = items.Select(i => new AwardedItem
            {
                AwardedItemId = string.IsNullOrWhiteSpace(i.AwardedItemId) ? Guid.NewGuid().ToString("N") : i.AwardedItemId,
                AwardId = id,
                RfqItemId = i.RfqItemId,
                Code = i.Code,
                Description = i.Description,
                OfferId = i.OfferId,
                SupplierId = i.SupplierId,
                Quantity = i.Quantity,
                UnitPriceBase = i.UnitPriceBase,
                TotalBase = i.TotalBase == 0m ? PrecisionPolicy.RoundImporte(i.Quantity * i.UnitPriceBase) : i.TotalBase,
                CurrencyOriginal = i.CurrencyOriginal,
                UnitPriceOriginal = i.UnitPriceOriginal
            }).ToList(),
            DiscardedAlternatives = discarded,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        award.BudgetImpactBase = PrecisionPolicy.RoundImporte(award.Items.Sum(i => i.TotalBase));
        ThrowIf(OfferAwardValidation.ValidateAward(award));
        await _repo.SaveAsync(award, ct);
        await Audit(award, "Create", actor, null, award.Status.ToString(), ct);
        return award;
    }

    private static List<DiscardedAlternative> BuildDiscarded(BidComparison comparison, List<AwardedItem> awarded)
    {
        var awardedKeys = new HashSet<string>(awarded.Select(a => $"{a.RfqItemId}|{a.OfferId.Value}"), StringComparer.Ordinal);
        var list = new List<DiscardedAlternative>();
        foreach (var row in comparison.Rows)
        {
            foreach (var cell in row.Cells)
            {
                var key = $"{row.RfqItemId}|{cell.OfferId.Value}";
                if (awardedKeys.Contains(key)) continue;
                list.Add(new DiscardedAlternative
                {
                    Id = Guid.NewGuid().ToString("N"),
                    OfferId = cell.OfferId,
                    SupplierId = cell.SupplierId,
                    RfqItemId = row.RfqItemId,
                    Reason = "No seleccionada en recomendación confirmada"
                });
            }
        }
        return list;
    }

    private async Task<AwardDecision> LoadWritable(AwardId id, string expectedRowVersion, CancellationToken ct)
    {
        var award = await _repo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("RfqNotFound");
        if (!string.Equals(award.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        return award;
    }

    private void Ensure(string permission, string actor)
    {
        if (_permissions.Has(permission)) return;
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }

    private Task Audit(AwardDecision award, string action, string actor, string? prev, string? next, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = "Award", EntityId = award.Id.Value, Action = action,
            Actor = actor, UtcTimestamp = DateTime.UtcNow, PreviousState = prev, NewState = next,
            Result = "Ok", ProjectPath = award.ProjectPath, Comment = award.Reason
        }, ct);

    private static void ThrowIf(IEnumerable<ProcurementValidationIssue> issues)
    {
        var err = issues.FirstOrDefault(i => i.Severity == ProcurementValidationSeverity.Error);
        if (err is not null) throw new InvalidOperationException($"{err.Code}:{err.Message}");
    }
}
