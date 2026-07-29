using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class UserRolePermissionServiceTests
{
    [Fact]
    public void Viewer_CannotExportOrEdit()
    {
        var service = new UserRolePermissionService();
        service.SetRole(UserRole.Viewer);

        Assert.False(service.CanEditBudget);
        Assert.False(service.CanExportDocuments);
        Assert.False(service.CanManageAppSettings);
    }

    [Fact]
    public void Estimator_CanExportButNotManageSettings()
    {
        var service = new UserRolePermissionService();
        service.SetRole(UserRole.Estimator);

        Assert.True(service.CanEditBudget);
        Assert.True(service.CanExportDocuments);
        Assert.False(service.CanManageAppSettings);
    }

    [Fact]
    public void Administrator_HasAllConfiguredCapabilities()
    {
        var service = new UserRolePermissionService();
        service.SetRole(UserRole.Admin);

        Assert.True(service.CanEditBudget);
        Assert.True(service.CanExportDocuments);
        Assert.True(service.CanEditGlobalPriceBank);
        Assert.True(service.CanManageAppSettings);
    }

    [Fact]
    public void Estimator_CanEditGlobalPriceBank()
    {
        var service = new UserRolePermissionService();
        service.SetRole(UserRole.Estimator);

        Assert.True(service.CanEditGlobalPriceBank);
    }
}
