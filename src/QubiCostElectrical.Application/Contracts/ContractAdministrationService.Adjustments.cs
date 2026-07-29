using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ContractAdjustment operations
// ──────────────────────────────────────────────────────────────
partial class ContractAdministrationService
{
    public async Task<ContractAdjustment> CreateAdjustmentAsync(ContractAdjustment draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateAdjustment(_userPermissions.CurrentRole), "CreateAdjustment");

        var id = AdjustmentId.New();
        var nextNum = await _adjustmentRepo.GetNextNumberAsync(draft.ProjectPath, ct);
        var visibleNum = nextNum;

        var adjustment = new ContractAdjustment
        {
            Id = id,
            ContractId = draft.ContractId,
            Period = draft.Period,
            BaseDate = draft.BaseDate,
            CalculationDate = draft.CalculationDate,
            AdjustmentType = draft.AdjustmentType,
            CurrencyCode = draft.CurrencyCode,
            BaseAmount = draft.BaseAmount,
            AdjustedAmount = draft.AdjustedAmount,
            Status = AdjustmentStatus.Draft,
            Description = draft.Description,
            Responsible = draft.Responsible,
            ProjectPath = draft.ProjectPath,
            VisibleNumber = visibleNum,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor
        };

        // Compute integrity hash
        var json = JsonSerializer.Serialize(adjustment);
        adjustment.Hash = ComputeHash(json);
        adjustment.RowVersion = Guid.NewGuid().ToString("N");

        await _adjustmentRepo.SaveAsync(adjustment, ct);
        await RecordAuditAsync(adjustment.ContractId, "ContractAdjustment", id.Value, "CREAR", actor, "Creación de reajuste contractual", ct: ct);

