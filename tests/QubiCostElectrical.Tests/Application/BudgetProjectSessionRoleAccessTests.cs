using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class BudgetProjectSessionRoleAccessTests
{
    [Fact]
    public void Viewer_role_blocks_edit_while_lock_can_still_export_for_estimator()
    {
        var session = new BudgetProjectSession();

        session.SetRoleReadOnly(true, "Rol Visor");
        Assert.True(session.IsReadOnly);
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanExportDocuments(UserRole.Viewer));

        session.SetLockReadOnly(true, "Lock local");
        Assert.Contains("Rol Visor", session.ReadOnlyReason);
        Assert.Contains("Lock local", session.ReadOnlyReason);
    }

    [Fact]
    public void Estimator_with_lock_readonly_can_still_export_by_policy()
    {
        var session = new BudgetProjectSession();
        session.SetRoleReadOnly(false);
        session.SetLockReadOnly(true, "Otro proceso");

        Assert.True(session.IsReadOnly);
        Assert.True(UserRolePermissions.CanExportDocuments(UserRole.Estimator));
    }
}
