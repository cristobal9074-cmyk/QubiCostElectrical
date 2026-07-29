using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ChangeOrder
// ──────────────────────────────────────────────────────────────
partial class ContractChangeService
{
    public async Task<ChangeOrder> CreateChangeOrderAsync(ChangeOrder draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateChangeOrder(_userPermissions.CurrentRole), "CreateChangeOrder");

        var id = ChangeOrderId.New();
        var nextNum = await _changeOrderRepo.GetNextNumberAsync(draft.ContractId.Value, ct);
        var visibleNum = string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"CO-{nextNum:D5}" : draft.VisibleNumber;

        var snapshot = JsonSerializer.Serialize(draft);

        var order = new ChangeOrder
        {
            Id = id,
            ContractId = draft.ContractId,
            ChangeRequestId = draft.ChangeRequestId,
            VisibleNumber = visibleNum,
            Version = 1,
            EffectiveDate = draft.EffectiveDate,
            CurrencyCode = draft.CurrencyCode,
            ChangeAmount = draft.ChangeAmount,
            AddedDays = draft.AddedDays,
            AddedItems = draft.AddedItems,
            ModifiedItems = draft.ModifiedItems,
            RemovedItems = draft.RemovedItems,
            AffectedActivities = draft.AffectedActivities,
            AffectedMilestones = draft.AffectedMilestones,
            CostImpact = draft.CostImpact,
            ScheduleImpactDays = draft.ScheduleImpactDays,
            ForecastImpact = draft.ForecastImpact,
            EvmImpact = draft.EvmImpact,
            Status = ChangeOrderStatus.Draft,
            Approvers = draft.Approvers,
            Comment = draft.Comment,
            HashSha256 = ComputeHash(snapshot),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor
        };

        await _changeOrderRepo.SaveAsync(order, ct);

