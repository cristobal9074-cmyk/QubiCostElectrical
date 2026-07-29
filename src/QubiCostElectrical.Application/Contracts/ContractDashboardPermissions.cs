namespace QubiCostElectrical;

public static class ContractDashboardPermissions
{
    public static bool CanViewDashboard(UserRole role) => true;
    public static bool CanCreateSnapshot(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanCompareSnapshots(UserRole role) => true;
    public static bool CanExportDashboard(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanAcknowledgeAlert(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanResolveAlert(UserRole role) => role is UserRole.Admin;
    public static bool CanViewAlertAudit(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
}

public sealed class ContractDashboardPermissionChecker : IContractDashboardPermissionChecker
{
    public bool CanViewDashboard(UserRole role) => ContractDashboardPermissions.CanViewDashboard(role);
    public bool CanCreateSnapshot(UserRole role) => ContractDashboardPermissions.CanCreateSnapshot(role);
    public bool CanCompareSnapshots(UserRole role) => ContractDashboardPermissions.CanCompareSnapshots(role);
    public bool CanExportDashboard(UserRole role) => ContractDashboardPermissions.CanExportDashboard(role);
    public bool CanAcknowledgeAlert(UserRole role) => ContractDashboardPermissions.CanAcknowledgeAlert(role);
    public bool CanResolveAlert(UserRole role) => ContractDashboardPermissions.CanResolveAlert(role);
    public bool CanViewAlertAudit(UserRole role) => ContractDashboardPermissions.CanViewAlertAudit(role);
}

public interface IContractDashboardPermissionChecker
{
    bool CanViewDashboard(UserRole role);
    bool CanCreateSnapshot(UserRole role);
    bool CanCompareSnapshots(UserRole role);
    bool CanExportDashboard(UserRole role);
    bool CanAcknowledgeAlert(UserRole role);
    bool CanResolveAlert(UserRole role);
    bool CanViewAlertAudit(UserRole role);
}
