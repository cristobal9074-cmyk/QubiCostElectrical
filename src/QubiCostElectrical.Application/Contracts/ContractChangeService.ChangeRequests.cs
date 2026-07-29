using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ChangeRequest
// ──────────────────────────────────────────────────────────────
partial class ContractChangeService
{
    public async Task<ChangeRequest> CreateChangeRequestAsync(ChangeRequest draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateChangeRequest(_userPermissions.CurrentRole), "CreateChangeRequest");

        var id = ChangeRequestId.New();
        var nextNum = await _changeRequestRepo.GetNextNumberAsync(draft.ContractId.Value, ct);
        var visibleNum = string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"CR-{nextNum:D5}" : draft.VisibleNumber;

        var request = new ChangeRequest
        {
            Id = id,
            ContractId = draft.ContractId,
            VisibleNumber = visibleNum,
            Source = draft.Source,
            Title = draft.Title,
            Description = draft.Description,
            Cause = draft.Cause,
            Requester = draft.Requester,
            RequestDate = draft.RequestDate == default ? DateTime.UtcNow : draft.RequestDate,
            Urgency = draft.Urgency,
            WbsCode = draft.WbsCode,
            AffectedActivities = draft.AffectedActivities,
            AffectedItems = draft.AffectedItems,
            SupportingDocuments = draft.SupportingDocuments,
            PreliminaryCostImpact = draft.PreliminaryCostImpact,
            PreliminaryDaysImpact = draft.PreliminaryDaysImpact,
            CurrencyCode = draft.CurrencyCode,
            Status = ChangeRequestStatus.Draft,
            Responsible = draft.Responsible,
            Comment = draft.Comment,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor
        };

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "CREAR", actor, "Creación de solicitud de cambio", ct: ct);

        return request;
    }

    public async Task<ChangeRequest> UpdateChangeRequestDraftAsync(ChangeRequest request, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditChangeRequest(_userPermissions.CurrentRole), "EditChangeRequest");

        var existing = await _changeRequestRepo.GetByIdAsync(request.Id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is ChangeRequestStatus.Approved or ChangeRequestStatus.ConvertedToChangeOrder or ChangeRequestStatus.Closed)
            throw new InvalidOperationException("No se puede editar una solicitud de cambio aprobada, convertida o cerrada.");

        existing.Title = request.Title;
        existing.Description = request.Description;
        existing.Cause = request.Cause;
        existing.Requester = request.Requester;
        existing.RequestDate = request.RequestDate;
        existing.Urgency = request.Urgency;
        existing.WbsCode = request.WbsCode;
        existing.AffectedActivities = request.AffectedActivities;
        existing.AffectedItems = request.AffectedItems;
        existing.SupportingDocuments = request.SupportingDocuments;
        existing.PreliminaryCostImpact = request.PreliminaryCostImpact;
        existing.PreliminaryDaysImpact = request.PreliminaryDaysImpact;
        existing.CurrencyCode = request.CurrencyCode;
        existing.Responsible = request.Responsible;
        existing.Comment = request.Comment;

        existing.RowVersion = Guid.NewGuid().ToString("N");
        existing.ModifiedUtc = DateTime.UtcNow;
        existing.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ChangeRequest", existing.Id.Value, "EDITAR", actor, "Edición de solicitud de cambio", ct: ct);

        return existing;
    }

    public async Task<ChangeRequest> SubmitChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanSubmitChangeRequest(_userPermissions.CurrentRole), "SubmitChangeRequest");

        var request = await _changeRequestRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(request.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        ChangeRequestStatusTransitions.EnsureCanTransition(request.Status, ChangeRequestStatus.Submitted);

        request.Status = ChangeRequestStatus.Submitted;
        request.RowVersion = Guid.NewGuid().ToString("N");
        request.ModifiedUtc = DateTime.UtcNow;
        request.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "ENVIAR", actor, "Solicitud de cambio enviada", ct: ct);

        return request;
    }

    public async Task<ChangeRequest> AnalyzeChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanAnalyzeChangeRequest(_userPermissions.CurrentRole), "AnalyzeChangeRequest");

        var request = await _changeRequestRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(request.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        ChangeRequestStatusTransitions.EnsureCanTransition(request.Status, ChangeRequestStatus.UnderAnalysis);

        request.Status = ChangeRequestStatus.UnderAnalysis;
        request.Comment = comment;
        request.RowVersion = Guid.NewGuid().ToString("N");
        request.ModifiedUtc = DateTime.UtcNow;
        request.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "ANALIZAR", actor, $"Análisis: {comment}", ct: ct);

        return request;
    }

    public async Task<ChangeRequest> ApproveChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanAnalyzeChangeRequest(_userPermissions.CurrentRole), "ApproveChangeRequest");

        var request = await _changeRequestRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(request.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        ChangeRequestStatusTransitions.EnsureCanTransition(request.Status, ChangeRequestStatus.Approved);

        if (_sod.CreatorCannotApprove && string.Equals(request.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SoD: El creador de la solicitud no puede aprobarla.");

        request.Status = ChangeRequestStatus.Approved;
        request.Comment = comment;
        request.RowVersion = Guid.NewGuid().ToString("N");
        request.ModifiedUtc = DateTime.UtcNow;
        request.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "APROBAR", actor, $"Aprobación: {comment}", ct: ct);

        return request;
    }

    public async Task<ChangeRequest> RejectChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanAnalyzeChangeRequest(_userPermissions.CurrentRole), "RejectChangeRequest");

        var request = await _changeRequestRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(request.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        ChangeRequestStatusTransitions.EnsureCanTransition(request.Status, ChangeRequestStatus.Rejected);

        request.Status = ChangeRequestStatus.Rejected;
        request.Comment = comment;
        request.RowVersion = Guid.NewGuid().ToString("N");
        request.ModifiedUtc = DateTime.UtcNow;
        request.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "RECHAZAR", actor, $"Rechazo: {comment}", ct: ct);

        return request;
    }

    public async Task<ChangeRequest> CancelChangeRequestAsync(ChangeRequestId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditChangeRequest(_userPermissions.CurrentRole), "CancelChangeRequest");

        var request = await _changeRequestRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(request.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        ChangeRequestStatusTransitions.EnsureCanTransition(request.Status, ChangeRequestStatus.Cancelled);

        request.Status = ChangeRequestStatus.Cancelled;
        request.Comment = comment;
        request.RowVersion = Guid.NewGuid().ToString("N");
        request.ModifiedUtc = DateTime.UtcNow;
        request.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "CANCELAR", actor, $"Cancelación: {comment}", ct: ct);

        return request;
    }

    public async Task<ChangeRequest> ConvertToChangeOrderAsync(ChangeRequestId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateChangeOrder(_userPermissions.CurrentRole), "ConvertToChangeOrder");

        var request = await _changeRequestRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Solicitud de cambio no encontrada.");

        if (!string.Equals(request.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La solicitud ha sido modificada por otro usuario.");

        ChangeRequestStatusTransitions.EnsureCanTransition(request.Status, ChangeRequestStatus.ConvertedToChangeOrder);

        request.Status = ChangeRequestStatus.ConvertedToChangeOrder;
        request.RowVersion = Guid.NewGuid().ToString("N");
        request.ModifiedUtc = DateTime.UtcNow;
        request.ModifiedBy = actor;

        await _changeRequestRepo.SaveAsync(request, ct);
        await RecordAuditAsync(request.ContractId, "ChangeRequest", id.Value, "CONVERTIR_OC", actor, "Convertida a orden de cambio", ct: ct);

        return request;
    }

    public async Task<ChangeRequest?> GetChangeRequestAsync(ChangeRequestId id, CancellationToken ct = default)
    {
        return await _changeRequestRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<ChangeRequest>> SearchChangeRequestsAsync(string projectPath, string? text, ChangeRequestStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return await _changeRequestRepo.SearchAsync(projectPath, text, status, skip, take, ct);
    }
}
