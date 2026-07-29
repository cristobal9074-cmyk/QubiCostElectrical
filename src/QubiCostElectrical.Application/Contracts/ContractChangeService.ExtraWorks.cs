using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ExtraWork
// ──────────────────────────────────────────────────────────────
partial class ContractChangeService
{
    public async Task<ExtraWork> CreateExtraWorkAsync(ExtraWork draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "CreateExtraWork");

        var id = ExtraWorkId.New();
        var nextNum = await _extraWorkRepo.GetNextNumberAsync(draft.ContractId.Value, ct);
        var visibleNum = string.IsNullOrWhiteSpace(draft.VisibleNumber) ? $"EW-{nextNum:D5}" : draft.VisibleNumber;

        var extraWork = new ExtraWork
        {
            Id = id,
            ContractId = draft.ContractId,
            ChangeRequestId = draft.ChangeRequestId,
            ChangeOrderId = draft.ChangeOrderId,
            VisibleNumber = visibleNum,
            Instruction = draft.Instruction,
            Description = draft.Description,
            Origin = draft.Origin,
            InstructionDate = draft.InstructionDate == default ? DateTime.UtcNow : draft.InstructionDate,
            WbsCode = draft.WbsCode,
            ActivityCode = draft.ActivityCode,
            Resources = draft.Resources,
            Materials = draft.Materials,
            Labor = draft.Labor,
            Equipment = draft.Equipment,
            Quantities = draft.Quantities,
            Hours = draft.Hours,
            EstimatedCost = draft.EstimatedCost,
            FinalCost = draft.FinalCost,
            CurrencyCode = draft.CurrencyCode,
            Evidence = draft.Evidence,
            Responsible = draft.Responsible,
            Status = ExtraWorkStatus.Draft,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor
        };

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "CREAR", actor, "Creación de obra extraordinaria", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> UpdateExtraWorkDraftAsync(ExtraWork extraWork, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "EditExtraWork");

        var existing = await _extraWorkRepo.GetByIdAsync(extraWork.Id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is ExtraWorkStatus.Approved or ExtraWorkStatus.Valued or ExtraWorkStatus.Closed)
            throw new InvalidOperationException("No se puede editar una obra extraordinaria aprobada, valorada o cerrada.");

        existing.Instruction = extraWork.Instruction;
        existing.Description = extraWork.Description;
        existing.Origin = extraWork.Origin;
        existing.InstructionDate = extraWork.InstructionDate;
        existing.WbsCode = extraWork.WbsCode;
        existing.ActivityCode = extraWork.ActivityCode;
        existing.Resources = extraWork.Resources;
        existing.Materials = extraWork.Materials;
        existing.Labor = extraWork.Labor;
        existing.Equipment = extraWork.Equipment;
        existing.Quantities = extraWork.Quantities;
        existing.Hours = extraWork.Hours;
        existing.EstimatedCost = extraWork.EstimatedCost;
        existing.FinalCost = extraWork.FinalCost;
        existing.CurrencyCode = extraWork.CurrencyCode;
        existing.Evidence = extraWork.Evidence;
        existing.Responsible = extraWork.Responsible;

        existing.RowVersion = Guid.NewGuid().ToString("N");
        existing.ModifiedUtc = DateTime.UtcNow;
        existing.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ExtraWork", existing.Id.Value, "EDITAR", actor, "Edición de obra extraordinaria", ct: ct);

        return existing;
    }

    public async Task<ExtraWork> InstructExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, string instruction, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "InstructExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.Instructed);

        extraWork.Status = ExtraWorkStatus.Instructed;
        extraWork.Instruction = instruction;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "INSTRUIR", actor, $"Instrucción: {instruction}", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> ExecuteExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "ExecuteExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.InExecution);

        extraWork.Status = ExtraWorkStatus.InExecution;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "EJECUTAR", actor, "Obra extraordinaria en ejecución", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> SubmitExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "SubmitExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.Submitted);

        extraWork.Status = ExtraWorkStatus.Submitted;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "ENVIAR", actor, "Obra extraordinaria enviada", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> ApproveExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "ApproveExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.Approved);

        if (_sod.CreatorCannotApprove && string.Equals(extraWork.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SoD: El creador de la obra extraordinaria no puede aprobarla.");

        extraWork.Status = ExtraWorkStatus.Approved;
        extraWork.ApprovedBy = actor;
        extraWork.ApprovalDate = DateTime.UtcNow;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "APROBAR", actor, "Obra extraordinaria aprobada", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> RejectExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, string comment, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "RejectExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.Rejected);

        extraWork.Status = ExtraWorkStatus.Rejected;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "RECHAZAR", actor, $"Rechazo: {comment}", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> ValueExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, decimal valuationAmount, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "ValueExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.Valued);

        extraWork.Status = ExtraWorkStatus.Valued;
        extraWork.ValuationAmount = valuationAmount;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "VALORAR", actor, $"Valoración: {valuationAmount:C}", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork> CloseExtraWorkAsync(ExtraWorkId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanManageExtraWork(_userPermissions.CurrentRole), "CloseExtraWork");

        var extraWork = await _extraWorkRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Obra extraordinaria no encontrada.");

        if (!string.Equals(extraWork.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La obra extraordinaria ha sido modificada por otro usuario.");

        ExtraWorkStatusTransitions.EnsureCanTransition(extraWork.Status, ExtraWorkStatus.Closed);

        extraWork.Status = ExtraWorkStatus.Closed;
        extraWork.RowVersion = Guid.NewGuid().ToString("N");
        extraWork.ModifiedUtc = DateTime.UtcNow;
        extraWork.ModifiedBy = actor;

        await _extraWorkRepo.SaveAsync(extraWork, ct);
        await RecordAuditAsync(extraWork.ContractId, "ExtraWork", id.Value, "CERRAR", actor, "Obra extraordinaria cerrada", ct: ct);

        return extraWork;
    }

    public async Task<ExtraWork?> GetExtraWorkAsync(ExtraWorkId id, CancellationToken ct = default)
    {
        return await _extraWorkRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<ExtraWork>> SearchExtraWorksAsync(string projectPath, string? text, ExtraWorkStatus? status, int skip, int take, CancellationToken ct = default)
    {
        return await _extraWorkRepo.SearchAsync(projectPath, text, status, skip, take, ct);
    }
}
