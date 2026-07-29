using System;
using System.Collections.Generic;
using System.Threading;

namespace QubiCostElectrical;

partial class AppServicesBuilder
{
    // ── Contract Dashboard fields ──────────────────────────────────
    private IContractDashboardRepository? contractDashboardRepository;
    private IContractDashboardPermissionChecker? contractDashboardPermissionChecker;
    private IContractDashboardService? contractDashboardService;

    private void WireContractDashboardServices(AppServices svc)
    {
        if (contractDashboardRepository != null) svc.ContractDashboardRepository = contractDashboardRepository;
        if (contractDashboardPermissionChecker != null) svc.ContractDashboardPermissionChecker = contractDashboardPermissionChecker;
        if (contractDashboardService != null) svc.ContractDashboardService = contractDashboardService;
    }

    // ── Fluent With* methods ───────────────────────────────────────
    public AppServicesBuilder WithContractDashboardRepository(IContractDashboardRepository value)
    {
        contractDashboardRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractDashboardPermissionChecker(IContractDashboardPermissionChecker value)
    {
        contractDashboardPermissionChecker = value;
        return this;
    }

    public AppServicesBuilder WithContractDashboardService(IContractDashboardService value)
    {
        contractDashboardService = value;
        return this;
    }
}
