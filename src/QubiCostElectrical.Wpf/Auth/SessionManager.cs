using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Auth;

public static class SessionManager
{
    private static UserSession? _current;

    public static UserSession? Current
    {
        get => _current;
        private set
        {
            _current = value;
            CurrentChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static bool IsLoggedIn => _current?.IsValid == true;

    public static string UserDisplayName => _current?.DisplayName ?? "";
    public static string UserInitials => _current?.Initials ?? "US";
    public static string UserEmail => _current?.Email ?? "";

    public static UserRole CurrentRole => Permissions.CurrentRole;

    public static bool CanExportDocuments => Permissions.CanExportDocuments;

    public static bool CanManageAppSettings => Permissions.CanManageAppSettings;

    public static string RoleDisplayName => Permissions.RoleDisplayName;

    internal static IPermissionService Permissions { get; private set; } = new UserRolePermissionService();

    internal static void BindPermissions(IPermissionService permissions)
        => Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));

    public static event EventHandler? CurrentChanged;

    public static void SetSession(UserSession session)
    {
        Current = session;
        Permissions.SetRole(session?.Account?.Role ?? UserRole.Viewer);
    }

    public static void ClearSession()
    {
        Current = null;
        Permissions.SetRole(UserRole.Viewer);
    }
}
