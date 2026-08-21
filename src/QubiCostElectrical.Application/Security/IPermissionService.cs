namespace QubiCostElectrical;

public interface IPermissionService
{
    UserRole CurrentRole { get; }
    void SetRole(UserRole role);
    bool CanEditBudget { get; }
    bool CanExportDocuments { get; }
    bool CanEditGlobalPriceBank { get; }
    bool CanManageAppSettings { get; }
    string RoleDisplayName { get; }
    string RoleReadOnlyMessage { get; }
}

public sealed class UserRolePermissionService : IPermissionService
{
    private UserRole currentRole = UserRole.Viewer;

    public UserRole CurrentRole => currentRole;

    public void SetRole(UserRole role) => currentRole = role;

    public bool CanEditBudget => UserRolePermissions.CanEditBudget(currentRole);

    public bool CanExportDocuments => UserRolePermissions.CanExportDocuments(currentRole);

    public bool CanEditGlobalPriceBank => UserRolePermissions.CanEditGlobalPriceBank(currentRole);

    public bool CanManageAppSettings => UserRolePermissions.CanManageAppSettings(currentRole);

    public string RoleDisplayName => UserRolePermissions.GetDisplayName(currentRole);

    public string RoleReadOnlyMessage => UserRolePermissions.GetRoleReadOnlyMessage(currentRole);
}