        // Create initial version
        var versionJson = JsonSerializer.Serialize(order);
        var version = new ChangeOrderVersion
        {
            Id = ChangeOrderVersionId.New(),
            ChangeOrderId = id,
            VersionNumber = 1,
            SnapshotJson = versionJson,
            HashSha256 = ComputeHash(versionJson),
            ChangedBy = actor,
            ChangeReason = "Versión inicial",
            CreatedUtc = DateTime.UtcNow
        };
        await _changeOrderRepo.SaveVersionAsync(version, ct);

        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "CREAR", actor, "Creación de orden de cambio", ct: ct);

        return order;
    }

    public async Task<ChangeOrder> UpdateChangeOrderDraftAsync(ChangeOrder order, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditChangeOrderDraft(_userPermissions.CurrentRole), "EditChangeOrderDraft");

        var existing = await _changeOrderRepo.GetByIdAsync(order.Id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is ChangeOrderStatus.Approved or ChangeOrderStatus.Implemented or ChangeOrderStatus.Closed)
            throw new InvalidOperationException("No se puede editar una orden aprobada, implementada o cerrada.");

        existing.EffectiveDate = order.EffectiveDate;
        existing.CurrencyCode = order.CurrencyCode;
        existing.ChangeAmount = order.ChangeAmount;
        existing.AddedDays = order.AddedDays;
        existing.AddedItems = order.AddedItems;
        existing.ModifiedItems = order.ModifiedItems;
        existing.RemovedItems = order.RemovedItems;
        existing.AffectedActivities = order.AffectedActivities;
        existing.AffectedMilestones = order.AffectedMilestones;
        existing.CostImpact = order.CostImpact;
        existing.ScheduleImpactDays = order.ScheduleImpactDays;
        existing.ForecastImpact = order.ForecastImpact;
        existing.EvmImpact = order.EvmImpact;
        existing.Approvers = order.Approvers;
        existing.Comment = order.Comment;

        existing.RowVersion = Guid.NewGuid().ToString("N");
        existing.ModifiedUtc = DateTime.UtcNow;
        existing.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ChangeOrder", existing.Id.Value, "EDITAR", actor, "Edición de orden de cambio", ct: ct);

        return existing;
    }

    public async Task<ChangeOrder> SubmitChangeOrderForReviewAsync(ChangeOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditChangeOrderDraft(_userPermissions.CurrentRole), "SubmitChangeOrder");

        var order = await _changeOrderRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(order.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        ChangeOrderStatusTransitions.EnsureCanTransition(order.Status, ChangeOrderStatus.PendingReview);

        order.Status = ChangeOrderStatus.PendingReview;
        order.RowVersion = Guid.NewGuid().ToString("N");
        order.ModifiedUtc = DateTime.UtcNow;
        order.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(order, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "ENVIAR_REVISION", actor, "Enviada a revisión", ct: ct);

        return order;
    }

    public async Task<ChangeOrder> NegotiateChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanNegotiateChangeOrder(_userPermissions.CurrentRole), "NegotiateChangeOrder");

        var order = await _changeOrderRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(order.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        ChangeOrderStatusTransitions.EnsureCanTransition(order.Status, ChangeOrderStatus.UnderNegotiation);

        order.Status = ChangeOrderStatus.UnderNegotiation;
        order.Comment = comment;
        order.RowVersion = Guid.NewGuid().ToString("N");
        order.ModifiedUtc = DateTime.UtcNow;
        order.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(order, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "NEGOCIAR", actor, $"Negociación: {comment}", ct: ct);

        return order;
    }

    public async Task<ChangeOrder> ApproveChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApproveChangeOrder(_userPermissions.CurrentRole), "ApproveChangeOrder");

        var order = await _changeOrderRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(order.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        ChangeOrderStatusTransitions.EnsureCanTransition(order.Status, ChangeOrderStatus.Approved);

        if (_sod.CreatorCannotApprove && string.Equals(order.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SoD: El creador de la orden no puede aprobarla.");

        order.Status = ChangeOrderStatus.Approved;
        order.Comment = comment;
        order.RowVersion = Guid.NewGuid().ToString("N");
        order.ModifiedUtc = DateTime.UtcNow;
        order.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(order, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "APROBAR", actor, $"Aprobación: {comment}", ct: ct);

        return order;
    }

    public async Task<ChangeOrder> RejectChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanRejectChangeOrder(_userPermissions.CurrentRole), "RejectChangeOrder");

        var order = await _changeOrderRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(order.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        ChangeOrderStatusTransitions.EnsureCanTransition(order.Status, ChangeOrderStatus.Rejected);

        order.Status = ChangeOrderStatus.Rejected;
        order.Comment = comment;
        order.RowVersion = Guid.NewGuid().ToString("N");
        order.ModifiedUtc = DateTime.UtcNow;
        order.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(order, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "RECHAZAR", actor, $"Rechazo: {comment}", ct: ct);

        return order;
    }

    public async Task<ChangeOrder> ImplementChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanImplementChangeOrder(_userPermissions.CurrentRole), "ImplementChangeOrder");

        var order = await _changeOrderRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(order.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        ChangeOrderStatusTransitions.EnsureCanTransition(order.Status, ChangeOrderStatus.Implemented);

        order.Status = ChangeOrderStatus.Implemented;
        order.RowVersion = Guid.NewGuid().ToString("N");
        order.ModifiedUtc = DateTime.UtcNow;
        order.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(order, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "IMPLEMENTAR", actor, "Orden de cambio implementada", ct: ct);

        return order;
    }

    public async Task<ChangeOrder> CloseChangeOrderAsync(ChangeOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanImplementChangeOrder(_userPermissions.CurrentRole), "CloseChangeOrder");

        var order = await _changeOrderRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        if (!string.Equals(order.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La orden ha sido modificada por otro usuario.");

        ChangeOrderStatusTransitions.EnsureCanTransition(order.Status, ChangeOrderStatus.Closed);

        order.Status = ChangeOrderStatus.Closed;
        order.RowVersion = Guid.NewGuid().ToString("N");
        order.ModifiedUtc = DateTime.UtcNow;
        order.ModifiedBy = actor;

        await _changeOrderRepo.SaveAsync(order, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", id.Value, "CERRAR", actor, "Orden de cambio cerrada", ct: ct);

        return order;
    }

    public async Task<ChangeOrder?> GetChangeOrderAsync(ChangeOrderId id, CancellationToken ct = default)
    {
        return await _changeOrderRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<ChangeOrder>> SearchChangeOrdersAsync(string projectPath, string? text, ChangeOrderStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return await _changeOrderRepo.SearchAsync(projectPath, text, status, skip, take, ct);
    }

    // ── ChangeOrder Versions & Snapshots ─────────────────────────

    public async Task<ChangeOrderVersion> CreateChangeOrderVersionAsync(ChangeOrderId orderId, string actor, string reason, CancellationToken ct = default)
    {
        var order = await _changeOrderRepo.GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        var versions = await _changeOrderRepo.GetVersionsAsync(orderId, ct);
        var nextVersionNum = (versions.Count > 0 ? versions.Max(v => v.VersionNumber) : 0) + 1;

        var snapshotJson = JsonSerializer.Serialize(order);
        var version = new ChangeOrderVersion
        {
            Id = ChangeOrderVersionId.New(),
            ChangeOrderId = orderId,
            VersionNumber = nextVersionNum,
            SnapshotJson = snapshotJson,
            HashSha256 = ComputeHash(snapshotJson),
            ChangedBy = actor,
            ChangeReason = reason,
            CreatedUtc = DateTime.UtcNow
        };

        await _changeOrderRepo.SaveVersionAsync(version, ct);
        await RecordAuditAsync(order.ContractId, "ChangeOrder", orderId.Value, "VERSIONAR", actor, $"Nueva versión {nextVersionNum}: {reason}", ct: ct);

        return version;
    }

    public async Task<List<ChangeOrderVersion>> GetChangeOrderVersionsAsync(ChangeOrderId orderId, CancellationToken ct = default)
    {
        return await _changeOrderRepo.GetVersionsAsync(orderId, ct);
    }

    public async Task<ChangeOrderVersion> VerifyChangeOrderIntegrityAsync(ChangeOrderVersionId versionId, CancellationToken ct = default)
    {
        throw new NotImplementedException("Use GetChangeOrderVersionsAsync to load versions, then verify the hash manually.");
    }

    public async Task<ChangeOrderSnapshot> CreateChangeOrderSnapshotAsync(ChangeOrderId orderId, ContractId contractId, string projectPath, string versionLabel, CancellationToken ct = default)
    {
        var order = await _changeOrderRepo.GetByIdAsync(orderId, ct)
            ?? throw new KeyNotFoundException("Orden de cambio no encontrada.");

        var snapshotJson = JsonSerializer.Serialize(order);
        var snapshot = new ChangeOrderSnapshot
        {
            Id = ChangeOrderSnapshotId.New(),
            ChangeOrderId = orderId,
            ContractId = contractId,
            ProjectPath = projectPath,
            AsOfUtc = DateTime.UtcNow,
            SnapshotJson = snapshotJson,
            Hash = ComputeHash(snapshotJson),
            VersionLabel = versionLabel
        };

        await _changeOrderRepo.SaveSnapshotAsync(snapshot, ct);
        await RecordAuditAsync(contractId, "ChangeOrder", orderId.Value, "SNAPSHOT", "system", $"Snapshot creado: {versionLabel}", ct: ct);

        return snapshot;
    }

    public async Task<List<ChangeOrderSnapshot>> GetChangeOrderSnapshotsAsync(ChangeOrderId orderId, CancellationToken ct = default)
    {
        return await _changeOrderRepo.GetSnapshotsAsync(orderId, ct);
    }

    // ── ChangeOrder Lines ────────────────────────────────────────

    public async Task SetChangeOrderLinesAsync(ChangeOrderId orderId, IEnumerable<ChangeOrderLine> lines, CancellationToken ct = default)
    {
        var lineList = lines.ToList();
        await _changeOrderLineRepo.DeleteByChangeOrderAsync(orderId, ct);
        await _changeOrderLineRepo.SaveRangeAsync(lineList, ct);
    }

    public async Task<List<ChangeOrderLine>> GetChangeOrderLinesAsync(ChangeOrderId orderId, CancellationToken ct = default)
    {
        return await _changeOrderLineRepo.GetByChangeOrderAsync(orderId, ct);
    }
}
