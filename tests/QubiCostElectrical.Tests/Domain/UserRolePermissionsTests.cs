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

    // ── Permisos de Workflow de Versiones ──────────────────────────────────────

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Estimator, false)]
    [InlineData(UserRole.Viewer, false)]
    public void CanApproveVersions_OnlyAdmin(UserRole role, bool expected)
    {
        Assert.Equal(expected, UserRolePermissions.CanApproveVersions(role));
    }

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Estimator, true)]
    [InlineData(UserRole.Viewer, false)]
    public void CanSendVersionToReview_AdminAndEstimator(UserRole role, bool expected)
    {
        Assert.Equal(expected, UserRolePermissions.CanSendVersionToReview(role));
    }

    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.Estimator, false)]
    [InlineData(UserRole.Viewer, false)]
    public void CanStartVersionReview_OnlyAdmin(UserRole role, bool expected)
    {
        Assert.Equal(expected, UserRolePermissions.CanStartVersionReview(role));
    }
}
