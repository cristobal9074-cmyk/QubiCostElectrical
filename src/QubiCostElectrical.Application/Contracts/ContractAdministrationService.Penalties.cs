using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ContractPenalty operations
// ──────────────────────────────────────────────────────────────
partial class ContractAdministrationService
{
    public async Task<ContractPenalty> CreatePenaltyAsync(ContractPenalty draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreatePenalty(_userPermissions.CurrentRole), "CreatePenalty");

        var id = PenaltyId.New();
        var nextNum = await _penaltyRepo.GetNextNumberAsync(draft.ProjectPath, ct);

        var penalty = new ContractPenalty
        {
            Id = id,
            ContractId = draft.ContractId,
            VisibleNumber = nextNum,
            PenaltyType = draft.PenaltyType,
            Cause = draft.Cause,
            Date = draft.Date == default ? DateTime.UtcNow : draft.Date,
            Amount = draft.Amount,
            Percentage = draft.Percentage,
            CurrencyCode = draft.CurrencyCode,
            Status = PenaltyStatus.Draft,
            Observations = draft.Observations,
            Responsible = draft.Responsible,
            ProjectPath = draft.ProjectPath,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };

        await _penaltyRepo.SaveAsync(penalty, ct);
        await RecordAuditAsync(penalty.ContractId, "ContractPenalty", id.Value, "CREAR", actor, "Creación de multa", ct: ct);

        return penalty;
    }

    public async Task<ContractPenalty> UpdatePenaltyDraftAsync(ContractPenalty penalty, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditPenalty(_userPermissions.CurrentRole), "EditPenalty");

        var existing = await LoadPenaltyWritable(penalty.Id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is PenaltyStatus.Approved or PenaltyStatus.Applied or PenaltyStatus.Closed)
            throw new InvalidOperationException("No se puede editar una multa aprobada, aplicada o cerrada.");

        existing.PenaltyType = penalty.PenaltyType;
        existing.Cause = penalty.Cause;
        existing.Date = penalty.Date;
        existing.Amount = penalty.Amount;
        existing.Percentage = penalty.Percentage;
        existing.CurrencyCode = penalty.CurrencyCode;
        existing.Observations = penalty.Observations;
        existing.Responsible = penalty.Responsible;
        existing.Comment = penalty.Comment;

        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", existing.Id.Value, "EDITAR", actor, "Edición de multa", ct: ct);

        return existing;
    }

    public async Task<ContractPenalty> ProposePenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditPenalty(_userPermissions.CurrentRole), "ProposePenalty");

        var existing = await LoadPenaltyWritable(id, expectedRowVersion, ct);
        PenaltyStatusTransitions.EnsureCanTransition(existing.Status, PenaltyStatus.Proposed);

        existing.Status = PenaltyStatus.Proposed;
        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", id.Value, "PROPONER", actor, "Multa propuesta", ct: ct);

        return existing;
    }

    public async Task<ContractPenalty> ReviewPenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditPenalty(_userPermissions.CurrentRole), "ReviewPenalty");

        var existing = await LoadPenaltyWritable(id, expectedRowVersion, ct);
        PenaltyStatusTransitions.EnsureCanTransition(existing.Status, PenaltyStatus.UnderReview);

        existing.Status = PenaltyStatus.UnderReview;
        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", id.Value, "REVISAR", actor, "Multa en revisión", ct: ct);

        return existing;
    }

    public async Task<ContractPenalty> ApprovePenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApprovePenalty(_userPermissions.CurrentRole), "ApprovePenalty");

        var existing = await LoadPenaltyWritable(id, expectedRowVersion, ct);
        PenaltyStatusTransitions.EnsureCanTransition(existing.Status, PenaltyStatus.Approved);

        if (_sod.CreatorCannotApprove && string.Equals(existing.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("SoD: El creador de la multa no puede aprobarla.");

        existing.Status = PenaltyStatus.Approved;
        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", id.Value, "APROBAR", actor, "Multa aprobada", ct: ct);

        return existing;
    }

    public async Task<ContractPenalty> RejectPenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditPenalty(_userPermissions.CurrentRole), "RejectPenalty");

        var existing = await LoadPenaltyWritable(id, expectedRowVersion, ct);
        PenaltyStatusTransitions.EnsureCanTransition(existing.Status, PenaltyStatus.Rejected);

        existing.Status = PenaltyStatus.Rejected;
        existing.Comment = reason;
        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", id.Value, "RECHAZAR", actor, $"Multa rechazada: {reason}", ct: ct);

        return existing;
    }

    public async Task<ContractPenalty> ApplyPenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApprovePenalty(_userPermissions.CurrentRole), "ApplyPenalty");

        var existing = await LoadPenaltyWritable(id, expectedRowVersion, ct);
        PenaltyStatusTransitions.EnsureCanTransition(existing.Status, PenaltyStatus.Applied);

        existing.Status = PenaltyStatus.Applied;
        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", id.Value, "APLICAR", actor, "Multa aplicada al contrato", ct: ct);

        return existing;
    }

    public async Task<ContractPenalty> ClosePenaltyAsync(PenaltyId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanViewPenalty(_userPermissions.CurrentRole), "ClosePenalty");

        var existing = await LoadPenaltyWritable(id, expectedRowVersion, ct);
        PenaltyStatusTransitions.EnsureCanTransition(existing.Status, PenaltyStatus.Closed);

        existing.Status = PenaltyStatus.Closed;
        StampRowVersion(existing, actor);

        await _penaltyRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractPenalty", id.Value, "CERRAR", actor, "Multa cerrada", ct: ct);

        return existing;
    }

    public Task<ContractPenalty?> GetPenaltyAsync(PenaltyId id, CancellationToken ct = default)
        => _penaltyRepo.GetByIdAsync(id, ct);

    public Task<List<ContractPenalty>> GetPenaltiesByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _penaltyRepo.GetByContractAsync(contractId, ct);
}
