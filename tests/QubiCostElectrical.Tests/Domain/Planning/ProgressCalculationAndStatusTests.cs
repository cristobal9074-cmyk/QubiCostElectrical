using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class ProgressCalculationAndStatusTests
{
    private static Activity TaskAct(double days = 10)
    {
        var a = new Activity(ActivityId.New(), new ScheduleId(Guid.NewGuid().ToString("N")),
            new WbsPlanningNodeId(Guid.NewGuid().ToString("N")), "T1", "Task", ActivityType.Task,
            new PlanningDuration(days, DurationUnit.WorkDays), "t");
        a.PlannedStart = new DateTime(2026, 1, 1);
        a.PlannedFinish = new DateTime(2026, 1, 15);
        a.PlannedQuantity = 100m;
        a.QuantityUnit = "m";
        return a;
    }

    [Fact] public void ClampPercent_limits()
    {
        Assert.Equal(0m, ProgressCalculationEngine.ClampPercent(-5));
        Assert.Equal(100m, ProgressCalculationEngine.ClampPercent(150));
        Assert.Equal(33.3333m, ProgressCalculationEngine.ClampPercent(33.33333m));
    }

    [Fact] public void Percent_by_duration()
    {
        var planned = new PlanningDuration(10, DurationUnit.WorkDays);
        var rem = new PlanningDuration(5, DurationUnit.WorkDays);
        Assert.Equal(50m, ProgressCalculationEngine.CalculatePercentByDuration(planned, rem));
    }

    [Fact] public void Percent_by_quantity()
    {
        Assert.Equal(25m, ProgressCalculationEngine.CalculatePercentByQuantity(100m, 25m));
    }

    [Fact] public void Percent_manual_method()
    {
        var pct = ProgressCalculationEngine.ResolvePercentComplete(ProgressMethod.Manual, 40m, null, null, null, null);
        Assert.Equal(40m, pct);
    }

    [Fact] public void Percent_by_quantity_method()
    {
        var pct = ProgressCalculationEngine.ResolvePercentComplete(ProgressMethod.ByQuantity, 0m, null, null, 200m, 50m);
        Assert.Equal(25m, pct);
    }

    [Fact] public void Physical_by_quantity_caps_without_over()
    {
        Assert.Equal(100m, ProgressCalculationEngine.CalculatePhysicalByQuantity(100m, 150m, false));
    }

    [Fact] public void Productivity_and_variance()
    {
        var p = ProgressCalculationEngine.CalculateProductivity(100m, 10m);
        Assert.Equal(10m, p);
        Assert.Equal(2m, ProgressCalculationEngine.ProductivityVariance(10m, 8m));
    }

    [Fact] public void Financial_from_costs()
    {
        Assert.Equal(50m, ProgressCalculationEngine.CalculateFinancialFromCosts(500m, null, 1000m, false));
    }

    [Fact] public void Remaining_quantity()
    {
        Assert.Equal(70m, ProgressCalculationEngine.RemainingQuantity(100m, 30m));
        Assert.Equal(0m, ProgressCalculationEngine.RemainingQuantity(100m, 120m));
    }

    [Fact] public void Status_not_started()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2025, 12, 1), PercentComplete = 0,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(ProgressTrackingStatus.NotStarted, ProgressStatusDeriver.Derive(u, act, u.StatusDate));
    }

    [Fact] public void Status_delayed_when_past_planned_start_without_start()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 2, 1), PercentComplete = 0,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(ProgressTrackingStatus.Delayed, ProgressStatusDeriver.Derive(u, act, u.StatusDate));
    }

    [Fact] public void Status_completed()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 20), PercentComplete = 100,
            ActualStart = new DateTime(2026, 1, 1), ActualFinish = new DateTime(2026, 1, 18),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(ProgressTrackingStatus.Completed, ProgressStatusDeriver.Derive(u, act, u.StatusDate));
    }

    [Fact] public void Status_suspended_explicit()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 10), PercentComplete = 20, ActualStart = new DateTime(2026, 1, 2),
            ProgressStatus = ProgressTrackingStatus.Suspended,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(ProgressTrackingStatus.Suspended, ProgressStatusDeriver.Derive(u, act, u.StatusDate, isExplicitlySuspended: true));
    }

    [Fact] public void Remaining_zero_when_completed()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 20), PercentComplete = 100,
            ActualFinish = new DateTime(2026, 1, 18), ActualStart = new DateTime(2026, 1, 1),
            ProgressStatus = ProgressTrackingStatus.Completed,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(0, RemainingDurationCalculator.CalculateRemaining(act, u, u.StatusDate).Value);
    }

    [Fact] public void Remaining_equals_planned_when_not_started()
    {
        var act = TaskAct(8);
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 5), PercentComplete = 0,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(8, RemainingDurationCalculator.CalculateRemaining(act, u, u.StatusDate).Value);
    }

    [Fact] public void Remaining_scales_with_percent()
    {
        var act = TaskAct(10);
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 10), PercentComplete = 40, ActualStart = new DateTime(2026, 1, 1),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        Assert.Equal(6, RemainingDurationCalculator.CalculateRemaining(act, u, u.StatusDate).Value);
    }

    [Fact] public void Forecast_not_started_moves_to_status_date()
    {
        var act = TaskAct();
        var status = new DateTime(2026, 2, 1);
        var (start, finish) = ProgressForecastEngine.ForecastActivity(act, null, status);
        Assert.Equal(status, start);
        Assert.NotNull(finish);
    }

    [Fact] public void Forecast_completed_keeps_actuals()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 20), PercentComplete = 100,
            ActualStart = new DateTime(2026, 1, 1), ActualFinish = new DateTime(2026, 1, 12),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        var (s, f) = ProgressForecastEngine.ForecastActivity(act, u, u.StatusDate);
        Assert.Equal(u.ActualStart, s);
        Assert.Equal(u.ActualFinish, f);
    }

    [Fact] public void Summary_weighted_by_duration()
    {
        var act = TaskAct(10);
        var sid = act.ScheduleId;
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = sid, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 10), PercentComplete = 50, PhysicalProgress = 50, FinancialProgress = 20,
            ActualStart = new DateTime(2026, 1, 1),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        var map = new Dictionary<string, ActivityProgressUpdate> { [act.Id.Value] = u };
        var summary = ScheduleProgressSummaryCalculator.Calculate(sid, u.StatusDate, new[] { act }, map, ProgressPolicy.Default);
        Assert.Equal(50m, summary.GlobalPhysicalProgress);
        Assert.Equal(1, summary.TotalActivities);
    }

    [Fact] public void Result_builder_includes_variances()
    {
        var act = TaskAct();
        var u = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 10), PercentComplete = 30, PhysicalProgress = 30,
            ActualStart = new DateTime(2026, 1, 3),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        var result = ActivityProgressResultBuilder.Build(act, u, u.StatusDate, act.PlannedStart, act.PlannedFinish);
        Assert.Equal(2, result.StartVarianceDays); // Jan 3 vs Jan 1
        Assert.Equal(30m, result.PercentComplete);
    }
}
