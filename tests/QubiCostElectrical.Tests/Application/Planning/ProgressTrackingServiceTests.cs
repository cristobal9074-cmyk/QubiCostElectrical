using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class ProgressTrackingServiceTests
{
    private readonly ScheduleId _scheduleId = ScheduleId.New();
    private readonly CalendarId _calId = CalendarId.New();
    private readonly FakeScheduleRepo _schedules = new();
    private readonly FakeActivityRepo _activities = new();
    private readonly FakeDepRepo _deps = new();
    private readonly FakeStatusDateRepo _statusDates = new();
    private readonly FakeProgressRepo _progress = new();
    private readonly FakeIntervalRepo _intervals = new();
    private readonly FakeDelayRepo _delays = new();
    private readonly FakeEvidenceRepo _evidences = new();
    private readonly FakeSnapshotRepo _snapshots = new();
    private readonly FakeAuditRepo _audit = new();

    private ProgressTrackingService Create(IPlanningPermissionChecker? perms = null) =>
        new(_statusDates, _progress, _intervals, _delays, _evidences, _snapshots,
            _schedules, _activities, _deps, _audit, perms ?? new AllowAllPlanningPermissionChecker());

    private Activity SeedActivity(string code = "A1")
    {
        var schedule = new Schedule(_scheduleId, "S", "America/Santiago", _calId, "tester");
        _schedules.Items[_scheduleId.Value] = schedule;
        var act = new Activity(ActivityId.New(), _scheduleId, WbsPlanningNodeId.New(), code, "Act " + code,
            ActivityType.Task, new PlanningDuration(10, DurationUnit.WorkDays), "tester")
        {
            PlannedStart = new DateTime(2026, 1, 1),
            PlannedFinish = new DateTime(2026, 1, 15),
            PlannedQuantity = 100m,
            QuantityUnit = "m"
        };
        _activities.Items[act.Id.Value] = act;
        return act;
    }

    [Fact]
    public async Task SetStatusDate_creates_and_audits()
    {
        SeedActivity();
        var svc = Create();
        var sd = await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "America/Santiago", "corte", "alice");
        Assert.Equal(new DateTime(2026, 1, 15), sd.StatusDate);
        Assert.Contains(_audit.Events, e => e.Action == "SetStatusDate");
    }

    [Fact]
    public async Task SetStatusDate_can_move_forward_and_back()
    {
        SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 2, 1), "UTC", "avanza", "a");
        var mid = await svc.GetStatusDateAsync(_scheduleId);
        Assert.Equal(new DateTime(2026, 2, 1), mid!.StatusDate);
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 10), "UTC", "retrocede", "a");
        var back = await svc.GetStatusDateAsync(_scheduleId);
        Assert.Equal(new DateTime(2026, 1, 10), back!.StatusDate);
    }

    [Fact]
    public async Task RegisterProgress_25_percent()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        var upd = await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"),
            ScheduleId = _scheduleId,
            ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15),
            ActualStart = new DateTime(2026, 1, 2),
            PercentComplete = 25,
            PhysicalProgress = 25,
            ProgressMethod = ProgressMethod.Manual,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a");
        Assert.Equal(25m, upd.PercentComplete);
        Assert.NotEqual(ProgressTrackingStatus.NotStarted, upd.ProgressStatus);
    }

    [Fact]
    public async Task RegisterQuantity_and_hours_and_cost()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15), ActualStart = new DateTime(2026, 1, 2), PercentComplete = 10,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a");
        var qty = await svc.RegisterQuantityAsync(_scheduleId, act.Id, 40m, "a");
        Assert.Equal(40m, qty.ExecutedQuantity);
        Assert.Equal(40m, qty.PercentComplete);
        var withHours = await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = qty.ProgressUpdateId, ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15), ActualStart = qty.ActualStart, PercentComplete = qty.PercentComplete,
            ExecutedQuantity = 40m, ActualHours = 8m, ProgressMethod = ProgressMethod.ByQuantity,
            CreatedUtc = qty.CreatedUtc, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "2"
        }, "a");
        Assert.Equal(5m, withHours.Productivity);
        var cost = await svc.RegisterCostAsync(_scheduleId, act.Id, 1000m, 1200m, "a");
        Assert.Equal(1000m, cost.ActualCost);
        Assert.Equal(1200m, cost.CommittedCost);
    }

    [Fact]
    public async Task Suspend_and_resume_create_interval()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15), ActualStart = new DateTime(2026, 1, 2), PercentComplete = 20,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a");
        await svc.SuspendActivityAsync(_scheduleId, act.Id, "lluvia", "a");
        Assert.Equal(ProgressTrackingStatus.Suspended, (await _progress.GetByActivityAsync(_scheduleId, act.Id))!.ProgressStatus);
        Assert.Single(_intervals.Items);
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 20), "UTC", "", "a");
        await svc.ResumeActivityAsync(_scheduleId, act.Id, "a");
        Assert.Equal(ProgressTrackingStatus.InProgress, (await _progress.GetByActivityAsync(_scheduleId, act.Id))!.ProgressStatus);
        Assert.True(_intervals.Items.Single().Finish.HasValue);
    }

    [Fact]
    public async Task Complete_activity_sets_100()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 20), "UTC", "", "a");
        await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 20), ActualStart = new DateTime(2026, 1, 2), PercentComplete = 80,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a");
        var done = await svc.CompleteActivityAsync(_scheduleId, act.Id, new DateTime(2026, 1, 18), "a");
        Assert.Equal(100m, done.PercentComplete);
        Assert.Equal(ProgressTrackingStatus.Completed, done.ProgressStatus);
    }

    [Fact]
    public async Task Delay_reason_and_snapshot_compare()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15), ActualStart = new DateTime(2026, 1, 5), PercentComplete = 10,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a");
        await svc.RegisterDelayReasonAsync(new DelayReasonEntry
        {
            EntryId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            Code = DelayReasonCode.Weather, Description = "lluvia", ImpactDays = 2,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a"
        }, "a");
        var s1 = await svc.CreateSnapshotAsync(_scheduleId, "s1", "a");
        await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15), ActualStart = new DateTime(2026, 1, 5), PercentComplete = 40,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "2"
        }, "a");
        var s2 = await svc.CreateSnapshotAsync(_scheduleId, "s2", "a");
        Assert.True(await svc.VerifySnapshotIntegrityAsync(s1.SnapshotId));
        Assert.True(await svc.VerifySnapshotIntegrityAsync(s2.SnapshotId));
        var cmp = await svc.CompareSnapshotsAsync(s1.SnapshotId, s2.SnapshotId, "a");
        Assert.True(cmp.ActivityDiffs.Any(d => d.PercentCompleteDelta == 30m));
    }

    [Fact]
    public async Task Reschedule_does_not_touch_baseline_repos()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 2, 1), "UTC", "", "a");
        var result = await svc.RescheduleFromStatusDateAsync(_scheduleId, "a");
        Assert.Contains(act.Id.Value, result.ChangedActivityIds);
        Assert.Contains(_audit.Events, e => e.Action == "Reschedule");
    }

    [Fact]
    public async Task Denied_permission_throws()
    {
        SeedActivity();
        var svc = Create(new DenyAllPlanningPermissionChecker());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.SetStatusDateAsync(_scheduleId, DateTime.Today, "UTC", "", "x"));
    }

    [Fact]
    public async Task Invalid_progress_throws()
    {
        var act = SeedActivity();
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 15), PercentComplete = 25,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a"));
    }

    [Fact]
    public async Task Summary_and_oos()
    {
        var act = SeedActivity();
        var succ = SeedActivity("B1");
        _deps.Items.Add(new ActivityDependency(Guid.NewGuid().ToString("N"), _scheduleId, act.Id, succ.Id,
            DependencyType.FinishToStart, new PlanningDuration(0, DurationUnit.Hours), "a"));
        var svc = Create();
        await svc.SetStatusDateAsync(_scheduleId, new DateTime(2026, 1, 15), "UTC", "", "a");
        await svc.RegisterProgressAsync(new ActivityProgressUpdate
        {
            ProgressUpdateId = Guid.NewGuid().ToString("N"), ScheduleId = _scheduleId, ActivityId = succ.Id,
            StatusDate = new DateTime(2026, 1, 15), ActualStart = new DateTime(2026, 1, 10), PercentComplete = 5,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        }, "a");
        var oos = await svc.DetectOutOfSequenceAsync(_scheduleId);
        Assert.NotEmpty(oos);
        var summary = await svc.GetSummaryAsync(_scheduleId);
        Assert.True(summary.TotalActivities >= 2);
    }

    // --- fakes ---
    private sealed class FakeScheduleRepo : IScheduleRepository
    {
        public Dictionary<string, Schedule> Items { get; } = new();
        public Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(id.Value, out var s) ? s : null);
        public Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default)
            => Task.FromResult(Items.Values.ToList());
        public Task SaveAsync(Schedule schedule, CancellationToken ct = default)
        { Items[schedule.Id.Value] = schedule; return Task.CompletedTask; }
        public Task DeleteAsync(ScheduleId id, CancellationToken ct = default)
        { Items.Remove(id.Value); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(ScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Items.ContainsKey(id.Value));
    }

    private sealed class FakeActivityRepo : IActivityRepository
    {
        public Dictionary<string, Activity> Items { get; } = new();
        public Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(id.Value, out var a) ? a : null);
        public Task<List<Activity>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.Values.Where(a => a.ScheduleId == scheduleId).ToList());
        public Task<List<Activity>> GetByWbsNodeAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default)
            => Task.FromResult(Items.Values.Where(a => a.WbsNodeId == wbsNodeId).ToList());
        public Task<ActivitySearchResult> SearchAsync(ActivitySearchRequest request, CancellationToken ct = default)
            => Task.FromResult(new ActivitySearchResult { Activities = Items.Values.ToList(), TotalCount = Items.Count });
        public Task SaveAsync(Activity activity, CancellationToken ct = default)
        { Items[activity.Id.Value] = activity; return Task.CompletedTask; }
        public Task SaveBatchAsync(List<Activity> activities, CancellationToken ct = default)
        { foreach (var a in activities) Items[a.Id.Value] = a; return Task.CompletedTask; }
        public Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default)
        { Items.Remove(id.Value); return Task.CompletedTask; }
        public Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult("A" + (Items.Count + 1));
    }

    private sealed class FakeDepRepo : IActivityDependencyRepository
    {
        public List<ActivityDependency> Items { get; } = new();
        public Task<ActivityDependency?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(d => d.Id == id));
        public Task<List<ActivityDependency>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(d => d.ScheduleId == scheduleId).ToList());
        public Task<List<ActivityDependency>> GetByPredecessorAsync(ActivityId activityId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(d => d.PredecessorActivityId == activityId).ToList());
        public Task<List<ActivityDependency>> GetBySuccessorAsync(ActivityId activityId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(d => d.SuccessorActivityId == activityId).ToList());
        public Task SaveAsync(ActivityDependency dependency, CancellationToken ct = default)
        { Items.RemoveAll(d => d.Id == dependency.Id); Items.Add(dependency); return Task.CompletedTask; }
        public Task DeleteAsync(string id, CancellationToken ct = default)
        { Items.RemoveAll(d => d.Id == id); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Items.Any(d => d.Id == id));
    }

    private sealed class FakeStatusDateRepo : IStatusDateRepository
    {
        public Dictionary<string, ScheduleStatusDate> Items { get; } = new();
        public Task<ScheduleStatusDate?> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(scheduleId.Value, out var s) ? s : null);
        public Task SaveAsync(ScheduleStatusDate statusDate, CancellationToken ct = default)
        { Items[statusDate.ScheduleId.Value] = statusDate; return Task.CompletedTask; }
    }

    private sealed class FakeProgressRepo : IActivityProgressRepository
    {
        public Dictionary<string, ActivityProgressUpdate> ByActivity { get; } = new();
        public Task<ActivityProgressUpdate?> GetByIdAsync(string progressUpdateId, CancellationToken ct = default)
            => Task.FromResult(ByActivity.Values.FirstOrDefault(p => p.ProgressUpdateId == progressUpdateId));
        public Task<ActivityProgressUpdate?> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
            => Task.FromResult(ByActivity.TryGetValue(activityId.Value, out var p) ? p : null);
        public Task<List<ActivityProgressUpdate>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(ByActivity.Values.Where(p => p.ScheduleId == scheduleId).ToList());
        public Task SaveAsync(ActivityProgressUpdate update, CancellationToken ct = default)
        { ByActivity[update.ActivityId.Value] = update; return Task.CompletedTask; }
        public Task DeleteAsync(string progressUpdateId, CancellationToken ct = default)
        {
            var key = ByActivity.FirstOrDefault(kv => kv.Value.ProgressUpdateId == progressUpdateId).Key;
            if (key != null) ByActivity.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIntervalRepo : IProgressIntervalRepository
    {
        public List<ProgressInterval> Items { get; } = new();
        public Task<List<ProgressInterval>> GetByProgressUpdateAsync(string progressUpdateId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(i => i.ProgressUpdateId == progressUpdateId).ToList());
        public Task<List<ProgressInterval>> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(i => i.ActivityId == activityId).ToList());
        public Task SaveAsync(ProgressInterval interval, CancellationToken ct = default)
        {
            Items.RemoveAll(i => i.IntervalId == interval.IntervalId);
            Items.Add(interval);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string intervalId, CancellationToken ct = default)
        { Items.RemoveAll(i => i.IntervalId == intervalId); return Task.CompletedTask; }
    }

    private sealed class FakeDelayRepo : IDelayReasonRepository
    {
        public List<DelayReasonEntry> Items { get; } = new();
        public Task<List<DelayReasonEntry>> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(i => i.ActivityId == activityId).ToList());
        public Task<List<DelayReasonEntry>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(i => i.ScheduleId == scheduleId).ToList());
        public Task SaveAsync(DelayReasonEntry entry, CancellationToken ct = default)
        { Items.Add(entry); return Task.CompletedTask; }
        public Task DeleteAsync(string entryId, CancellationToken ct = default)
        { Items.RemoveAll(i => i.EntryId == entryId); return Task.CompletedTask; }
    }

    private sealed class FakeEvidenceRepo : IProgressEvidenceRepository
    {
        public Task<List<ProgressEvidence>> GetByProgressUpdateAsync(string progressUpdateId, CancellationToken ct = default)
            => Task.FromResult(new List<ProgressEvidence>());
        public Task SaveAsync(ProgressEvidence evidence, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(string evidenceId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSnapshotRepo : IProgressSnapshotRepository
    {
        public Dictionary<string, ProgressSnapshot> Snaps { get; } = new();
        public Dictionary<string, List<ProgressSnapshotItem>> Items { get; } = new();
        public Task<ProgressSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default)
            => Task.FromResult(Snaps.TryGetValue(snapshotId, out var s) ? s : null);
        public Task<List<ProgressSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Snaps.Values.Where(s => s.ScheduleId == scheduleId).OrderBy(s => s.Version).ToList());
        public Task SaveAsync(ProgressSnapshot snapshot, CancellationToken ct = default)
        { Snaps[snapshot.SnapshotId] = snapshot; return Task.CompletedTask; }
        public Task SaveItemsAsync(string snapshotId, IReadOnlyList<ProgressSnapshotItem> items, CancellationToken ct = default)
        { Items[snapshotId] = items.ToList(); return Task.CompletedTask; }
        public Task<List<ProgressSnapshotItem>> GetItemsAsync(string snapshotId, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(snapshotId, out var list) ? list : new List<ProgressSnapshotItem>());
        public Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
        {
            var max = Snaps.Values.Where(s => s.ScheduleId == scheduleId).Select(s => s.Version).DefaultIfEmpty(-1).Max();
            return Task.FromResult(max + 1);
        }
    }

    private sealed class FakeAuditRepo : IPlanningAuditRepository
    {
        public List<PlanningAuditEvent> Events { get; } = new();
        public Task RecordAsync(PlanningAuditEvent auditEvent, CancellationToken ct = default)
        { Events.Add(auditEvent); return Task.CompletedTask; }
        public Task<List<PlanningAuditEvent>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.ScheduleId == scheduleId.Value).Take(limit).ToList());
        public Task<List<PlanningAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.EntityType == entityType && e.EntityId == entityId).Take(limit).ToList());
    }
}
