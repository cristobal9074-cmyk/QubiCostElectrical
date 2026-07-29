using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class OutOfSequenceAndRescheduleTests
{
    private static (Activity Pred, Activity Succ, ActivityDependency Dep) FsPair()
    {
        var sid = ScheduleId.New();
        var wbs = WbsPlanningNodeId.New();
        var pred = new Activity(ActivityId.New(), sid, wbs, "P", "Pred", ActivityType.Task,
            new PlanningDuration(5, DurationUnit.WorkDays), "t");
        var succ = new Activity(ActivityId.New(), sid, wbs, "S", "Succ", ActivityType.Task,
            new PlanningDuration(5, DurationUnit.WorkDays), "t");
        var dep = new ActivityDependency
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = sid,
            PredecessorActivityId = pred.Id,
            SuccessorActivityId = succ.Id,
            Type = DependencyType.FinishToStart,
            Lag = new PlanningDuration(0, DurationUnit.Hours),
            IsLead = false
        };
        return (pred, succ, dep);
    }

    [Fact]
    public void Detects_FS_out_of_sequence()
    {
        var (pred, succ, dep) = FsPair();
        var status = new DateTime(2026, 1, 10);
        var map = new Dictionary<string, ActivityProgressUpdate>
        {
            [succ.Id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = succ.ScheduleId, ActivityId = succ.Id,
                StatusDate = status, ActualStart = status, PercentComplete = 10,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var warnings = OutOfSequenceDetector.Detect(status, new[] { pred, succ }, new[] { dep }, map);
        Assert.Contains(warnings, w => w.Kind == OutOfSequenceKind.SuccessorStartedBeforeFsPredecessorComplete);
    }

    [Fact]
    public void Detects_SS_out_of_sequence()
    {
        var (pred, succ, _) = FsPair();
        var dep = new ActivityDependency
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = pred.ScheduleId,
            PredecessorActivityId = pred.Id,
            SuccessorActivityId = succ.Id,
            Type = DependencyType.StartToStart,
            Lag = new PlanningDuration(0, DurationUnit.Hours)
        };
        var status = new DateTime(2026, 1, 10);
        var map = new Dictionary<string, ActivityProgressUpdate>
        {
            [succ.Id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = succ.ScheduleId, ActivityId = succ.Id,
                StatusDate = status, ActualStart = status, PercentComplete = 5,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var warnings = OutOfSequenceDetector.Detect(status, new[] { pred, succ }, new[] { dep }, map);
        Assert.Contains(warnings, w => w.Kind == OutOfSequenceKind.SuccessorStartedBeforeSsPredecessorStart);
    }

    [Fact]
    public void Detects_FF_out_of_sequence()
    {
        var (pred, succ, _) = FsPair();
        var dep = new ActivityDependency
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = pred.ScheduleId,
            PredecessorActivityId = pred.Id,
            SuccessorActivityId = succ.Id,
            Type = DependencyType.FinishToFinish,
            Lag = new PlanningDuration(0, DurationUnit.Hours)
        };
        var status = new DateTime(2026, 1, 10);
        var map = new Dictionary<string, ActivityProgressUpdate>
        {
            [succ.Id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = succ.ScheduleId, ActivityId = succ.Id,
                StatusDate = status, ActualStart = status.AddDays(-5), ActualFinish = status, PercentComplete = 100,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var warnings = OutOfSequenceDetector.Detect(status, new[] { pred, succ }, new[] { dep }, map);
        Assert.Contains(warnings, w => w.Kind == OutOfSequenceKind.FinishBeforeFfPredecessorFinish);
    }

    [Fact]
    public void Detects_constraint_violation()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "C", "C",
            ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "t")
        {
            ConstraintType = ConstraintType.StartNoEarlierThan,
            ConstraintDate = new DateTime(2026, 1, 15)
        };
        var status = new DateTime(2026, 1, 20);
        var map = new Dictionary<string, ActivityProgressUpdate>
        {
            [act.Id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
                StatusDate = status, ActualStart = new DateTime(2026, 1, 10), PercentComplete = 10,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var warnings = OutOfSequenceDetector.Detect(status, new[] { act }, Array.Empty<ActivityDependency>(), map);
        Assert.Contains(warnings, w => w.Kind == OutOfSequenceKind.StartedBeforeConstraint);
    }

    [Fact]
    public void Detects_non_working_day()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "N", "N",
            ActivityType.Task, new PlanningDuration(1, DurationUnit.WorkDays), "t");
        var status = new DateTime(2026, 1, 10);
        var map = new Dictionary<string, ActivityProgressUpdate>
        {
            [act.Id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
                StatusDate = status, ActualStart = new DateTime(2026, 1, 4), // Sunday
                PercentComplete = 5,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var warnings = OutOfSequenceDetector.Detect(status, new[] { act }, Array.Empty<ActivityDependency>(), map,
            isWorkingDay: d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
        Assert.Contains(warnings, w => w.Kind == OutOfSequenceKind.WorkOnNonWorkingDay);
    }

    [Fact]
    public void Reschedule_moves_not_started_to_status_date()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "R", "R",
            ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "t")
        {
            PlannedStart = new DateTime(2026, 1, 1),
            PlannedFinish = new DateTime(2026, 1, 8)
        };
        var status = new DateTime(2026, 2, 1);
        var result = ProgressRescheduleEngine.RescheduleRemaining(status, new[] { act },
            new Dictionary<string, ActivityProgressUpdate>());
        var updated = result.UpdatedActivities.Single();
        Assert.Equal(status, updated.PlannedStart);
        Assert.Contains(act.Id.Value, result.ChangedActivityIds);
    }

    [Fact]
    public void Reschedule_keeps_completed_actuals()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "C", "C",
            ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "t")
        {
            PlannedStart = new DateTime(2026, 1, 1),
            PlannedFinish = new DateTime(2026, 1, 8)
        };
        var prog = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 20), PercentComplete = 100,
            ActualStart = new DateTime(2026, 1, 1), ActualFinish = new DateTime(2026, 1, 7),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        var result = ProgressRescheduleEngine.RescheduleRemaining(new DateTime(2026, 2, 1), new[] { act },
            new Dictionary<string, ActivityProgressUpdate> { [act.Id.Value] = prog });
        Assert.Equal(prog.ActualStart, result.UpdatedProgress[act.Id.Value].ActualStart);
        Assert.DoesNotContain(act.Id.Value, result.ChangedActivityIds);
    }

    [Fact]
    public void Reschedule_updates_in_progress_remaining()
    {
        var act = new Activity(ActivityId.New(), ScheduleId.New(), WbsPlanningNodeId.New(), "I", "I",
            ActivityType.Task, new PlanningDuration(10, DurationUnit.WorkDays), "t")
        {
            PlannedStart = new DateTime(2026, 1, 1),
            PlannedFinish = new DateTime(2026, 1, 15)
        };
        var prog = new ActivityProgressUpdate
        {
            ProgressUpdateId = "1", ScheduleId = act.ScheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 10), PercentComplete = 40, ActualStart = new DateTime(2026, 1, 1),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
        var result = ProgressRescheduleEngine.RescheduleRemaining(new DateTime(2026, 1, 10), new[] { act },
            new Dictionary<string, ActivityProgressUpdate> { [act.Id.Value] = prog });
        Assert.NotNull(result.UpdatedProgress[act.Id.Value].RemainingDuration);
        Assert.Equal(6, result.UpdatedProgress[act.Id.Value].RemainingDuration!.Value);
    }

    [Fact]
    public void Comparison_detects_newly_completed()
    {
        var id = ActivityId.New();
        var sid = ScheduleId.New();
        var left = new Dictionary<string, ActivityProgressUpdate>
        {
            [id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = sid, ActivityId = id, StatusDate = DateTime.Today,
                PercentComplete = 50, ProgressStatus = ProgressTrackingStatus.InProgress,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var right = new Dictionary<string, ActivityProgressUpdate>
        {
            [id.Value] = new ActivityProgressUpdate
            {
                ProgressUpdateId = "1", ScheduleId = sid, ActivityId = id, StatusDate = DateTime.Today,
                PercentComplete = 100, ProgressStatus = ProgressTrackingStatus.Completed,
                CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
            }
        };
        var cmp = ProgressComparisonEngine.Compare("A", "B", DateTime.Today, DateTime.Today, left, right);
        Assert.Contains(id.Value, cmp.NewlyCompletedActivityIds);
        Assert.Equal(50m, cmp.ActivityDiffs.Single().PercentCompleteDelta);
    }

    [Fact]
    public void Snapshot_hash_is_stable_and_verifiable()
    {
        var sid = ScheduleId.New();
        var actId = ActivityId.New();
        var items = new List<ProgressSnapshotItem>
        {
            new()
            {
                SnapshotId = "s1",
                ActivityId = actId,
                Progress = new ActivityProgressUpdate
                {
                    ProgressUpdateId = "p1", ScheduleId = sid, ActivityId = actId, StatusDate = new DateTime(2026, 1, 15),
                    PercentComplete = 25, PhysicalProgress = 25, FinancialProgress = 10,
                    CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
                },
                IsCritical = true
            }
        };
        var snap = new ProgressSnapshot
        {
            SnapshotId = "s1", ScheduleId = sid, Version = 0, StatusDate = new DateTime(2026, 1, 15),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", Comment = "c",
            CriticalPathActivityIds = new[] { actId.Value },
            Summary = new ScheduleProgressSummary { ScheduleId = sid, StatusDate = new DateTime(2026, 1, 15), TotalActivities = 1, Completed = 0, GlobalPhysicalProgress = 25 }
        };
        var hash1 = ProgressSnapshotHasher.ComputeHash(snap, items);
        var hash2 = ProgressSnapshotHasher.ComputeHash(snap, items);
        Assert.Equal(hash1, hash2);
        var sealedSnap = new ProgressSnapshot
        {
            SnapshotId = snap.SnapshotId, ScheduleId = snap.ScheduleId, Version = snap.Version,
            StatusDate = snap.StatusDate, CreatedUtc = snap.CreatedUtc, CreatedBy = snap.CreatedBy,
            Comment = snap.Comment, SHA256 = hash1, Summary = snap.Summary, CriticalPathActivityIds = snap.CriticalPathActivityIds
        };
        Assert.True(ProgressSnapshotHasher.Verify(sealedSnap, items));
    }
}
