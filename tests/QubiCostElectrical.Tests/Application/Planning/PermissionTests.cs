using System;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public class PermissionTests
{
    [Fact]
    public void ScheduleView_PermissionName_IsCorrect()
    {
        Assert.Equal("Planning.ScheduleView", PlanningPermissions.ScheduleView);
    }

    [Fact]
    public void ScheduleCreate_PermissionName_IsCorrect()
    {
        Assert.Equal("Planning.ScheduleCreate", PlanningPermissions.ScheduleCreate);
    }

    [Fact]
    public void WbsPlanningEdit_PermissionName_IsCorrect()
    {
        Assert.Equal("Planning.WbsPlanningEdit", PlanningPermissions.WbsPlanningEdit);
    }

    [Fact]
    public void CalendarManage_PermissionName_IsCorrect()
    {
        Assert.Equal("Planning.CalendarManage", PlanningPermissions.CalendarManage);
    }

    [Fact]
    public void AllPermissions_AreUnique()
    {
        var permissions = new[]
        {
            PlanningPermissions.ScheduleView,
            PlanningPermissions.ScheduleCreate,
            PlanningPermissions.ScheduleEdit,
            PlanningPermissions.ScheduleDelete,
            PlanningPermissions.WbsPlanningEdit,
            PlanningPermissions.ActivityCreate,
            PlanningPermissions.ActivityEdit,
            PlanningPermissions.ActivityDelete,
            PlanningPermissions.CalendarView,
            PlanningPermissions.CalendarManage,
            PlanningPermissions.ConstraintManage,
            PlanningPermissions.ResponsibilityAssign,
            PlanningPermissions.BudgetLinkManage,
            PlanningPermissions.ScheduleAuditView
        };
        Assert.Equal(permissions.Length, new System.Collections.Generic.HashSet<string>(permissions).Count);
    }
}
