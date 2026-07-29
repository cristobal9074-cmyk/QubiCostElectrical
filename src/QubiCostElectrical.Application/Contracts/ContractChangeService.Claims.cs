using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// Claim
// ──────────────────────────────────────────────────────────────
partial class ContractChangeService
{
    public async Task<ContractClaim> CreateClaimAsync(ContractClaim draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateClaim(_userPermissions.CurrentRole), "CreateClaim");

        var id = ClaimId.New();
        var nextNum = await _claimRepo.GetNextNumberAsync(draft.ContractId.Value, ct);
        var visibleNum = string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"CL-{nextNum:D5}" : draft.VisibleNumber;

        var claim = new ContractClaim
        {
            Id = id,
            ContractId = draft.ContractId,
            VisibleNumber = visibleNum,
            Type = draft.Type,
            EventDescription = draft.EventDescription,
            EventDate = draft.EventDate,
            NotificationDate = draft.NotificationDate,
            Cause = draft.Cause,
            Description = draft.Description,
            AmountClaimed = draft.AmountClaimed,
            DaysClaimed = draft.DaysClaimed,
            CurrencyCode = draft.CurrencyCode,
            AffectedActivities = draft.AffectedActivities,
            WbsCode = draft.WbsCode,
            SupportingDocuments = draft.SupportingDocuments,
            Responsible = draft.Responsible,
            Status = ClaimStatus.Draft,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor
        };

        await _claimRepo.SaveAsync(claim, ct);
        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "CREAR", actor, "Creación de reclamo", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> UpdateClaimDraftAsync(ContractClaim claim, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateClaim(_userPermissions.CurrentRole), "EditClaim");

        var existing = await _claimRepo.GetByIdAsync(claim.Id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is ClaimStatus.Accepted or ClaimStatus.Closed)
            throw new InvalidOperationException("No se puede editar un reclamo aceptado o cerrado.");

        existing.Type = claim.Type;
        existing.EventDescription = claim.EventDescription;
        existing.EventDate = claim.EventDate;
        existing.NotificationDate = claim.NotificationDate;
        existing.Cause = claim.Cause;
        existing.Description = claim.Description;
        existing.AmountClaimed = claim.AmountClaimed;
        existing.DaysClaimed = claim.DaysClaimed;
        existing.CurrencyCode = claim.CurrencyCode;
        existing.AffectedActivities = claim.AffectedActivities;
        existing.WbsCode = claim.WbsCode;
        existing.SupportingDocuments = claim.SupportingDocuments;
        existing.Responsible = claim.Responsible;

        existing.RowVersion = Guid.NewGuid().ToString("N");
        existing.ModifiedUtc = DateTime.UtcNow;
        existing.ModifiedBy = actor;

