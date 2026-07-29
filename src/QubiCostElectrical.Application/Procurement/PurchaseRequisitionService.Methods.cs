using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class PurchaseRequisitionService
{
    public async Task<ConsolidationResult> ConsolidateAsync(PurchaseRequisitionId id, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionEdit, actor, "Requisition", "Consolidate");
        var r = await _repo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("RequisitionNotFound");
        var result = ProcurementConsolidationEngine.Consolidate(r.Items);
        await Audit(r.ProjectPath, "Requisition", id.Value, "Consolidate", actor, null,
            $"lines={result.Lines.Count};conflicts={result.Conflicts.Count}", null, "Ok", ct);
        return result;
    }

    public List<NeedGenerationPreviewItem> PreviewFromBudget(IEnumerable<NeedGenerationPreviewItem> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .GroupBy(c => $"{c.Code}|{c.Unit}|{c.BudgetItemId}")
            .Select(g =>
            {
                var first = g.First();
                return new NeedGenerationPreviewItem
                {
                    Code = first.Code,
                    Description = first.Description,
                    Unit = first.Unit,
                    Quantity = g.Sum(x => x.Quantity),
                    BudgetedUnitPrice = first.BudgetedUnitPrice,
                    RequiredDate = g.Min(x => x.RequiredDate),
                    SuggestedRequestDate = g.Min(x => x.SuggestedRequestDate == default ? x.RequiredDate : x.SuggestedRequestDate),
                    ActivityId = first.ActivityId,
                    WbsNodeId = first.WbsNodeId,
                    ResourceId = first.ResourceId,
                    BudgetItemId = first.BudgetItemId,
                    ApuId = first.ApuId,
                    Origin = NeedOriginKind.Budget,
                    OriginId = first.OriginId,
                    Criticidad = g.Max(x => x.Criticidad),
                    Selected = true
                };
            })
            .OrderBy(x => x.Code, StringComparer.Ordinal)
            .ToList();
    }

    public List<NeedGenerationPreviewItem> PreviewFromSchedule(
        IEnumerable<NeedGenerationPreviewItem> candidates,
        ProcurementNeedDatePolicy? policy = null,
        int? leadDays = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        policy ??= new ProcurementNeedDatePolicy();
        return candidates.Select(c =>
        {
            var required = c.RequiredDate == default ? DateTime.Today.AddDays(30) : c.RequiredDate;
            return new NeedGenerationPreviewItem
            {
                Code = c.Code,
                Description = c.Description,
                Unit = c.Unit,
                Quantity = c.Quantity,
                BudgetedUnitPrice = c.BudgetedUnitPrice,
                RequiredDate = required,
                SuggestedRequestDate = policy.SuggestRequestDate(required, leadDays),
                ActivityId = c.ActivityId,
                WbsNodeId = c.WbsNodeId,
                ResourceId = c.ResourceId,
                BudgetItemId = c.BudgetItemId,
                ApuId = c.ApuId,
                Origin = NeedOriginKind.Schedule,
                OriginId = c.OriginId,
                Criticidad = c.Criticidad,
                Selected = true
            };
        }).OrderBy(x => x.SuggestedRequestDate).ThenBy(x => x.Code, StringComparer.Ordinal).ToList();
    }

    public async Task<PurchaseRequisition> ConfirmGenerationAsync(
        string projectPath,
        IReadOnlyList<NeedGenerationPreviewItem> selected,
        string actor,
        NeedOriginKind origin,
        CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RequisitionCreate, actor, "Requisition", "Generate");
        var chosen = selected.Where(s => s.Selected && s.Quantity > 0m).ToList();
        if (chosen.Count == 0) throw new InvalidOperationException("InvalidRequiredQuantity");

        var draft = await CreateAsync(new PurchaseRequisition
        {
            ProjectPath = projectPath,
            RequestedBy = actor,
            RequiredDate = chosen.Min(c => c.RequiredDate),
            PrimaryOrigin = origin,
            Reason = origin == NeedOriginKind.Budget ? "Generado desde presupuesto" : "Generado desde planificación"
        }, actor, ct);

        foreach (var c in chosen)
        {
            await AddItemAsync(draft.Id, new PurchaseRequisitionItem
            {
                ItemId = Guid.NewGuid().ToString("N"),
                RequisitionId = draft.Id,
                ResourceId = c.ResourceId,
                Code = c.Code,
                Description = c.Description,
                Unit = c.Unit,
                RequiredQuantity = c.Quantity,
                BudgetedUnitPrice = c.BudgetedUnitPrice,
                RequiredDate = c.RequiredDate,
                ActivityId = c.ActivityId,
                WbsNodeId = c.WbsNodeId,
                BudgetItemId = c.BudgetItemId,
                ApuId = c.ApuId,
                Criticidad = c.Criticidad,
                Origin = new NeedOrigin
                {
                    Kind = c.Origin,
                    OriginId = c.OriginId,
                    OriginalQuantity = c.Quantity,
                    User = actor,
                    Utc = DateTime.UtcNow,
                    Comment = "Confirmación de generación"
                }
            }, draft.RowVersion, actor, ct);
            draft = (await _repo.GetByIdAsync(draft.Id, ct))!;
        }

        await Audit(projectPath, "Requisition", draft.Id.Value, "Generate", actor, null, origin.ToString(), $"{chosen.Count} ítems", "Ok", ct);
        return draft;
    }

    private async Task<PurchaseRequisition> LoadWritable(PurchaseRequisitionId id, string expectedRowVersion, CancellationToken ct)
    {
        var r = await _repo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("RequisitionNotFound");
        if (!string.Equals(r.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        return r;
    }

    private static PurchaseRequisitionItem NormalizeItem(PurchaseRequisitionItem item, PurchaseRequisition r, string actor) =>
        new()
        {
            ItemId = string.IsNullOrWhiteSpace(item.ItemId) ? Guid.NewGuid().ToString("N") : item.ItemId,
            RequisitionId = r.Id,
            ResourceId = item.ResourceId,
            Code = item.Code?.Trim() ?? string.Empty,
            Description = item.Description?.Trim() ?? string.Empty,
            Type = item.Type,
            Unit = string.IsNullOrWhiteSpace(item.Unit) ? "UN" : item.Unit,
            RequiredQuantity = item.RequiredQuantity,
            CoveredQuantity = item.CoveredQuantity,
            BudgetedUnitPrice = item.BudgetedUnitPrice,
            CurrencyCode = string.IsNullOrWhiteSpace(item.CurrencyCode) ? r.CurrencyCode : item.CurrencyCode,
            RequiredDate = item.RequiredDate == default ? r.RequiredDate : item.RequiredDate,
            ActivityId = item.ActivityId ?? r.ActivityId,
            WbsNodeId = item.WbsNodeId ?? r.WbsNodeId,
            BudgetItemId = item.BudgetItemId,
            ApuId = item.ApuId,
            SuggestedSupplierId = item.SuggestedSupplierId,
            TechnicalSpec = item.TechnicalSpec ?? string.Empty,
            Remarks = item.Remarks ?? string.Empty,
            Criticidad = item.Criticidad,
            Status = RequisitionItemStatus.Open,
            Origin = item.Origin.Kind == default && string.IsNullOrEmpty(item.Origin.OriginId)
                ? new NeedOrigin { Kind = NeedOriginKind.Manual, User = actor, Utc = DateTime.UtcNow, OriginalQuantity = item.RequiredQuantity }
                : item.Origin
        };

    private void Ensure(string permission, string actor, string entity, string action)
    {
        if (_permissions.Has(permission)) return;
        _ = Audit("", entity, "", "Denied", actor, null, permission, action, "Denied", CancellationToken.None);
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }

    private Task Audit(string project, string entity, string id, string action, string actor, string? prev, string? next, string? comment, string result, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectPath = project,
            EntityType = entity,
            EntityId = id,
            Action = action,
            Actor = actor,
            UtcTimestamp = DateTime.UtcNow,
            PreviousState = prev,
            NewState = next,
            Comment = comment ?? string.Empty,
            Result = result
        }, ct);

    private static void ThrowIfErrors(IEnumerable<ProcurementValidationIssue> issues)
    {
        var err = issues.FirstOrDefault(i => i.Severity == ProcurementValidationSeverity.Error);
        if (err is not null) throw new InvalidOperationException($"{err.Code}:{err.Message}");
    }

    private static string Normalize(string? s) => (s ?? string.Empty).Trim();
}
