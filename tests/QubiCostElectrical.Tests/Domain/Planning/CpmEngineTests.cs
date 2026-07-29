using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class CpmEngineTests
{
    private sealed class FakeCpmCalendarCalculator : ICpmCalendarCalculator
    {
        public DateTime AddDuration(DateTime start, PlanningDuration duration, CalendarId? calendarId)
        {
            if (duration.Unit == DurationUnit.WorkDays)
                return start.AddDays(duration.Value);
            if (duration.Unit == DurationUnit.Hours)
                return start.AddHours(duration.Value);
            return start.AddMinutes(duration.Value);
        }

        public DateTime SubtractDuration(DateTime start, PlanningDuration duration, CalendarId? calendarId)
        {
            if (duration.Unit == DurationUnit.WorkDays)
                return start.AddDays(-duration.Value);
            if (duration.Unit == DurationUnit.Hours)
                return start.AddHours(-duration.Value);
            return start.AddMinutes(-duration.Value);
        }

        public PlanningDuration SubtractDates(DateTime start, DateTime end, CalendarId? calendarId)
        {
            var diff = (end - start).TotalDays;
            return new PlanningDuration(Math.Max(0, diff), DurationUnit.WorkDays);
        }
    }

    private readonly FakeCpmCalendarCalculator _calculator = new();
    private readonly CpmCalculationEngine _engine = new();

    [Fact]
    public void KahnAlgorithm_topological_sort_is_deterministic()
    {
        var start = new DateTime(2026, 1, 1);
        var act1 = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var act2 = new Activity(new ActivityId("B"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "B", "Act B", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "Author");
        
        var activities = new List<Activity> { act2, act1 };
        var dependencies = new List<ActivityDependency>(); // No dependencies

        var result = _engine.Calculate(start, null, activities, dependencies, _calculator);

        Assert.True(result.Success);
        Assert.Equal(2, result.CalculatedDates.Count);
        Assert.Equal(start, result.CalculatedDates[act1.Id].EarlyStart);
        Assert.Equal(start.AddDays(5), result.CalculatedDates[act1.Id].EarlyFinish);
        Assert.Equal(start, result.CalculatedDates[act2.Id].EarlyStart);
        Assert.Equal(start.AddDays(3), result.CalculatedDates[act2.Id].EarlyFinish);
    }

    [Fact]
    public void KahnAlgorithm_detects_simple_cycle()
    {
        var start = new DateTime(2026, 1, 1);
        var act1 = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var act2 = new Activity(new ActivityId("B"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "B", "Act B", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "Author");

        var dep1 = new ActivityDependency("D1", new ScheduleId("S"), act1.Id, act2.Id, DependencyType.FinishToStart, new PlanningDuration(0, DurationUnit.WorkDays), "Author");
        var dep2 = new ActivityDependency("D2", new ScheduleId("S"), act2.Id, act1.Id, DependencyType.FinishToStart, new PlanningDuration(0, DurationUnit.WorkDays), "Author");

        var activities = new List<Activity> { act1, act2 };
        var dependencies = new List<ActivityDependency> { dep1, dep2 };

        var result = _engine.Calculate(start, null, activities, dependencies, _calculator);

        Assert.False(result.Success);
        Assert.Contains("Ciclo detectado", result.ErrorMessage);
        Assert.True(result.CyclePath.Contains("A") && result.CyclePath.Contains("B"));
    }

    [Fact]
    public void KahnAlgorithm_detects_self_reference_cycle()
    {
        var start = new DateTime(2026, 1, 1);
        var act1 = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var dep = new ActivityDependency("D1", new ScheduleId("S"), act1.Id, act1.Id, DependencyType.FinishToStart, new PlanningDuration(0, DurationUnit.WorkDays), "Author");

        var result = _engine.Calculate(start, null, new List<Activity> { act1 }, new List<ActivityDependency> { dep }, _calculator);

        Assert.False(result.Success);
        Assert.Contains("Ciclo detectado", result.ErrorMessage);
    }

    [Fact]
    public void Forward_backward_passes_for_linear_network()
    {
        var start = new DateTime(2026, 1, 1);
        var act1 = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var act2 = new Activity(new ActivityId("B"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "B", "Act B", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "Author");
        var dep = new ActivityDependency("D1", new ScheduleId("S"), act1.Id, act2.Id, DependencyType.FinishToStart, new PlanningDuration(2, DurationUnit.WorkDays), "Author");

        var result = _engine.Calculate(start, null, new List<Activity> { act1, act2 }, new List<ActivityDependency> { dep }, _calculator);

        Assert.True(result.Success);
        var dateA = result.CalculatedDates[act1.Id];
        var dateB = result.CalculatedDates[act2.Id];

        Assert.Equal(start, dateA.EarlyStart);
        Assert.Equal(start.AddDays(5), dateA.EarlyFinish);

        // B starts at A's EF (5 days) + 2 days lag = 7 days
        Assert.Equal(start.AddDays(7), dateB.EarlyStart);
        Assert.Equal(start.AddDays(10), dateB.EarlyFinish);

        // Backward pass
        Assert.Equal(start.AddDays(10), dateB.LateFinish);
        Assert.Equal(start.AddDays(7), dateB.LateStart);
        Assert.Equal(start.AddDays(5), dateA.LateFinish);
        Assert.Equal(start, dateA.LateStart);

        Assert.Equal(0, dateA.TotalFloat);
        Assert.Equal(0, dateB.TotalFloat);
        Assert.True(dateA.IsCritical);
        Assert.True(dateB.IsCritical);
    }

    [Fact]
    public void Lag_and_lead_offsets_calculate_correctly()
    {
        var start = new DateTime(2026, 1, 1);
        var act1 = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var act2 = new Activity(new ActivityId("B"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "B", "Act B", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "Author");
        
        // Lead (negative lag) of 1 day
        var dep = new ActivityDependency("D1", new ScheduleId("S"), act1.Id, act2.Id, DependencyType.FinishToStart, new PlanningDuration(1, DurationUnit.WorkDays), "Author")
        {
            IsLead = true
        };

        var result = _engine.Calculate(start, null, new List<Activity> { act1, act2 }, new List<ActivityDependency> { dep }, _calculator);

        Assert.True(result.Success);
        var dateA = result.CalculatedDates[act1.Id];
        var dateB = result.CalculatedDates[act2.Id];

        // B starts at A's EF (5 days) - 1 day lead = 4 days
        Assert.Equal(start.AddDays(4), dateB.EarlyStart);
        Assert.Equal(start.AddDays(7), dateB.EarlyFinish);
    }

    [Fact]
    public void Basic_constraints_start_no_earlier_than_forces_later_start()
    {
        var start = new DateTime(2026, 1, 1);
        var act = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author")
        {
            ConstraintType = ConstraintType.StartNoEarlierThan,
            ConstraintDate = start.AddDays(3)
        };

        var result = _engine.Calculate(start, null, new List<Activity> { act }, new List<ActivityDependency>(), _calculator);

        Assert.True(result.Success);
        var dateA = result.CalculatedDates[act.Id];

        Assert.Equal(start.AddDays(3), dateA.EarlyStart);
        Assert.Equal(start.AddDays(8), dateA.EarlyFinish);
    }

    [Fact]
    public void Must_finish_on_constraint_forces_early_finish()
    {
        var start = new DateTime(2026, 1, 1);
        var act = new Activity(new ActivityId("A"), new ScheduleId("S"), new WbsPlanningNodeId("W"), "A", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author")
        {
            ConstraintType = ConstraintType.MustFinishOn,
            ConstraintDate = start.AddDays(10)
        };

        var result = _engine.Calculate(start, null, new List<Activity> { act }, new List<ActivityDependency>(), _calculator);

        Assert.True(result.Success);
        var dateA = result.CalculatedDates[act.Id];

        Assert.Equal(start.AddDays(10), dateA.EarlyFinish);
        Assert.Equal(start.AddDays(5), dateA.EarlyStart);
    }
}
