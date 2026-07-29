using System.Collections.Generic;
using System.Threading;

namespace QubiCostElectrical;

// ── AppServices partial: Contract Administration properties ─────────────
partial class AppServices
{
    // Adjustment
    public IContractAdjustmentRepository ContractAdjustmentRepository { get; internal set; } = null!;
    public IAdjustmentFormulaRepository AdjustmentFormulaRepository { get; internal set; } = null!;
    public IAdjustmentIndexRepository AdjustmentIndexRepository { get; internal set; } = null!;

    // Guarantee
    public IContractGuaranteeRepository ContractGuaranteeRepository { get; internal set; } = null!;

    // Penalty
    public IContractPenaltyRepository ContractPenaltyRepository { get; internal set; } = null!;

    // Invoice
    public IContractInvoiceRepository ContractInvoiceRepository { get; internal set; } = null!;

    // Audit
    public IContractAdministrationAuditRepository ContractAdministrationAuditRepository { get; internal set; } = null!;

    // Permission checker
    public IContractAdministrationPermissionChecker ContractAdministrationPermissionChecker { get; internal set; } = null!;

    // Service
    public IContractAdministrationService ContractAdministrationService { get; internal set; } = null!;

    public void SetContractAdministrationServices(
        IContractAdjustmentRepository adjustmentRepo,
        IAdjustmentFormulaRepository formulaRepo,
        IAdjustmentIndexRepository indexRepo,
        IContractGuaranteeRepository guaranteeRepo,
        IContractPenaltyRepository penaltyRepo,
        IContractInvoiceRepository invoiceRepo,
        IContractAdministrationAuditRepository auditRepo,
        IContractAdministrationPermissionChecker permissionChecker,
        IContractAdministrationService service)
    {
        ContractAdjustmentRepository = adjustmentRepo;
        AdjustmentFormulaRepository = formulaRepo;
        AdjustmentIndexRepository = indexRepo;
        ContractGuaranteeRepository = guaranteeRepo;
        ContractPenaltyRepository = penaltyRepo;
        ContractInvoiceRepository = invoiceRepo;
        ContractAdministrationAuditRepository = auditRepo;
        ContractAdministrationPermissionChecker = permissionChecker;
        ContractAdministrationService = service;
    }
}
