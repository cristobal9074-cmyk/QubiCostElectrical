using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Domain;

public sealed class UserRolePermissionsTests
{
    [Theory]
    [InlineData(UserRole.Admin, true, true, true)]
    [InlineData(UserRole.Estimator, true, true, false)]
    [InlineData(UserRole.Viewer, false, false, false)]
    public void Role_matrix(UserRole role, bool canEdit, bool canExport, bool canManageSettings)
    {
        Assert.Equal(canEdit, UserRolePermissions.CanEditBudget(role));
        Assert.Equal(canExport, UserRolePermissions.CanExportDocuments(role));
        Assert.Equal(canManageSettings, UserRolePermissions.CanManageAppSettings(role));
    }

    [Fact]
    public void Viewer_has_read_only_message()
    {
        Assert.Contains("Visor", UserRolePermissions.GetRoleReadOnlyMessage(UserRole.Viewer));
        Assert.Equal("", UserRolePermissions.GetRoleReadOnlyMessage(UserRole.Estimator));
    }
}
