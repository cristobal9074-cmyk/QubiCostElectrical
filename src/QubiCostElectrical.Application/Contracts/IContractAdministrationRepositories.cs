using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IContractAdjustmentRepository
{
    Task SaveAsync(ContractAdjustment adjustment, CancellationToken ct = default);
    Task<ContractAdjustment?> GetByIdAsync(AdjustmentId id, CancellationToken ct = default);
    Task<List<ContractAdjustment>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default);
}

public interface IAdjustmentFormulaRepository
{
    Task SaveAsync(AdjustmentFormula formula, CancellationToken ct = default);
    Task<AdjustmentFormula?> GetByIdAsync(AdjustmentFormulaId id, CancellationToken ct = default);
    Task<List<AdjustmentFormula>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
}

public interface IAdjustmentIndexRepository
{
    Task SaveAsync(AdjustmentIndex index, CancellationToken ct = default);
    Task<AdjustmentIndex?> GetByIdAsync(AdjustmentIndexId id, CancellationToken ct = default);
    Task<List<AdjustmentIndex>> SearchAsync(string? name, string? code, CancellationToken ct = default);
}

public interface IContractGuaranteeRepository
{
    Task SaveAsync(ContractGuarantee guarantee, CancellationToken ct = default);
    Task<ContractGuarantee?> GetByIdAsync(GuaranteeId id, CancellationToken ct = default);
    Task<List<ContractGuarantee>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<List<ContractGuarantee>> GetExpiringAsync(int withinDays, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default);
}

public interface IContractPenaltyRepository
{
    Task SaveAsync(ContractPenalty penalty, CancellationToken ct = default);
    Task<ContractPenalty?> GetByIdAsync(PenaltyId id, CancellationToken ct = default);
    Task<List<ContractPenalty>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default);
}

public interface IContractInvoiceRepository
{
    Task SaveAsync(ContractInvoice invoice, CancellationToken ct = default);
    Task<ContractInvoice?> GetByIdAsync(InvoiceId id, CancellationToken ct = default);
    Task<List<ContractInvoice>> GetByContractAsync(ContractId contractId, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string contractPath, CancellationToken ct = default);
}

public interface IContractAdministrationAuditRepository
{
    Task SaveAsync(ContractAdministrationAuditEvent auditEvent, CancellationToken ct = default);
    Task<List<ContractAdministrationAuditEvent>> GetByContractAsync(ContractId contractId, int skip, int take, CancellationToken ct = default);
    Task<List<ContractAdministrationAuditEvent>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default);
}

public interface IContractAdministrationPermissionChecker
{
    bool CanViewAdjustment(UserRole role);
    bool CanCreateAdjustment(UserRole role);
    bool CanEditAdjustment(UserRole role);
    bool CanCalculateAdjustment(UserRole role);
    bool CanApplyAdjustment(UserRole role);
    bool CanViewGuarantee(UserRole role);
    bool CanCreateGuarantee(UserRole role);
    bool CanEditGuarantee(UserRole role);
    bool CanApproveGuarantee(UserRole role);
    bool CanViewPenalty(UserRole role);
    bool CanCreatePenalty(UserRole role);
    bool CanEditPenalty(UserRole role);
    bool CanApprovePenalty(UserRole role);
    bool CanViewInvoice(UserRole role);
    bool CanCreateInvoice(UserRole role);
    bool CanEditInvoice(UserRole role);
    bool CanExportContractAdministration(UserRole role);
}
