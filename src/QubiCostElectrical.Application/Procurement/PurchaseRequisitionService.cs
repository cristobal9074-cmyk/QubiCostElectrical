using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class NeedGenerationPreviewItem
{
    public string Code { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Unit { get; init; } = "UN";
    public decimal Quantity { get; init; }
    public decimal BudgetedUnitPrice { get; init; }
    public DateTime RequiredDate { get; init; }
    public DateTime SuggestedRequestDate { get; init; }
    public string? ActivityId { get; init; }
    public string? WbsNodeId { get; init; }
    public string? ResourceId { get; init; }
    public string? BudgetItemId { get; init; }
    public string? ApuId { get; init; }
    public NeedOriginKind Origin { get; init; }
    public string OriginId { get; init; } = string.Empty;
    public PurchasePriority Criticidad { get; init; } = PurchasePriority.Normal;
    public bool Selected { get; set; } = true;
}

public interface IPurchaseRequisitionService
{
    Task<PurchaseRequisition> CreateAsync(PurchaseRequisition draft, string actor, CancellationToken ct = default);
    Task<PurchaseRequisition> AddItemAsync(PurchaseRequisitionId id, PurchaseRequisitionItem item, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PurchaseRequisition> SubmitAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PurchaseRequisition> ApproveAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, string? comment, CancellationToken ct = default);
    Task<PurchaseRequisition> RejectAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<PurchaseRequisition> CancelAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<PurchaseRequisition?> GetAsync(PurchaseRequisitionId id, CancellationToken ct = default);
    Task<List<PurchaseRequisition>> SearchAsync(string projectPath, PurchaseRequisitionStatus? status, int skip, int take, CancellationToken ct = default);
    Task<ConsolidationResult> ConsolidateAsync(PurchaseRequisitionId id, string actor, CancellationToken ct = default);
    List<NeedGenerationPreviewItem> PreviewFromBudget(IEnumerable<NeedGenerationPreviewItem> candidates);
    List<NeedGenerationPreviewItem> PreviewFromSchedule(IEnumerable<NeedGenerationPreviewItem> candidates, ProcurementNeedDatePolicy? policy = null, int? leadDays = null);
    Task<PurchaseRequisition> ConfirmGenerationAsync(string projectPath, IReadOnlyList<NeedGenerationPreviewItem> selected, string actor, NeedOriginKind origin, CancellationToken ct = default);
}

public sealed partial class PurchaseRequisitionService : IPurchaseRequisitionService
{
    private readonly IPurchaseRequisitionRepository _repo;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;
    private readonly ProcurementSoDPolicy _sod;

    public PurchaseRequisitionService(
        IPurchaseRequisitionRepository repo,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions,
        ProcurementSoDPolicy? sod = null)
    {
        _repo = repo;
        _audit = audit;
        _permissions = permissions;
        _sod = sod ?? new ProcurementSoDPolicy();
    }

    public async Task<PurchaseRequisition> CreateAsync(PurchaseRequisition draft, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionCreate, actor, "Requisition", "Create");
        var n = await _repo.GetNextNumberAsync(draft.ProjectPath, ct);
        draft = new PurchaseRequisition
        {
            Id = draft.Id ?? PurchaseRequisitionId.New(),
            Number = string.IsNullOrWhiteSpace(draft.Number) ? $"PR-{n:D5}" : draft.Number,
            ProjectPath = draft.ProjectPath,
            WbsNodeId = draft.WbsNodeId,
            ActivityId = draft.ActivityId,
            RequestedBy = string.IsNullOrWhiteSpace(draft.RequestedBy) ? actor : draft.RequestedBy,
            Responsible = draft.Responsible,
            CreatedUtc = DateTime.UtcNow,
            RequiredDate = draft.RequiredDate == default ? DateTime.Today.AddDays(14) : draft.RequiredDate,
            Priority = draft.Priority,
            Status = PurchaseRequisitionStatus.Draft,
            Reason = draft.Reason ?? string.Empty,
            CurrencyCode = string.IsNullOrWhiteSpace(draft.CurrencyCode) ? "CLP" : draft.CurrencyCode,
            Comment = draft.Comment ?? string.Empty,
            PrimaryOrigin = draft.PrimaryOrigin,
            Items = draft.Items ?? new List<PurchaseRequisitionItem>(),
            RowVersion = Guid.NewGuid().ToString("N")
        };
        ThrowIfErrors(ProcurementValidation.ValidateRequisition(draft));
        await _repo.SaveAsync(draft, ct);
        await Audit(draft.ProjectPath, "Requisition", draft.Id.Value, "Create", actor, null, draft.Number, null, "Ok", ct);
        return draft;
    }

    public async Task<PurchaseRequisition> AddItemAsync(PurchaseRequisitionId id, PurchaseRequisitionItem item, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionEdit, actor, "Requisition", "AddItem");
        var r = await LoadWritable(id, expectedRowVersion, ct);
        if (r.Status is not PurchaseRequisitionStatus.Draft and not PurchaseRequisitionStatus.PendingReview)
            throw new InvalidOperationException("RequisitionNotApproved");
        item = NormalizeItem(item, r, actor);
        ThrowIfErrors(ProcurementValidation.ValidateItem(item, r));
        if (r.Items.Any(x => string.Equals(x.Code, item.Code, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Unit, item.Unit, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Normalize(x.TechnicalSpec), Normalize(item.TechnicalSpec), StringComparison.OrdinalIgnoreCase)
            && x.Status != RequisitionItemStatus.Cancelled))
            throw new InvalidOperationException("DuplicateRequisitionItem");
        r.Items.Add(item);
        r.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(r, ct);
        await Audit(r.ProjectPath, "RequisitionItem", item.ItemId, "Add", actor, null, item.Code, null, "Ok", ct);
        return r;
    }

    public async Task<PurchaseRequisition> SubmitAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionSubmit, actor, "Requisition", "Submit");
        var r = await LoadWritable(id, expectedRowVersion, ct);
        ThrowIfErrors(ProcurementValidation.ValidateRequisition(r));
        var prev = r.Status;
        r.Status = PurchaseRequisitionStatus.PendingReview;
        r.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(r, ct);
        await Audit(r.ProjectPath, "Requisition", r.Id.Value, "Submit", actor, prev.ToString(), r.Status.ToString(), null, "Ok", ct);
        return r;
    }

    public async Task<PurchaseRequisition> ApproveAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, string? comment, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionApprove, actor, "Requisition", "Approve");
        var r = await LoadWritable(id, expectedRowVersion, ct);
        if (_sod.CreatorCannotApprove && string.Equals(r.RequestedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("PermissionDenied:SoD");
        if (r.Status != PurchaseRequisitionStatus.PendingReview)
            throw new InvalidOperationException("RequisitionNotApproved");
        var prev = r.Status;
        r.Status = PurchaseRequisitionStatus.Approved;
        r.Approver = actor;
        r.ApprovedUtc = DateTime.UtcNow;
        r.Comment = string.IsNullOrWhiteSpace(comment) ? r.Comment : comment!;
        r.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(r, ct);
        await Audit(r.ProjectPath, "Requisition", r.Id.Value, "Approve", actor, prev.ToString(), r.Status.ToString(), comment, "Ok", ct);
        return r;
    }

    public async Task<PurchaseRequisition> RejectAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionReject, actor, "Requisition", "Reject");
        var r = await LoadWritable(id, expectedRowVersion, ct);
        var prev = r.Status;
        r.Status = PurchaseRequisitionStatus.Rejected;
        r.RejectionReason = reason;
        r.Approver = actor;
        r.ApprovedUtc = DateTime.UtcNow;
        r.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(r, ct);
        await Audit(r.ProjectPath, "Requisition", r.Id.Value, "Reject", actor, prev.ToString(), r.Status.ToString(), reason, "Ok", ct);
        return r;
    }

    public async Task<PurchaseRequisition> CancelAsync(PurchaseRequisitionId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionEdit, actor, "Requisition", "Cancel");
        var r = await LoadWritable(id, expectedRowVersion, ct);
        var prev = r.Status;
        r.Status = PurchaseRequisitionStatus.Cancelled;
        r.Comment = reason;
        r.RowVersion = Guid.NewGuid().ToString("N");
        await _repo.SaveAsync(r, ct);
        await Audit(r.ProjectPath, "Requisition", r.Id.Value, "Cancel", actor, prev.ToString(), r.Status.ToString(), reason, "Ok", ct);
        return r;
    }

    public Task<PurchaseRequisition?> GetAsync(PurchaseRequisitionId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionView, "system", "Requisition", "Get");
        return _repo.GetByIdAsync(id, ct);
    }

    public Task<List<PurchaseRequisition>> SearchAsync(string projectPath, PurchaseRequisitionStatus? status, int skip, int take, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionView, "system", "Requisition", "Search");
        return _repo.SearchAsync(projectPath, status, skip, take, ct);
    }
}
