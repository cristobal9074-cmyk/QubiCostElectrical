using System;
using System.Collections.Generic;
using System.Threading;

namespace QubiCostElectrical;

partial class AppServicesBuilder
{
    // ── Contract Administration fields ─────────────────────────────────
    private IContractAdjustmentRepository? contractAdjustmentRepository;
    private IAdjustmentFormulaRepository? adjustmentFormulaRepository;
    private IAdjustmentIndexRepository? adjustmentIndexRepository;
    private IContractGuaranteeRepository? contractGuaranteeRepository;
    private IContractPenaltyRepository? contractPenaltyRepository;
    private IContractInvoiceRepository? contractInvoiceRepository;
    private IContractAdministrationAuditRepository? contractAdministrationAuditRepository;
    private IContractAdministrationPermissionChecker? contractAdministrationPermissionChecker;
    private IContractAdministrationService? contractAdministrationService;

    private void WireContractAdministrationServices(AppServices svc)
    {
        if (contractAdjustmentRepository != null) svc.ContractAdjustmentRepository = contractAdjustmentRepository;
        if (adjustmentFormulaRepository != null) svc.AdjustmentFormulaRepository = adjustmentFormulaRepository;
        if (adjustmentIndexRepository != null) svc.AdjustmentIndexRepository = adjustmentIndexRepository;
        if (contractGuaranteeRepository != null) svc.ContractGuaranteeRepository = contractGuaranteeRepository;
        if (contractPenaltyRepository != null) svc.ContractPenaltyRepository = contractPenaltyRepository;
        if (contractInvoiceRepository != null) svc.ContractInvoiceRepository = contractInvoiceRepository;
        if (contractAdministrationAuditRepository != null) svc.ContractAdministrationAuditRepository = contractAdministrationAuditRepository;
        if (contractAdministrationPermissionChecker != null) svc.ContractAdministrationPermissionChecker = contractAdministrationPermissionChecker;
        if (contractAdministrationService != null) svc.ContractAdministrationService = contractAdministrationService;
    }

    // ── Fluent With* methods ──────────────────────────────────────────
    public AppServicesBuilder WithContractAdjustmentRepository(IContractAdjustmentRepository value)
    {
        contractAdjustmentRepository = value;
        return this;
    }

    public AppServicesBuilder WithAdjustmentFormulaRepository(IAdjustmentFormulaRepository value)
    {
        adjustmentFormulaRepository = value;
        return this;
    }

    public AppServicesBuilder WithAdjustmentIndexRepository(IAdjustmentIndexRepository value)
    {
        adjustmentIndexRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractGuaranteeRepository(IContractGuaranteeRepository value)
    {
        contractGuaranteeRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractPenaltyRepository(IContractPenaltyRepository value)
    {
        contractPenaltyRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractInvoiceRepository(IContractInvoiceRepository value)
    {
        contractInvoiceRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractAdministrationAuditRepository(IContractAdministrationAuditRepository value)
    {
        contractAdministrationAuditRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractAdministrationPermissionChecker(IContractAdministrationPermissionChecker value)
    {
        contractAdministrationPermissionChecker = value;
        return this;
    }

    public AppServicesBuilder WithContractAdministrationService(IContractAdministrationService value)
    {
        contractAdministrationService = value;
        return this;
    }
}
