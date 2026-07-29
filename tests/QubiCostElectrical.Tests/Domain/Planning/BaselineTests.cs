using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Domain.Planning;

public sealed class BaselineTests
{
    private sealed class FakeBaselineRepo : IScheduleBaselineRepository
    {
        public List<ScheduleBaseline> Baselines { get; } = new();
        public Task<ScheduleBaseline?> GetByIdAsync(string baselineId, CancellationToken ct = default)
            => Task.FromResult(Baselines.FirstOrDefault(b => b.BaselineId == baselineId));
        
        public Task<List<ScheduleBaseline>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Baselines.Where(b => b.ScheduleId == scheduleId).ToList());

        public Task SaveAsync(ScheduleBaseline baseline, CancellationToken ct = default)
        {
            Baselines.RemoveAll(b => b.BaselineId == baseline.BaselineId);
            Baselines.Add(baseline);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string baselineId, CancellationToken ct = default)
        {
            Baselines.RemoveAll(b => b.BaselineId == baselineId);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string baselineId, CancellationToken ct = default)
            => Task.FromResult(Baselines.Any(b => b.BaselineId == baselineId));

        public Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default)
        {
            var max = Baselines.Where(b => b.ScheduleId == scheduleId).Select(b => b.Version).DefaultIfEmpty(-1).Max();
            return Task.FromResult(max + 1);
        }
    }

    private sealed class FakeSnapshotRepo : IScheduleBaselineSnapshotRepository
    {
        public Dictionary<string, ScheduleBaselineSnapshot> Snapshots { get; } = new();
        public Task<ScheduleBaselineSnapshot?> GetByBaselineIdAsync(string baselineId, CancellationToken ct = default)
            => Task.FromResult(Snapshots.TryGetValue(baselineId, out var snapshot) ? snapshot : null);

        public Task SaveAsync(string baselineId, ScheduleBaselineSnapshot snapshot, CancellationToken ct = default)
        {
            Snapshots[baselineId] = snapshot;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string baselineId, CancellationToken ct = default)
        {
            Snapshots.Remove(baselineId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeScheduleRepo : IScheduleRepository
    {
        public List<Schedule> Schedules { get; } = new();
        public Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Schedules.FirstOrDefault(s => s.Id == id));
        public Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default)
            => Task.FromResult(Schedules.ToList());
        public Task SaveAsync(Schedule schedule, CancellationToken ct = default)
        {
            Schedules.RemoveAll(s => s.Id == schedule.Id);
            Schedules.Add(schedule);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(ScheduleId id, CancellationToken ct = default)
        {
            Schedules.RemoveAll(s => s.Id == id);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(ScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Schedules.Any(s => s.Id == id));
    }

    private sealed class FakeActivityRepo : IActivityRepository
    {
        public List<Activity> Activities { get; } = new();
        public Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default)
            => Task.FromResult(Activities.FirstOrDefault(a => a.Id == id));
        public Task<List<Activity>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Activities.Where(a => a.ScheduleId == scheduleId).ToList());
        public Task<List<Activity>> GetByWbsNodeAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default)
            => Task.FromResult(Activities.ToList());
        public Task<ActivitySearchResult> SearchAsync(ActivitySearchRequest request, CancellationToken ct = default)
            => Task.FromResult(new ActivitySearchResult { Activities = Activities.AsReadOnly(), TotalCount = Activities.Count, FilteredCount = Activities.Count });
        public Task SaveAsync(Activity activity, CancellationToken ct = default)
        {
            Activities.RemoveAll(a => a.Id == activity.Id);
            Activities.Add(activity);
            return Task.CompletedTask;
        }
        public Task SaveBatchAsync(List<Activity> list, CancellationToken ct = default)
        {
            foreach (var a in list) SaveAsync(a, ct);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default)
        {
            Activities.RemoveAll(a => a.Id == id);
            return Task.CompletedTask;
        }
        public Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult("ACT-001");
    }

    private sealed class FakeDependencyRepo : IActivityDependencyRepository
    {
        public List<ActivityDependency> Dependencies { get; } = new();
        public Task<ActivityDependency?> GetByIdAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Dependencies.FirstOrDefault(d => d.Id == id));
        public Task<List<ActivityDependency>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Dependencies.Where(d => d.ScheduleId == scheduleId).ToList());
        public Task<List<ActivityDependency>> GetByPredecessorAsync(ActivityId activityId, CancellationToken ct = default)
            => Task.FromResult(Dependencies.Where(d => d.PredecessorActivityId == activityId).ToList());
        public Task<List<ActivityDependency>> GetBySuccessorAsync(ActivityId successorId, CancellationToken ct = default)
            => Task.FromResult(Dependencies.Where(d => d.SuccessorActivityId == successorId).ToList());
        public Task SaveAsync(ActivityDependency dependency, CancellationToken ct = default)
        {
            Dependencies.RemoveAll(d => d.Id == dependency.Id);
            Dependencies.Add(dependency);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            Dependencies.RemoveAll(d => d.Id == id);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(string id, CancellationToken ct = default)
            => Task.FromResult(Dependencies.Any(d => d.Id == id));
    }

    private sealed class FakeCalendarRepo : ICalendarRepository
    {
        public List<Calendar> Calendars { get; } = new();
        public Task<Calendar?> GetByIdAsync(CalendarId id, CancellationToken ct = default)
            => Task.FromResult(Calendars.FirstOrDefault(c => c.Id == id));
        public Task<List<Calendar>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(Calendars.ToList());
        public Task<List<Calendar>> GetByProjectAsync(string projectId, CancellationToken ct = default)
            => Task.FromResult(Calendars.ToList());
        public Task SaveAsync(Calendar calendar, CancellationToken ct = default)
        {
            Calendars.RemoveAll(c => c.Id == calendar.Id);
            Calendars.Add(calendar);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(CalendarId id, CancellationToken ct = default)
        {
            Calendars.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(CalendarId id, CancellationToken ct = default)
            => Task.FromResult(Calendars.Any(c => c.Id == id));
    }

    private sealed class FakeAuditRepo : IPlanningAuditRepository
    {
        public List<PlanningAuditEvent> Events { get; } = new();
        public Task RecordAsync(PlanningAuditEvent auditEvent, CancellationToken ct = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
        public Task<List<PlanningAuditEvent>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.ScheduleId == scheduleId.Value).Take(limit).ToList());
        public Task<List<PlanningAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.EntityType == entityType && e.EntityId == entityId).Take(limit).ToList());
    }

    [Fact]
    public void Deterministic_hashing_returns_same_hash_for_same_snapshot_elements()
    {
        var schedule = new Schedule(new ScheduleId("S1"), "Schedule 1", "Pacific/Easter", new CalendarId("C1"), "Author");
        var act1 = new Activity(new ActivityId("A1"), new ScheduleId("S1"), new WbsPlanningNodeId("W1"), "A1", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var act2 = new Activity(new ActivityId("A2"), new ScheduleId("S1"), new WbsPlanningNodeId("W1"), "A2", "Act B", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "Author");
        
        var snap1 = new ScheduleBaselineSnapshot
        {
            Schedule = schedule,
            Activities = new List<Activity> { act1, act2 },
            Dependencies = new List<ActivityDependency>(),
            Calendars = new List<Calendar>()
        };

        var snap2 = new ScheduleBaselineSnapshot
        {
            Schedule = schedule,
            Activities = new List<Activity> { act2, act1 }, // Reversed order
            Dependencies = new List<ActivityDependency>(),
            Calendars = new List<Calendar>()
        };

        string hash1 = BaselineHasher.ComputeHash(snap1);
        string hash2 = BaselineHasher.ComputeHash(snap2);

        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
    }

    [Fact]
    public async Task CreateBaselineAsync_saves_correctly_and_creates_audit_log()
    {
        var baselineRepo = new FakeBaselineRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        var schedRepo = new FakeScheduleRepo();
        var actRepo = new FakeActivityRepo();
        var depRepo = new FakeDependencyRepo();
        var calRepo = new FakeCalendarRepo();
        var auditRepo = new FakeAuditRepo();

        var service = new ScheduleBaselineService(baselineRepo, snapshotRepo, schedRepo, actRepo, depRepo, calRepo, auditRepo);

        var schedId = new ScheduleId("S1");
        var schedule = new Schedule(schedId, "Schedule 1", "Pacific/Easter", new CalendarId("C1"), "Author");
        schedRepo.Schedules.Add(schedule);

        var act = new Activity(new ActivityId("A1"), schedId, new WbsPlanningNodeId("W1"), "A1", "Act A", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        actRepo.Activities.Add(act);

        var baseline = await service.CreateBaselineAsync(schedId, "B1", "Desc", "Tester");

        Assert.NotNull(baseline);
        Assert.Equal(0, baseline.Version); // First baseline version should be 0
        Assert.Equal("Draft", baseline.Status);
        Assert.False(baseline.IsOfficial);
        
        Assert.Single(baselineRepo.Baselines);
        Assert.Single(snapshotRepo.Snapshots);
        Assert.Single(auditRepo.Events);
        Assert.Equal("CreateBaseline", auditRepo.Events[0].Action);
    }

    [Fact]
    public async Task RestoreBaselineAsync_fails_if_corrupted()
    {
        var baselineRepo = new FakeBaselineRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        var schedRepo = new FakeScheduleRepo();
        var actRepo = new FakeActivityRepo();
        var depRepo = new FakeDependencyRepo();
        var calRepo = new FakeCalendarRepo();
        var auditRepo = new FakeAuditRepo();

        var service = new ScheduleBaselineService(baselineRepo, snapshotRepo, schedRepo, actRepo, depRepo, calRepo, auditRepo);

        var schedId = new ScheduleId("S1");
        var schedule = new Schedule(schedId, "Schedule 1", "Pacific/Easter", new CalendarId("C1"), "Author");
        schedRepo.Schedules.Add(schedule);

        var baseline = await service.CreateBaselineAsync(schedId, "B1", "Desc", "Tester");
        
        // Let's modify the saved snapshot data to cause a mismatch
        var snapshot = snapshotRepo.Snapshots[baseline.BaselineId];
        snapshot.Activities.Add(new Activity(new ActivityId("A2"), schedId, new WbsPlanningNodeId("W1"), "A2", "New Act", ActivityType.Task, new PlanningDuration(1, DurationUnit.WorkDays), "Author"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreBaselineAsync(baseline.BaselineId, "Tester"));
        Assert.Equal("RestoreFailed", auditRepo.Events.Last().Action);
    }

    [Fact]
    public async Task DeleteDraftBaselineAsync_works_for_draft_but_fails_for_official()
    {
        var baselineRepo = new FakeBaselineRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        var schedRepo = new FakeScheduleRepo();
        var actRepo = new FakeActivityRepo();
        var depRepo = new FakeDependencyRepo();
        var calRepo = new FakeCalendarRepo();
        var auditRepo = new FakeAuditRepo();

        var service = new ScheduleBaselineService(baselineRepo, snapshotRepo, schedRepo, actRepo, depRepo, calRepo, auditRepo);
        var schedId = new ScheduleId("S1");
        schedRepo.Schedules.Add(new Schedule(schedId, "Schedule 1", "Pacific/Easter", new CalendarId("C1"), "Author"));

        var baseline = await service.CreateBaselineAsync(schedId, "B1", "Desc", "Tester");

        // Should work on Draft
        await service.DeleteDraftBaselineAsync(baseline.BaselineId);
        Assert.Empty(baselineRepo.Baselines);

        // Create again and mark official
        var baseline2 = await service.CreateBaselineAsync(schedId, "B2", "Desc", "Tester");
        await service.MarkOfficialAsync(baseline2.BaselineId);

        // Delete should throw on official
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftBaselineAsync(baseline2.BaselineId));
    }

    [Fact]
    public async Task CompareBaselinesAsync_detects_added_deleted_and_modified_activities()
    {
        var baselineRepo = new FakeBaselineRepo();
        var snapshotRepo = new FakeSnapshotRepo();
        var schedRepo = new FakeScheduleRepo();
        var actRepo = new FakeActivityRepo();
        var depRepo = new FakeDependencyRepo();
        var calRepo = new FakeCalendarRepo();
        var auditRepo = new FakeAuditRepo();

        var service = new ScheduleBaselineService(baselineRepo, snapshotRepo, schedRepo, actRepo, depRepo, calRepo, auditRepo);
        var schedId = new ScheduleId("S1");
        var schedule = new Schedule(schedId, "Schedule 1", "Pacific/Easter", new CalendarId("C1"), "Author");
        schedRepo.Schedules.Add(schedule);

        // Setup snapshot A (baseline 1)
        var act1 = new Activity(new ActivityId("A1"), schedId, new WbsPlanningNodeId("W1"), "A1", "Act 1", ActivityType.Task, new PlanningDuration(5, DurationUnit.WorkDays), "Author");
        var act2 = new Activity(new ActivityId("A2"), schedId, new WbsPlanningNodeId("W1"), "A2", "Act 2", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays), "Author");
        
        actRepo.Activities.Add(act1);
        actRepo.Activities.Add(act2);

        var baselineA = await service.CreateBaselineAsync(schedId, "B1", "Desc", "Tester");

        // Setup snapshot B (baseline 2)
        actRepo.Activities.Clear();
        // A1 modified duration, A2 deleted, A3 added
        var act1Mod = new Activity(new ActivityId("A1"), schedId, new WbsPlanningNodeId("W1"), "A1", "Act 1", ActivityType.Task, new PlanningDuration(10, DurationUnit.WorkDays), "Author");
        var act3 = new Activity(new ActivityId("A3"), schedId, new WbsPlanningNodeId("W1"), "A3", "Act 3", ActivityType.Task, new PlanningDuration(2, DurationUnit.WorkDays), "Author");
        actRepo.Activities.Add(act1Mod);
        actRepo.Activities.Add(act3);

        var baselineB = await service.CreateBaselineAsync(schedId, "B2", "Desc", "Tester");

        var compResult = await service.CompareBaselinesAsync(baselineA.BaselineId, baselineB.BaselineId);

        Assert.Equal(1, compResult.AddedActivitiesCount);
        Assert.Equal(1, compResult.DeletedActivitiesCount);
        Assert.Equal(1, compResult.ModifiedActivitiesCount);

        var addedDiff = compResult.ActivityDifferences.First(d => d.DiffType == "Added");
        Assert.Equal("A3", addedDiff.Code);

        var deletedDiff = compResult.ActivityDifferences.First(d => d.DiffType == "Deleted");
        Assert.Equal("A2", deletedDiff.Code);

        var modifiedDiff = compResult.ActivityDifferences.First(d => d.DiffType == "Modified");
        Assert.Equal("A1", modifiedDiff.Code);
        Assert.Equal(5.0, modifiedDiff.DurationDiff);
    }
}
