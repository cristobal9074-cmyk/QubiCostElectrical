using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────
// ContractGuarantee operations
// ──────────────────────────────────────────────────────────────
partial class ContractAdministrationService
{
    public async Task<ContractGuarantee> CreateGuaranteeAsync(ContractGuarantee draft, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanCreateGuarantee(_userPermissions.CurrentRole), "CreateGuarantee");

        var id = GuaranteeId.New();
        var nextNum = await _guaranteeRepo.GetNextNumberAsync(draft.ProjectPath, ct);

        var guarantee = new ContractGuarantee
        {
            Id = id,
            ContractId = draft.ContractId,
            VisibleNumber = nextNum,
            GuaranteeType = draft.GuaranteeType,
            Number = draft.Number,
            Issuer = draft.Issuer,
            Institution = draft.Institution,
            Beneficiary = draft.Beneficiary,
            Amount = draft.Amount,
            Percentage = draft.Percentage,
            CurrencyCode = draft.CurrencyCode,
            StartDate = draft.StartDate,
            ExpiryDate = draft.ExpiryDate,
            Status = GuaranteeStatus.Draft,
            SupportingDocuments = draft.SupportingDocuments,
            Responsible = draft.Responsible,
            ProjectPath = draft.ProjectPath,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };

        await _guaranteeRepo.SaveAsync(guarantee, ct);
        await RecordAuditAsync(guarantee.ContractId, "ContractGuarantee", id.Value, "CREAR", actor, "Creación de garantía", ct: ct);

        return guarantee;
    }

    public async Task<ContractGuarantee> UpdateGuaranteeDraftAsync(ContractGuarantee guarantee, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditGuarantee(_userPermissions.CurrentRole), "EditGuarantee");

        var existing = await LoadGuaranteeWritable(guarantee.Id, expectedRowVersion, ct);

        if (_sod.ApprovedOrObsoleteCannotEdit && existing.Status is GuaranteeStatus.Active or GuaranteeStatus.Renewed or GuaranteeStatus.Released or GuaranteeStatus.Executed)
            throw new InvalidOperationException("No se puede editar una garantía activa, renovada, liberada o ejecutada.");

        existing.GuaranteeType = guarantee.GuaranteeType;
        existing.Number = guarantee.Number;
        existing.Issuer = guarantee.Issuer;
        existing.Institution = guarantee.Institution;
        existing.Beneficiary = guarantee.Beneficiary;
        existing.Amount = guarantee.Amount;
        existing.Percentage = guarantee.Percentage;
        existing.CurrencyCode = guarantee.CurrencyCode;
        existing.StartDate = guarantee.StartDate;
        existing.ExpiryDate = guarantee.ExpiryDate;
        existing.SupportingDocuments = guarantee.SupportingDocuments;
        existing.Responsible = guarantee.Responsible;
        existing.Comment = guarantee.Comment;

        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", existing.Id.Value, "EDITAR", actor, "Edición de garantía", ct: ct);

        return existing;
    }

    public async Task<ContractGuarantee> ActivateGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditGuarantee(_userPermissions.CurrentRole), "ActivateGuarantee");

        var existing = await LoadGuaranteeWritable(id, expectedRowVersion, ct);
        GuaranteeStatusTransitions.EnsureCanTransition(existing.Status, GuaranteeStatus.Active);

        existing.Status = GuaranteeStatus.Active;
        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", id.Value, "ACTIVAR", actor, "Garantía activada", ct: ct);

        return existing;
    }

    public async Task<ContractGuarantee> RenewGuaranteeAsync(GuaranteeId id, GuaranteeRenewal renewal, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditGuarantee(_userPermissions.CurrentRole), "RenewGuarantee");

        var existing = await LoadGuaranteeWritable(id, expectedRowVersion, ct);
        GuaranteeStatusTransitions.EnsureCanTransition(existing.Status, GuaranteeStatus.Renewed);

        renewal.Id = string.IsNullOrWhiteSpace(renewal.Id) ? Guid.NewGuid().ToString("N") : renewal.Id;
        renewal.RenewalDate = renewal.RenewalDate == default ? DateTime.UtcNow : renewal.RenewalDate;
        renewal.RenewedBy = actor;

        existing.Renewals.Add(renewal);
        existing.Status = GuaranteeStatus.Renewed;

        // Update expiry with new one
        if (renewal.NewExpiryDate > existing.ExpiryDate)
            existing.ExpiryDate = renewal.NewExpiryDate;

        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", id.Value, "RENOVAR", actor, $"Garantía renovada: {renewal.NewNumber}", ct: ct);

        return existing;
    }

    public async Task<ContractGuarantee> ReleaseGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApproveGuarantee(_userPermissions.CurrentRole), "ReleaseGuarantee");

        var existing = await LoadGuaranteeWritable(id, expectedRowVersion, ct);
        GuaranteeStatusTransitions.EnsureCanTransition(existing.Status, GuaranteeStatus.Released);

        existing.Status = GuaranteeStatus.Released;
        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", id.Value, "LIBERAR", actor, "Garantía liberada", ct: ct);

        return existing;
    }

    public async Task<ContractGuarantee> ExpireGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanViewGuarantee(_userPermissions.CurrentRole), "ExpireGuarantee");

        var existing = await LoadGuaranteeWritable(id, expectedRowVersion, ct);

        var from = existing.Status is GuaranteeStatus.Active or GuaranteeStatus.Renewed ? existing.Status : GuaranteeStatus.Active;
        GuaranteeStatusTransitions.EnsureCanTransition(from, GuaranteeStatus.Expired);

        existing.Status = GuaranteeStatus.Expired;
        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", id.Value, "VENCER", actor, "Garantía vencida", ct: ct);

        return existing;
    }

    public async Task<ContractGuarantee> ExecuteGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(_permissions.CanApproveGuarantee(_userPermissions.CurrentRole), "ExecuteGuarantee");

        var existing = await LoadGuaranteeWritable(id, expectedRowVersion, ct);
        GuaranteeStatusTransitions.EnsureCanTransition(existing.Status, GuaranteeStatus.Executed);

        existing.Status = GuaranteeStatus.Executed;
        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", id.Value, "EJECUTAR", actor, "Garantía ejecutada", ct: ct);

        return existing;
    }

    public async Task<ContractGuarantee> CancelGuaranteeAsync(GuaranteeId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(_permissions.CanEditGuarantee(_userPermissions.CurrentRole), "CancelGuarantee");

        var existing = await LoadGuaranteeWritable(id, expectedRowVersion, ct);

        // Allow cancel from most states
        existing.Status = GuaranteeStatus.Cancelled;
        existing.Comment = reason;
        StampRowVersion(existing, actor);

        await _guaranteeRepo.SaveAsync(existing, ct);
        await RecordAuditAsync(existing.ContractId, "ContractGuarantee", id.Value, "CANCELAR", actor, $"Garantía cancelada: {reason}", ct: ct);

        return existing;
    }

    public Task<ContractGuarantee?> GetGuaranteeAsync(GuaranteeId id, CancellationToken ct = default)
        => _guaranteeRepo.GetByIdAsync(id, ct);

    public Task<List<ContractGuarantee>> GetGuaranteesByContractAsync(ContractId contractId, CancellationToken ct = default)
        => _guaranteeRepo.GetByContractAsync(contractId, ct);

    public Task<List<ContractGuarantee>> GetExpiringGuaranteesAsync(int withinDays, CancellationToken ct = default)
        => _guaranteeRepo.GetExpiringAsync(withinDays, ct);
}