        await _claimRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "Claim", existing.Id.Value, "EDITAR", actor, "Edición de reclamo", ct: ct);

        return existing;
    }

    public async Task<ContractClaim> NotifyClaimAsync(ClaimId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanSubmitClaim(_userPermissions.CurrentRole), "NotifyClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.Notified);

        claim.Status = ClaimStatus.Notified;
        claim.NotificationDate = DateTime.UtcNow;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        await _claimRepo.SaveAsync(claim, ct);
        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "NOTIFICAR", actor, "Reclamo notificado", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> SubmitClaimAsync(ClaimId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanSubmitClaim(_userPermissions.CurrentRole), "SubmitClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.Submitted);

        claim.Status = ClaimStatus.Submitted;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        await _claimRepo.SaveAsync(claim, ct);
        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "ENVIAR", actor, "Reclamo enviado", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> EvaluateClaimAsync(ClaimId id, string expectedRowVersion, string actor, decimal evaluatedAmount, int evaluatedDays, string justification, string recommendation, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEvaluateClaim(_userPermissions.CurrentRole), "EvaluateClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.UnderReview);

        claim.Status = ClaimStatus.UnderReview;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        var evaluation = new ClaimEvaluation
        {
            Id = ClaimEvaluationId.New(),
            ClaimId = id,
            Evaluator = actor,
            EvaluationDate = DateTime.UtcNow,
            EvaluatedAmount = evaluatedAmount,
            EvaluatedDays = evaluatedDays,
            Justification = justification,
            Recommendation = recommendation
        };

        await _claimRepo.SaveEvaluationAsync(evaluation, ct);
        await _claimRepo.SaveAsync(claim, ct);

        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "EVALUAR", actor,
            $"Evaluación: monto={evaluatedAmount:C}, días={evaluatedDays}, recomendación={recommendation}", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> RespondToClaimAsync(ClaimId id, string expectedRowVersion, string actor, string responseType, string responseDetail, decimal offeredAmount, int offeredDays, CancellationToken ct = default)
    {
        Ensure(_permissions.CanRespondClaim(_userPermissions.CurrentRole), "RespondClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        var response = new ClaimResponse
        {
            ClaimId = id,
            Responder = actor,
            ResponseDate = DateTime.UtcNow,
            ResponseType = responseType,
            ResponseDetail = responseDetail,
            OfferedAmount = offeredAmount,
            OfferedDays = offeredDays,
            Comment = responseDetail
        };

        ClaimStatus newStatus = responseType.ToUpperInvariant() switch
        {
            "ACCEPTED" => ClaimStatus.Accepted,
            "PARTIALLYACCEPTED" or "PARTIALLY ACCEPTED" => ClaimStatus.PartiallyAccepted,
            "REJECTED" => ClaimStatus.Rejected,
            _ => claim.Status
        };

        if (newStatus != claim.Status)
        {
            ClaimStatusTransitions.EnsureCanTransition(claim.Status, newStatus);
            claim.Status = newStatus;
        }
        else
        {
            ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.Negotiation);
            claim.Status = ClaimStatus.Negotiation;
        }

        claim.Response = responseDetail;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        await _claimRepo.SaveResponseAsync(response, ct);
        await _claimRepo.SaveAsync(claim, ct);

        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "RESPONDER", actor,
            $"Respuesta: {responseType}, oferta={offeredAmount:C}, días={offeredDays}", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> ApproveClaimAsync(ClaimId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApproveClaim(_userPermissions.CurrentRole), "ApproveClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.Accepted);

        if (_sod.CreatorCannotApprove && string.Equals(claim.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SoD: El creador del reclamo no puede aprobarlo.");

        claim.Status = ClaimStatus.Accepted;
        claim.Response = comment;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        await _claimRepo.SaveAsync(claim, ct);
        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "ACEPTAR", actor, $"Aceptación: {comment}", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> WithdrawClaimAsync(ClaimId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(_permissions.CanSubmitClaim(_userPermissions.CurrentRole), "WithdrawClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.Withdrawn);

        claim.Status = ClaimStatus.Withdrawn;
        claim.Response = reason;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        await _claimRepo.SaveAsync(claim, ct);
        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "RETIRAR", actor, $"Retiro: {reason}", ct: ct);

        return claim;
    }

    public async Task<ContractClaim> CloseClaimAsync(ClaimId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApproveClaim(_userPermissions.CurrentRole), "CloseClaim");

        var claim = await _claimRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reclamo no encontrado.");

        if (!string.Equals(claim.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reclamo ha sido modificado por otro usuario.");

        ClaimStatusTransitions.EnsureCanTransition(claim.Status, ClaimStatus.Closed);

        claim.Status = ClaimStatus.Closed;
        claim.RowVersion = Guid.NewGuid().ToString("N");
        claim.ModifiedUtc = DateTime.UtcNow;
        claim.ModifiedBy = actor;

        await _claimRepo.SaveAsync(claim, ct);
        await RecordAuditAsync(claim.ContractId, "Claim", id.Value, "CERRAR", actor, "Reclamo cerrado", ct: ct);

        return claim;
    }

    public async Task<ContractClaim?> GetClaimAsync(ClaimId id, CancellationToken ct = default)
    {
        return await _claimRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<ContractClaim>> SearchClaimsAsync(string projectPath, string? text, ClaimStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return await _claimRepo.SearchAsync(projectPath, text, status, skip, take, ct);
    }

    // ── Claim Sub-entities ───────────────────────────────────────

    public async Task<ClaimEvent> AddClaimEventAsync(ClaimId claimId, string eventType, string description, string actor, CancellationToken ct = default)
    {
        var claimEvent = new ClaimEvent
        {
            Id = ClaimEventId.New(),
            ClaimId = claimId,
            EventType = eventType,
            Description = description,
            EventDate = DateTime.UtcNow,
            Actor = actor,
            DetailsJson = System.Text.Json.JsonSerializer.Serialize(new { EventType = eventType, Description = description })
        };

        await _claimRepo.SaveEventAsync(claimEvent, ct);
        return claimEvent;
    }

    public async Task<List<ClaimEvent>> GetClaimEventsAsync(ClaimId claimId, CancellationToken ct = default)
    {
        return await _claimRepo.GetEventsAsync(claimId, ct);
    }

    public async Task<ClaimEvidence> AddClaimEvidenceAsync(ClaimId claimId, string documentReference, string description, string fileName, string uploadedBy, CancellationToken ct = default)
    {
        var evidence = new ClaimEvidence
        {
            Id = ClaimEvidenceId.New(),
            ClaimId = claimId,
            DocumentReference = documentReference,
            Description = description,
            FileName = fileName,
            UploadedUtc = DateTime.UtcNow,
            UploadedBy = uploadedBy
        };

        await _claimRepo.SaveEvidenceAsync(evidence, ct);
        return evidence;
    }

    public async Task<List<ClaimEvidence>> GetClaimEvidenceAsync(ClaimId claimId, CancellationToken ct = default)
    {
        return await _claimRepo.GetEvidenceAsync(claimId, ct);
    }

    public async Task<ClaimEvaluation?> GetClaimEvaluationAsync(ClaimId claimId, CancellationToken ct = default)
    {
        return await _claimRepo.GetEvaluationAsync(claimId, ct);
    }

    public async Task<ClaimResponse?> GetClaimResponseAsync(ClaimId claimId, CancellationToken ct = default)
    {
        return await _claimRepo.GetResponseAsync(claimId, ct);
    }
}
