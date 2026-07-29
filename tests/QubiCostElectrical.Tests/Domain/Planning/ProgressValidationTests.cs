using System;
using System.Collections.Generic;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class ProgressValidationTests
{
    private static Activity MakeActivity(ActivityType type = ActivityType.Task, decimal? qty = 100m, string unit = "m")
    {
        var a = new Activity(ActivityId.New(), ScheduleId.Parse(Guid.NewGuid().ToString("N")),
            WbsPlanningNodeId.Parse(Guid.NewGuid().ToString("N")), "A1", "Act", type,
            type == ActivityType.Milestone ? PlanningDuration.Zero(ActivityType.Milestone) : new PlanningDuration(5, DurationUnit.WorkDays),
            "tester");
        a.PlannedQuantity = qty;
        a.QuantityUnit = unit;
        a.PlannedStart = new DateTime(2026, 1, 1);
        a.PlannedFinish = new DateTime(2026, 1, 10);
        return a;
    }

    private static ActivityProgressUpdate MakeUpdate(Activity act, Action<ActivityProgressUpdateBuilder>? cfg = null)
    {
        var b = new ActivityProgressUpdateBuilder(act);
        cfg?.Invoke(b);
        return b.Build();
    }

    private sealed class ActivityProgressUpdateBuilder
    {
        private readonly Activity _act;
        public DateTime StatusDate = new(2026, 1, 15);
        public DateTime? ActualStart;
        public DateTime? ActualFinish;
        public decimal Percent;
        public decimal Physical;
        public decimal? Executed;
        public decimal? PlannedQty;
        public string Unit = "";
        public decimal? ActualCost;
        public decimal? Committed;
        public ProgressTrackingStatus Status = ProgressTrackingStatus.NotStarted;
        public string Comment = "";

        public ActivityProgressUpdateBuilder(Activity act)
        {
            _act = act;
            PlannedQty = act.PlannedQuantity;
            Unit = act.QuantityUnit;
        }

        public ActivityProgressUpdate Build() => new()
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"),
            ScheduleId = _act.ScheduleId,
            ActivityId = _act.Id,
            StatusDate = StatusDate,
            ActualStart = ActualStart,
            ActualFinish = ActualFinish,
            PercentComplete = Percent,
            PhysicalProgress = Physical,
            FinancialProgress = 0m,
            PlannedQuantity = PlannedQty,
            ExecutedQuantity = Executed,
            QuantityUnit = Unit,
            ActualCost = ActualCost,
            CommittedCost = Committed,
            ProgressStatus = Status,
            Comment = Comment,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = "t",
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = "t",
            RowVersion = "1"
        };
    }

    [Fact] public void Percent_negative_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => b.Percent = -1);
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "PCT_RANGE" && i.IsError);
    }

    [Fact] public void Percent_over_100_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.Percent = 101; b.ActualStart = b.StatusDate; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "PCT_RANGE");
    }

    [Fact] public void Finish_without_start_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => b.ActualFinish = b.StatusDate);
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "FINISH_WITHOUT_START");
    }

    [Fact] public void Finish_before_start_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = new DateTime(2026, 1, 10); b.ActualFinish = new DateTime(2026, 1, 5); });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "FINISH_BEFORE_START");
    }

    [Fact] public void Future_finish_vs_status_date_is_error_by_default()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b =>
        {
            b.StatusDate = new DateTime(2026, 1, 10);
            b.ActualStart = new DateTime(2026, 1, 1);
            b.ActualFinish = new DateTime(2026, 1, 20);
        });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "FUTURE_FINISH");
    }

    [Fact] public void Future_finish_allowed_with_policy()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b =>
        {
            b.StatusDate = new DateTime(2026, 1, 10);
            b.ActualStart = new DateTime(2026, 1, 1);
            b.ActualFinish = new DateTime(2026, 1, 20);
        });
        var policy = ProgressPolicy.Default with { AllowFutureActualFinishVsStatusDate = true };
        Assert.DoesNotContain(ProgressValidation.Validate(u, act, policy), i => i.Code == "FUTURE_FINISH" && i.IsError);
    }

    [Fact] public void Progress_without_start_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => b.Percent = 25);
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "PROGRESS_WITHOUT_START");
    }

    [Fact] public void Completed_requires_100_percent()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b =>
        {
            b.ActualStart = new DateTime(2026, 1, 1);
            b.ActualFinish = new DateTime(2026, 1, 10);
            b.Percent = 80;
            b.Status = ProgressTrackingStatus.Completed;
        });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "COMPLETE_PCT");
    }

    [Fact] public void Completed_task_requires_finish()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b =>
        {
            b.ActualStart = new DateTime(2026, 1, 1);
            b.Percent = 100;
            b.Status = ProgressTrackingStatus.Completed;
        });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "COMPLETE_FINISH");
    }

    [Fact] public void Milestone_completed_requires_real_date()
    {
        var act = MakeActivity(ActivityType.Milestone, null, "");
        var u = MakeUpdate(act, b => { b.Percent = 100; b.Status = ProgressTrackingStatus.Completed; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "MILESTONE_DATE");
    }

    [Fact] public void Quantity_negative_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Executed = -1; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "QTY_NEGATIVE");
    }

    [Fact] public void Over_quantity_blocked_without_policy()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Executed = 150; b.PlannedQty = 100; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "QTY_OVER");
    }

    [Fact] public void Over_quantity_allowed_with_policy()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Executed = 150; b.PlannedQty = 100; });
        var policy = ProgressPolicy.Default with { AllowOverProgress = true };
        Assert.DoesNotContain(ProgressValidation.Validate(u, act, policy), i => i.Code == "QTY_OVER" && i.IsError);
    }

    [Fact] public void Unit_mismatch_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Executed = 10; b.Unit = "kg"; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "QTY_UNIT");
    }

    [Fact] public void Negative_cost_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.ActualCost = -5; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "COST_NEGATIVE");
    }

    [Fact] public void Negative_committed_is_error()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Committed = -1; });
        Assert.Contains(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "COMMITTED_NEGATIVE");
    }

    [Fact] public void Negative_remaining_rejected_by_planning_duration()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlanningDuration(-1, DurationUnit.WorkDays));
    }

    [Fact] public void Percent_decrease_requires_comment()
    {
        var act = MakeActivity();
        var prev = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Percent = 50; });
        var next = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Percent = 40; b.Comment = ""; });
        Assert.Contains(ProgressValidation.Validate(next, act, ProgressPolicy.Default, prev), i => i.Code == "PCT_DECREASE");
    }

    [Fact] public void Valid_update_has_no_errors()
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b =>
        {
            b.ActualStart = new DateTime(2026, 1, 2);
            b.Percent = 25;
            b.Physical = 25;
            b.Executed = 25;
        });
        Assert.False(ProgressValidation.HasErrors(ProgressValidation.Validate(u, act, ProgressPolicy.Default)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Physical_range_boundaries(decimal phy)
    {
        var act = MakeActivity();
        var u = MakeUpdate(act, b => { b.ActualStart = b.StatusDate; b.Percent = phy; b.Physical = phy; });
        Assert.DoesNotContain(ProgressValidation.Validate(u, act, ProgressPolicy.Default), i => i.Code == "PHY_RANGE" && i.IsError);
    }
}
