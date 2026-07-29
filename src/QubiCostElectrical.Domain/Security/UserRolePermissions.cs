namespace QubiCostElectrical;

public static class UserRolePermissions
{
    public static bool CanEditBudget(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanExportDocuments(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanEditGlobalPriceBank(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanManageAppSettings(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanApproveVersions(UserRole role) =>
        role is UserRole.Approver or UserRole.Admin;

    public static bool CanSendVersionToReview(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanStartVersionReview(UserRole role) =>
        role is UserRole.Reviewer or UserRole.Admin;

    public static bool CanRejectVersion(UserRole role) =>
        role is UserRole.Reviewer or UserRole.Approver or UserRole.Admin;

    public static bool CanViewAudit(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanManageUsers(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanManageRoles(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanManagePolicies(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanCompareVersions(UserRole role) =>
        role is not UserRole.Viewer;

    public static string GetDisplayName(UserRole role) => role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Estimator => "Estimador",
        UserRole.Reviewer => "Revisor",
        UserRole.Approver => "Aprobador",
        UserRole.Viewer => "Visor",
        _ => role.ToString()
    };

    public static string GetRoleReadOnlyMessage(UserRole role) =>
        role == UserRole.Viewer
            ? "Rol Visor: solo consulta. No puede editar, exportar ni modificar precios."
            : "";
}