        return adjustment;
    }

    public async Task<ContractAdjustment> UpdateAdjustmentDraftAsync(ContractAdjustment adjustment, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditAdjustment(_userPermissions.CurrentRole), "EditAdjustment");

        var existing = await LoadAdjustmentWritable(adjustment.Id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is AdjustmentStatus.Applied or AdjustmentStatus.Closed)
            throw new InvalidOperationException("No se puede editar un reajuste aplicado o cerrado.");

        existing.Period = adjustment.Period;
        existing.BaseDate = adjustment.BaseDate;
        existing.CalculationDate = adjustment.CalculationDate;
        existing.AdjustmentType = adjustment.AdjustmentType;
        existing.CurrencyCode = adjustment.CurrencyCode;
        existing.BaseAmount = adjustment.BaseAmount;
        existing.AdjustedAmount = adjustment.AdjustedAmount;
        existing.Description = adjustment.Description;
        existing.Responsible = adjustment.Responsible;

        StampRowVersion(existing, actor);

        var json = JsonSerializer.Serialize(existing);
        existing.Hash = ComputeHash(json);

        await _adjustmentRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractAdjustment", existing.Id.Value, "EDITAR", actor, "Edición de reajuste contractual", ct: ct);

        return existing;
    }

    public async Task<ContractAdjustment> CalculateAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCalculateAdjustment(_userPermissions.CurrentRole), "CalculateAdjustment");

        var existing = await LoadAdjustmentWritable(id, expectedRowVersion, ct);

        AdjustmentStatusTransitions.EnsureCanTransition(existing.Status, AdjustmentStatus.Calculated);

        // Calculate based on type
        existing.AdjustedAmount = existing.AdjustmentType switch
        {
            AdjustmentType.IPC => existing.BaseAmount * 1.02m, // Simulated IPC
            AdjustmentType.UF => existing.BaseAmount * 1.015m, // Simulated UF
            AdjustmentType.FixedFactor => existing.BaseAmount * 1.01m, // Simulated fixed
            AdjustmentType.ManualJustified => existing.AdjustedAmount > 0 ? existing.AdjustedAmount : existing.BaseAmount,
            _ => existing.BaseAmount * 1.01m
        };

        existing.Status = AdjustmentStatus.Calculated;
        existing.CalculationDate = DateTime.UtcNow;

        StampRowVersion(existing, actor);

        var json = JsonSerializer.Serialize(existing);
        existing.Hash = ComputeHash(json);

        await _adjustmentRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractAdjustment", id.Value, "CALCULAR", actor, $"Reajuste calculado: {existing.AdjustmentType}", ct: ct);

        return existing;
    }

    public async Task<ContractAdjustment> ApplyAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApplyAdjustment(_userPermissions.CurrentRole), "ApplyAdjustment");

        var existing = await LoadAdjustmentWritable(id, expectedRowVersion, ct);

        AdjustmentStatusTransitions.EnsureCanTransition(existing.Status, AdjustmentStatus.Applied);

        if (_sod.CreatorCannotApprove && string.Equals(existing.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("SoD: El creador del reajuste no puede aplicarlo.");

        existing.Status = AdjustmentStatus.Applied;
        StampRowVersion(existing, actor);

        var json = JsonSerializer.Serialize(existing);
        existing.Hash = ComputeHash(json);

        await _adjustmentRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractAdjustment", id.Value, "APLICAR", actor, "Reajuste aplicado al contrato", ct: ct);

        return existing;
    }

    public async Task<ContractAdjustment> RejectAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApplyAdjustment(_userPermissions.CurrentRole), "RejectAdjustment");

        var existing = await LoadAdjustmentWritable(id, expectedRowVersion, ct);

        AdjustmentStatusTransitions.EnsureCanTransition(existing.Status, AdjustmentStatus.Rejected);

        existing.Status = AdjustmentStatus.Rejected;
        existing.Comment = reason;
        StampRowVersion(existing, actor);

        await _adjustmentRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractAdjustment", id.Value, "RECHAZAR", actor, $"Reajuste rechazado: {reason}", ct: ct);

        return existing;
    }

    public async Task<ContractAdjustment> CloseAdjustmentAsync(AdjustmentId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanViewAdjustment(_userPermissions.CurrentRole), "CloseAdjustment");

        var existing = await LoadAdjustmentWritable(id, expectedRowVersion, ct);

        AdjustmentStatusTransitions.EnsureCanTransition(existing.Status, AdjustmentStatus.Closed);

        existing.Status = AdjustmentStatus.Closed;
        StampRowVersion(existing, actor);

        await _adjustmentRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractAdjustment", id.Value, "CERRAR", actor, "Reajuste cerrado", ct: ct);

        return existing;
    }

    public Task<ContractAdjustment?> GetAdjustmentAsync(AdjustmentId id, CancellationToken ct = default)
        => _adjustmentRepo.GetByIdAsync(id, ct);

    public Task<List<ContractAdjustment>> GetAdjustmentsByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _adjustmentRepo.GetByContractAsync(contractId, ct);

    // ── Formulas ───────────────────────────────────────

    public async Task<AdjustmentFormula> SaveFormulaAsync(AdjustmentFormula formula, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateAdjustment(_userPermissions.CurrentRole), "SaveFormula");

        if (formula.Id is null)
        {
            formula = new AdjustmentFormula
            {
                Id = AdjustmentFormulaId.New(),
                ContractId = formula.ContractId,
                Name = formula.Name,
                Description = formula.Description,
                Components = formula.Components,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = actor
            };
        }
        else
        {
            formula.CreatedUtc = DateTime.UtcNow;
            formula.CreatedBy = actor;
        }

        await _formulaRepo.SaveAsync(formula, ct);
        return formula;
    }

    public Task<AdjustmentFormula?> GetFormulaAsync(AdjustmentFormulaId id, CancellationToken ct = default)
        => _formulaRepo.GetByIdAsync(id, ct);

    public Task<List<AdjustmentFormula>> GetFormulasByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _formulaRepo.GetByContractAsync(contractId, ct);

    public Task<decimal> CalculatePolynomialFormulaAsync(AdjustmentFormula formula, CancellationToken ct = default)
    {
        // Deterministic calculation: weighted sum of (currentIndex / baseIndex) for each component
        if (formula.Components == null || formula.Components.Count == 0)
            return Task.FromResult(1.0m);

        var totalWeight = formula.Components.Sum(c => c.Weight);
        if (totalWeight == 0)
            return Task.FromResult(1.0m);

        var factor = formula.Components
            .Where(c => c.BaseIndex != 0)
            .Sum(c => c.Weight * (c.CurrentIndex / c.BaseIndex)) / totalWeight;

        return Task.FromResult(Math.Round(factor, 6));
    }

    // ── Indexes ────────────────────────────────────────

    public async Task<AdjustmentIndex> SaveIndexAsync(AdjustmentIndex index, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateAdjustment(_userPermissions.CurrentRole), "SaveIndex");

        if (index.Id is null)
        {
            index = new AdjustmentIndex
            {
                Id = AdjustmentIndexId.New(),
                Name = index.Name,
                Code = index.Code,
                Value = index.Value,
                Date = index.Date,
                Source = index.Source
            };
        }

        await _indexRepo.SaveAsync(index, ct);
        return index;
    }

    public Task<List<AdjustmentIndex>> SearchIndexesAsync(string? name, string? code, CancellationToken ct = default)
        => _indexRepo.SearchAsync(name, code, ct);
}
