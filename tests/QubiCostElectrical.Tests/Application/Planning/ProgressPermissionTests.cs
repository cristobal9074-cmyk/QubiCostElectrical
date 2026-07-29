using System;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class ProgressPermissionTests
{
    [Theory]
    [InlineData(nameof(PlanningPermissions.ProgressView), "Planning.ProgressView")]
    [InlineData(nameof(PlanningPermissions.ProgressUpdate), "Planning.ProgressUpdate")]
    [InlineData(nameof(PlanningPermissions.ProgressApprove), "Planning.ProgressApprove")]
    [InlineData(nameof(PlanningPermissions.StatusDateManage), "Planning.StatusDateManage")]
    [InlineData(nameof(PlanningPermissions.ActualDatesManage), "Planning.ActualDatesManage")]
    [InlineData(nameof(PlanningPermissions.ActualCostManage), "Planning.ActualCostManage")]
    [InlineData(nameof(PlanningPermissions.DelayReasonManage), "Planning.DelayReasonManage")]
    [InlineData(nameof(PlanningPermissions.ProgressSnapshotCreate), "Planning.ProgressSnapshotCreate")]
    [InlineData(nameof(PlanningPermissions.ProgressSnapshotCompare), "Planning.ProgressSnapshotCompare")]
    [InlineData(nameof(PlanningPermissions.ProgressAuditView), "Planning.ProgressAuditView")]
    [InlineData(nameof(PlanningPermissions.ScheduleReschedule), "Planning.ScheduleReschedule")]
    public void Progress_permission_constants(string field, string expected)
    {
        var value = typeof(PlanningPermissions).GetField(field)!.GetValue(null) as string;
        Assert.Equal(expected, value);
    }

    [Fact]
    public void DenyAll_denies()
    {
        Assert.False(new DenyAllPlanningPermissionChecker().Has(PlanningPermissions.ProgressView));
    }

    [Fact]
    public void AllowAll_allows()
    {
        Assert.True(new AllowAllPlanningPermissionChecker().Has(PlanningPermissions.ProgressUpdate));
    }

    [Fact]
    public void HashSet_checker_is_selective()
    {
        var c = new HashSetPlanningPermissionChecker(new[] { PlanningPermissions.ProgressView });
        Assert.True(c.Has(PlanningPermissions.ProgressView));
        Assert.False(c.Has(PlanningPermissions.ProgressUpdate));
    }

    [Fact]
    public void All_progress_permissions_are_unique()
    {
        var perms = new[]
        {
            PlanningPermissions.ProgressView,
            PlanningPermissions.ProgressUpdate,
            PlanningPermissions.ProgressApprove,
            PlanningPermissions.StatusDateManage,
            PlanningPermissions.ActualDatesManage,
            PlanningPermissions.ActualCostManage,
            PlanningPermissions.DelayReasonManage,
            PlanningPermissions.ProgressSnapshotCreate,
            PlanningPermissions.ProgressSnapshotCompare,
            PlanningPermissions.ProgressAuditView,
            PlanningPermissions.ScheduleReschedule
        };
        Assert.Equal(perms.Length, new System.Collections.Generic.HashSet<string>(perms).Count);
    }
}
