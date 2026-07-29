using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProcurementCommitmentSnapshotRepository
{
    Task SaveAsync(ProcurementCommitmentSummary summary, CancellationToken ct = default);
    Task<List<ProcurementCommitmentSummary>> ListByProjectAsync(string projectPath, CancellationToken ct = default);
}

public interface IProcurementCommitmentService
{
    Task<ProcurementCommitmentSummary> BuildAsync(
        string projectPath,
        decimal budgetAmount,
        DateTime? asOfUtc = null,
        CancellationToken ct = default);
    Task<ProcurementCommitmentSummary> SnapshotAsync(string projectPath, decimal budgetAmount, string actor, CancellationToken ct = default);
    Task<List<ProcurementCommitmentSummary>> HistoryAsync(string projectPath, CancellationToken ct = default);
}

public sealed class ProcurementCommitmentService : IProcurementCommitmentService
{
    private readonly IPurchaseRequisitionRepository _prs;
    private readonly ISupplierOfferRepository _offers;
    private readonly IAwardRepository _awards;
    private readonly IPurchaseOrderRepository _orders;
    private readonly IGoodsReceiptRepository _receipts;
    private readonly IRequestForQuotationRepository _rfqs;
    private readonly IProcurementCommitmentSnapshotRepository _snapshots;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;

    public ProcurementCommitmentService(
        IPurchaseRequisitionRepository prs,
        ISupplierOfferRepository offers,
        IAwardRepository awards,
        IPurchaseOrderRepository orders,
        IGoodsReceiptRepository receipts,
        IRequestForQuotationRepository rfqs,
        IProcurementCommitmentSnapshotRepository snapshots,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions)
    {
        _prs = prs;
        _offers = offers;
        _awards = awards;
        _orders = orders;
        _receipts = receipts;
        _rfqs = rfqs;
        _snapshots = snapshots;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<ProcurementCommitmentSummary> BuildAsync(
        string projectPath, decimal budgetAmount, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.ProcurementCommitmentView);
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var prs = await _prs.SearchAsync(projectPath, null, 0, 500, ct);
        var orders = await _orders.GetByProjectAsync(projectPath, ct);
        var receipts = await _receipts.GetByProjectAsync(projectPath, ct);
        var awards = new List<AwardDecision>();
        var offers = new List<SupplierOffer>();
        var rfqs = await _rfqs.SearchAsync(projectPath, null, 0, 200, ct);
        foreach (var rfq in rfqs)
        {
            awards.AddRange(await _awards.GetByRfqAsync(rfq.Id, ct));
            offers.AddRange(await _offers.GetByRfqAsync(rfq.Id, ct));
        }
        return ProcurementCommitmentEngine.Build(projectPath, budgetAmount, prs, offers, awards, orders, receipts, asOf);
    }

    public async Task<ProcurementCommitmentSummary> SnapshotAsync(
        string projectPath, decimal budgetAmount, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.ProcurementDashboardView);
        var summary = await BuildAsync(projectPath, budgetAmount, DateTime.UtcNow, ct);
        await _snapshots.SaveAsync(summary, ct);
        await _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = "ProcurementCommitment", EntityId = summary.SnapshotHash,
            Action = "Snapshot", Actor = actor, UtcTimestamp = DateTime.UtcNow, ProjectPath = projectPath
        }, ct);
        return summary;
    }

    public Task<List<ProcurementCommitmentSummary>> HistoryAsync(string projectPath, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.ProcurementDashboardView);
        return _snapshots.ListByProjectAsync(projectPath, ct);
    }

    private void Ensure(string permission)
    {
        if (_permissions.Has(permission)) return;
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }
}
