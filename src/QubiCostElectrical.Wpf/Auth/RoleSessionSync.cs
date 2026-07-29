using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Auth;

public static class RoleSessionSync
{
    public static void Apply(BudgetProjectSession session, UserRole role)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        bool readOnly = !UserRolePermissions.CanEditBudget(role);
        session.SetRoleReadOnly(readOnly, UserRolePermissions.GetRoleReadOnlyMessage(role));
    }

    public static void Apply(BudgetProjectSession session, IPermissionService permissions)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (permissions == null) throw new ArgumentNullException(nameof(permissions));

        session.SetRoleReadOnly(!permissions.CanEditBudget, permissions.RoleReadOnlyMessage);
    }
}
