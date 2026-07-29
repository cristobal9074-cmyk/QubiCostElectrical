using System.Collections.Generic;
using System.Threading;

namespace QubiCostElectrical;

// ── AppServices partial: Contract Dashboard properties ─────────────
partial class AppServices
{
    // Dashboard repository
    public IContractDashboardRepository ContractDashboardRepository { get; internal set; } = null!;

    // Permission checker
    public IContractDashboardPermissionChecker ContractDashboardPermissionChecker { get; internal set; } = null!;

    // Service
    public IContractDashboardService ContractDashboardService { get; internal set; } = null!;

    public void SetContractDashboardServices(
        IContractDashboardRepository dashboardRepo,
        IContractDashboardPermissionChecker permissionChecker,
        IContractDashboardService service)
    {
        ContractDashboardRepository = dashboardRepo;
        ContractDashboardPermissionChecker = permissionChecker;
        ContractDashboardService = service;
    }
}
