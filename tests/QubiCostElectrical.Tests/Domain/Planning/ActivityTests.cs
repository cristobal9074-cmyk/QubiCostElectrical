using System;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public class ActivityTests
{
    [Fact]
    public void Create_ValidActivity_SetsAllProperties()
    {
        var id = ActivityId.New();
        var scheduleId = ScheduleId.New();
        var wbsNodeId = WbsPlanningNodeId.New();
        var duration = new PlanningDuration(5, DurationUnit.WorkDays);
        var activity = new Activity(id, scheduleId, wbsNodeId, "ACT-001", "Excavación", ActivityType.Task, duration, "user1");
        Assert.Equal(id, activity.Id);
        Assert.Equal(scheduleId, activity.ScheduleId);
        Assert.Equal(wbsNodeId, activity.WbsNodeId);
        Assert.Equal("ACT-001", activity.Code);
        Assert.Equal("Excavación", activity.Name);
        Assert.Equal(ActivityType.Task, activity.Type);
        Assert.Equal(5, activity.PlannedDuration.Value);
        Assert.Equal(DurationUnit.WorkDays, activity.PlannedDuration.Unit);
        Assert.Equal(ActivityStatus.NotStarted, activity.Status);
        Assert.Equal("user1", activity.Author);
    }

    [Fact]
    public void CreateActivity_WithoutName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "ACT-001", null!, ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours), "user1"));
    }

    [Fact]
    public void CreateActivity_ValidDuration_Accepted()
    {
        var duration = new PlanningDuration(8, DurationUnit.Hours);
        var activity = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "A1", "Test", ActivityType.Task, duration, "user1");
        Assert.Equal(8, activity.PlannedDuration.Value);
        Assert.Equal(DurationUnit.Hours, activity.PlannedDuration.Unit);
    }

    [Fact]
    public void Duration_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlanningDuration(-1, DurationUnit.Hours));
    }

    [Fact]
    public void Milestone_ZeroDuration_Valid()
    {
        var duration = new PlanningDuration(0, DurationUnit.Hours);
        var activity = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "M1", "Hito", ActivityType.Milestone, duration, "user1");
        Assert.Equal(0, activity.PlannedDuration.Value);
    }

    [Fact]
    public void Milestone_NonZeroDuration_Allowed()
    {
        var duration = new PlanningDuration(1, DurationUnit.Hours);
        var activity = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "M1", "Hito largo", ActivityType.Milestone, duration, "user1");
        Assert.Equal(1, activity.PlannedDuration.Value);
    }

    [Fact]
    public void Calendar_NotAssigned_IsNull()
    {
        var activity = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "A1", "Test", ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours), "user1");
        Assert.Null(activity.CalendarId);
    }

    [Fact]
    public void Activity_AssignResponsible_SetsId()
    {
        var activity = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "A1", "Test", ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours), "user1");
        activity.ResponsibleId = "resp-1";
        Assert.Equal("resp-1", activity.ResponsibleId);
    }

    [Fact]
    public void StatusTransition_NotStartedToReady_IsValid()
    {
        Assert.True(ActivityStatusTransitions.IsValid(ActivityStatus.NotStarted, ActivityStatus.Ready));
    }

    [Fact]
    public void StatusTransition_NotStartedToCompleted_IsInvalid()
    {
        Assert.False(ActivityStatusTransitions.IsValid(ActivityStatus.NotStarted, ActivityStatus.Completed));
    }

    [Fact]
    public void StatusTransition_SameStatus_IsValid()
    {
        Assert.True(ActivityStatusTransitions.IsValid(ActivityStatus.Scheduled, ActivityStatus.Scheduled));
    }
}
