using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

// ──────────────────────────────────────────────────────────────────
// Implementation (partial — shared fields, constructor, helpers)
// ──────────────────────────────────────────────────────────────────

public sealed partial class ContractAdministrationService : IContractAdministrationService
{
    // Repositories
    private readonly IContractAdjustmentRepository _adjustmentRepo;
    private readonly IAdjustmentFormulaRepository _formulaRepo;
    private readonly IAdjustmentIndexRepository _indexRepo;
    private readonly IContractGuaranteeRepository _guaranteeRepo;
    private readonly IContractPenaltyRepository _penaltyRepo;
    private readonly IContractInvoiceRepository _invoiceRepo;
    private readonly IContractAdministrationAuditRepository _auditRepo;
    private readonly IContractAdministrationPermissionChecker _permissions;
    private readonly IPermissionService _userPermissions;
    private readonly SoDPolicyConfig _sod;
    private readonly IContractRepository _contractRepo;

    public ContractAdministrationService(
        IContractAdjustmentRepository adjustmentRepo,
        IAdjustmentFormulaRepository formulaRepo,
        IAdjustmentIndexRepository indexRepo,
        IContractGuaranteeRepository guaranteeRepo,
        IContractPenaltyRepository penaltyRepo,
        IContractInvoiceRepository invoiceRepo,
        IContractAdministrationAuditRepository auditRepo,
        IContractAdministrationPermissionChecker permissions,
        IPermissionService userPermissions,
        IContractRepository contractRepo,
        SoDPolicyConfig? sod = null)
    {
        _adjustmentRepo = adjustmentRepo;
        _formulaRepo = formulaRepo;
        _indexRepo = indexRepo;
        _guaranteeRepo = guaranteeRepo;
        _penaltyRepo = penaltyRepo;
        _invoiceRepo = invoiceRepo;
        _auditRepo = auditRepo;
        _permissions = permissions;
        _userPermissions = userPermissions;
        _contractRepo = contractRepo;
        _sod = sod ?? new SoDPolicyConfig();
    }

    // ── Helpers ─────────────────────────────────────────────

    private void Ensure(bool canDo, string action)
    {
        if (!canDo)
            throw new UnauthorizedAccessException($"No tiene permisos para la acción de administración contractual: {action}.");
    }

    private async Task RecordAuditAsync(ContractId contractId, string entityType, string entityId, string action, string actor, string details, bool isDenied = false, CancellationToken ct = default)
    {
        var audit = new ContractAdministrationAuditEvent
        {
            Id = ContractAdministrationAuditEventId.New(),
            ContractId = contractId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            TimestampUtc = DateTime.UtcNow,
            DetailsJson = JsonSerializer.Serialize(new { Action = action, Details = details, Date = DateTime.UtcNow }),
            IsDeniedAttempt = isDenied
        };
        await _auditRepo.SaveAsync(audit, ct);
    }

    private static string ComputeHash(string json)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexStringLower(bytes);
    }

    private async Task<ContractAdjustment> LoadAdjustmentWritable(AdjustmentId id, string expectedRowVersion, CancellationToken ct)
    {
        var existing = await _adjustmentRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Reajuste no encontrado.");
        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: El reajuste ha sido modificado por otro usuario.");
        return existing;
    }

    private async Task<ContractGuarantee> LoadGuaranteeWritable(GuaranteeId id, string expectedRowVersion, CancellationToken ct)
    {
        var existing = await _guaranteeRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Garantía no encontrada.");
        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La garantía ha sido modificada por otro usuario.");
        return existing;
    }

    private async Task<ContractPenalty> LoadPenaltyWritable(PenaltyId id, string expectedRowVersion, CancellationToken ct)
    {
        var existing = await _penaltyRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Multa no encontrada.");
        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La multa ha sido modificada por otro usuario.");
        return existing;
    }

    private async Task<ContractInvoice> LoadInvoiceWritable(InvoiceId id, string expectedRowVersion, CancellationToken ct)
    {
        var existing = await _invoiceRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Factura no encontrada.");
        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("Concurrencia: La factura ha sido modificada por otro usuario.");
        return existing;
    }

    private void StampRowVersion<T>(T entity, string actor) where T : class
    {
        var rvProp = typeof(T).GetProperty("RowVersion");
        rvProp?.SetValue(entity, Guid.NewGuid().ToString("N"));

        var modUtcProp = typeof(T).GetProperty("ModifiedUtc");
        modUtcProp?.SetValue(entity, DateTime.UtcNow);

        var modByProp = typeof(T).GetProperty("ModifiedBy");
        modByProp?.SetValue(entity, actor);
    }
}
