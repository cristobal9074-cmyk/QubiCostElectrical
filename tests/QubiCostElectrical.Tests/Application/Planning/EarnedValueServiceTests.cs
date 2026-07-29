using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class EarnedValueServiceTests
{
    private readonly ScheduleId _sid = ScheduleId.New();
    private readonly CalendarId _cal = CalendarId.New();
    private readonly FakeScheduleRepo _schedules = new();
    private readonly FakeActivityRepo _activities = new();
    private readonly FakeProgressRepo _progress = new();
    private readonly FakeStatusDateRepo _status = new();
    private readonly FakeBaselineRepo _baselines = new();
    private readonly FakeEvmRepo _evm = new();
    private readonly FakeEvmSnapRepo _snaps = new();
    private readonly FakeEvmHistRepo _hist = new();
    private readonly FakeAuditRepo _audit = new();

    private EarnedValueService Create(IPlanningPermissionChecker? perms = null)
    {
        _schedules.Items[_sid.Value] = new Schedule(_sid, "S", "UTC", _cal, "t");
        return new EarnedValueService(_evm, _snaps, _hist, _schedules, _activities, _progress, _status, _baselines,
            _audit, perms ?? new AllowAllPlanningPermissionChecker());
    }

    private Activity SeedAct()
    {
        var act = new Activity(ActivityId.New(), _sid, WbsPlanningNodeId.New(), "A1", "Act",
            ActivityType.Task, new PlanningDuration(10, DurationUnit.WorkDays), "t")
        {
            PlannedStart = new DateTime(2026, 1, 1),
            PlannedFinish = new DateTime(2026, 1, 21)
        };
        _activities.Items[act.Id.Value] = act;
        return act;
    }

    [Fact]
    public async Task Calculate_requires_status_date()
    {
        SeedAct();
        var svc = Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CalculateAsync(_sid, "a", 1000m));
    }

    [Fact]
    public async Task Calculate_persists_latest_and_history()
    {
        var act = SeedAct();
        _status.Items[_sid.Value] = new ScheduleStatusDate
        {
            Id = "1", ScheduleId = _sid, StatusDate = new DateTime(2026, 1, 11),
            TimeZoneId = "UTC", UpdatedUtc = DateTime.UtcNow, UpdatedBy = "a", RowVersion = "1"
        };
        _baselines.Items.Add(new ScheduleBaseline
        {
            BaselineId = "b1", ScheduleId = _sid, Version = 0, IsOfficial = true,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", SHA256 = "x", SnapshotDate = DateTime.UtcNow
        });
        _progress.ByActivity[act.Id.Value] = new ActivityProgressUpdate
        {
            ProgressUpdateId = "p", ScheduleId = _sid, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 11), PercentComplete = 40m, ActualCost = 100m,
            ActualStart = new DateTime(2026, 1, 2),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        };
        var svc = Create();
        var result = await svc.CalculateAsync(_sid, "alice", 1000m);
        Assert.True(result.Indicators.Bac == 1000m);
        Assert.NotNull(await svc.GetLatestAsync(_sid));
        Assert.NotEmpty(await svc.GetHistoryAsync(_sid));
        Assert.Contains(_audit.Events, e => e.Action == "Calculate");
    }

    [Fact]
    public async Task Snapshot_and_compare()
    {
        var act = SeedAct();
        _status.Items[_sid.Value] = new ScheduleStatusDate
        {
            Id = "1", ScheduleId = _sid, StatusDate = new DateTime(2026, 1, 11),
            TimeZoneId = "UTC", UpdatedUtc = DateTime.UtcNow, UpdatedBy = "a", RowVersion = "1"
        };
        _baselines.Items.Add(new ScheduleBaseline
        {
            BaselineId = "b1", ScheduleId = _sid, Version = 0, IsOfficial = true,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", SHA256 = "x", SnapshotDate = DateTime.UtcNow
        });
        _progress.ByActivity[act.Id.Value] = new ActivityProgressUpdate
        {
            ProgressUpdateId = "p", ScheduleId = _sid, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 11), PercentComplete = 20m, ActualCost = 50m,
            ActualStart = new DateTime(2026, 1, 2),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "1"
        };
        var svc = Create();
        await svc.CalculateAsync(_sid, "a", 1000m);
        var s1 = await svc.CreateSnapshotAsync(_sid, "s1", "a");
        _progress.ByActivity[act.Id.Value] = new ActivityProgressUpdate
        {
            ProgressUpdateId = "p", ScheduleId = _sid, ActivityId = act.Id,
            StatusDate = new DateTime(2026, 1, 11), PercentComplete = 50m, ActualCost = 200m,
            ActualStart = new DateTime(2026, 1, 2),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "a", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "a", RowVersion = "2"
        };
        await svc.CalculateAsync(_sid, "a", 1000m);
        var s2 = await svc.CreateSnapshotAsync(_sid, "s2", "a");
        Assert.True(await svc.VerifySnapshotIntegrityAsync(s1.SnapshotId));
        var cmp = await svc.CompareSnapshotsAsync(s1.SnapshotId, s2.SnapshotId, "a");
        Assert.True(cmp.EvDelta != 0m || cmp.AcDelta != 0m);
    }

    [Fact]
    public async Task Denied_permission()
    {
        SeedAct();
        var svc = Create(new DenyAllPlanningPermissionChecker());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CalculateAsync(_sid, "x"));
    }

    // fakes
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
        public Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult("A1");
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
        public Task DeleteAsync(string progressUpdateId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStatusDateRepo : IStatusDateRepository
    {
        public Dictionary<string, ScheduleStatusDate> Items { get; } = new();
        public Task<ScheduleStatusDate?> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(scheduleId.Value, out var s) ? s : null);
        public Task SaveAsync(ScheduleStatusDate statusDate, CancellationToken ct = default)
        { Items[statusDate.ScheduleId.Value] = statusDate; return Task.CompletedTask; }
    }

    private sealed class FakeBaselineRepo : IScheduleBaselineRepository
    {
        public List<ScheduleBaseline> Items { get; } = new();
        public Task<ScheduleBaseline?> GetByIdAsync(string baselineId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(b => b.BaselineId == baselineId));
        public Task<List<ScheduleBaseline>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.Where(b => b.ScheduleId == scheduleId).ToList());
        public Task SaveAsync(ScheduleBaseline baseline, CancellationToken ct = default)
        { Items.Add(baseline); return Task.CompletedTask; }
        public Task DeleteAsync(string baselineId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string baselineId, CancellationToken ct = default)
            => Task.FromResult(Items.Any(b => b.BaselineId == baselineId));
        public Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeEvmRepo : IEarnedValueRepository
    {
        public EarnedValueResult? Latest;
        public Task SaveLatestAsync(EarnedValueResult result, CancellationToken ct = default)
        { Latest = result; return Task.CompletedTask; }
        public Task<EarnedValueResult?> GetLatestAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Latest);
    }

    private sealed class FakeEvmSnapRepo : IEarnedValueSnapshotRepository
    {
        public Dictionary<string, EarnedValueSnapshot> Items { get; } = new();
        public Task SaveAsync(EarnedValueSnapshot snapshot, CancellationToken ct = default)
        { Items[snapshot.SnapshotId] = snapshot; return Task.CompletedTask; }
        public Task<EarnedValueSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default)
            => Task.FromResult(Items.TryGetValue(snapshotId, out var s) ? s : null);
        public Task<List<EarnedValueSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.Values.Where(s => s.ScheduleId == scheduleId).OrderBy(s => s.Version).ToList());
        public Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Items.Values.Where(s => s.ScheduleId == scheduleId).Select(s => s.Version).DefaultIfEmpty(-1).Max() + 1);
    }

    private sealed class FakeEvmHistRepo : IEarnedValueHistoryRepository
    {
        public List<EarnedValueHistoryEntry> Items { get; } = new();
        public Task AppendAsync(EarnedValueHistoryEntry entry, CancellationToken ct = default)
        { Items.Add(entry); return Task.CompletedTask; }
        public Task<List<EarnedValueHistoryEntry>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(Items.Where(i => i.ScheduleId == scheduleId).Take(limit).ToList());
    }

    private sealed class FakeAuditRepo : IPlanningAuditRepository
    {
        public List<PlanningAuditEvent> Events { get; } = new();
        public Task RecordAsync(PlanningAuditEvent auditEvent, CancellationToken ct = default)
        { Events.Add(auditEvent); return Task.CompletedTask; }
        public Task<List<PlanningAuditEvent>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.ScheduleId == scheduleId.Value).Take(limit).ToList());
        public Task<List<PlanningAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult(Events.Take(limit).ToList());
    }
}
