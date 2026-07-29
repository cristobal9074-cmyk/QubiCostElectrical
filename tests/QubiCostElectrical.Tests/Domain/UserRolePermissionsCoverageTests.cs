using Xunit;

namespace QubiCostElectrical.Tests;

public class UserRolePermissionsCoverageTests
{
    [Fact]
    public void Admin_HasAllPermissions()
    {
        Assert.True(UserRolePermissions.CanEditBudget(UserRole.Admin));
        Assert.True(UserRolePermissions.CanExportDocuments(UserRole.Admin));
        Assert.True(UserRolePermissions.CanEditGlobalPriceBank(UserRole.Admin));
        Assert.True(UserRolePermissions.CanManageAppSettings(UserRole.Admin));
        Assert.True(UserRolePermissions.CanViewAudit(UserRole.Admin));
        Assert.True(UserRolePermissions.CanManageUsers(UserRole.Admin));
        Assert.True(UserRolePermissions.CanManageRoles(UserRole.Admin));
        Assert.True(UserRolePermissions.CanManagePolicies(UserRole.Admin));
        Assert.True(UserRolePermissions.CanCompareVersions(UserRole.Admin));
        Assert.True(UserRolePermissions.CanRejectVersion(UserRole.Admin));
        Assert.True(UserRolePermissions.CanApproveVersions(UserRole.Admin));
        Assert.True(UserRolePermissions.CanSendVersionToReview(UserRole.Admin));
        Assert.True(UserRolePermissions.CanStartVersionReview(UserRole.Admin));
    }

    [Fact]
    public void Estimator_HasCorePermissions()
    {
        Assert.True(UserRolePermissions.CanEditBudget(UserRole.Estimator));
        Assert.True(UserRolePermissions.CanExportDocuments(UserRole.Estimator));
        Assert.True(UserRolePermissions.CanEditGlobalPriceBank(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanManageAppSettings(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanViewAudit(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanManageUsers(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanManageRoles(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanManagePolicies(UserRole.Estimator));
        Assert.True(UserRolePermissions.CanCompareVersions(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanRejectVersion(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanApproveVersions(UserRole.Estimator));
        Assert.True(UserRolePermissions.CanSendVersionToReview(UserRole.Estimator));
        Assert.False(UserRolePermissions.CanStartVersionReview(UserRole.Estimator));
    }

    [Fact]
    public void Viewer_HasReadOnlyPermissions()
    {
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanExportDocuments(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanEditGlobalPriceBank(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanManageAppSettings(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanViewAudit(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanManageUsers(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanManageRoles(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanManagePolicies(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanCompareVersions(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanRejectVersion(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanApproveVersions(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanSendVersionToReview(UserRole.Viewer));
        Assert.False(UserRolePermissions.CanStartVersionReview(UserRole.Viewer));
    }

    [Fact]
    public void Reviewer_HasReviewPermissions()
    {
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanExportDocuments(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanEditGlobalPriceBank(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanManageAppSettings(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanViewAudit(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanManageUsers(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanManageRoles(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanManagePolicies(UserRole.Reviewer));
        Assert.True(UserRolePermissions.CanCompareVersions(UserRole.Reviewer));
        Assert.True(UserRolePermissions.CanRejectVersion(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanApproveVersions(UserRole.Reviewer));
        Assert.False(UserRolePermissions.CanSendVersionToReview(UserRole.Reviewer));
        Assert.True(UserRolePermissions.CanStartVersionReview(UserRole.Reviewer));
    }

    [Fact]
    public void Approver_HasApprovalPermissions()
    {
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Approver));
        Assert.False(UserRolePermissions.CanExportDocuments(UserRole.Approver));
        Assert.False(UserRolePermissions.CanEditGlobalPriceBank(UserRole.Approver));
        Assert.False(UserRolePermissions.CanManageAppSettings(UserRole.Approver));
        Assert.False(UserRolePermissions.CanViewAudit(UserRole.Approver));
        Assert.False(UserRolePermissions.CanManageUsers(UserRole.Approver));
        Assert.False(UserRolePermissions.CanManageRoles(UserRole.Approver));
        Assert.False(UserRolePermissions.CanManagePolicies(UserRole.Approver));
        Assert.True(UserRolePermissions.CanCompareVersions(UserRole.Approver));
        Assert.True(UserRolePermissions.CanRejectVersion(UserRole.Approver));
        Assert.True(UserRolePermissions.CanApproveVersions(UserRole.Approver));
        Assert.False(UserRolePermissions.CanSendVersionToReview(UserRole.Approver));
        Assert.False(UserRolePermissions.CanStartVersionReview(UserRole.Approver));
    }

    [Fact]
    public void Reviewer_CannotEditEconomics()
    {
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Reviewer));
    }

    [Fact]
    public void Approver_CannotEditEconomics()
    {
        Assert.False(UserRolePermissions.CanEditBudget(UserRole.Approver));
    }

    [Fact]
    public void AllRoles_HaveNonNullDisplayName()
    {
        foreach (UserRole role in Enum.GetValues<UserRole>())
        {
            var name = UserRolePermissions.GetDisplayName(role);
            Assert.NotNull(name);
            Assert.NotEqual("", name);
        }
    }
}
