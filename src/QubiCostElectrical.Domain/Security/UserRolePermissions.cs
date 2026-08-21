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

    public static string GetDisplayName(UserRole role) => role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Estimator => "Estimador",
        UserRole.Viewer => "Visor",
        _ => role.ToString()
    };

    public static string GetRoleReadOnlyMessage(UserRole role) =>
        role == UserRole.Viewer
            ? "Rol Visor: solo consulta. No puede editar, exportar ni modificar precios."
            : "";
}
