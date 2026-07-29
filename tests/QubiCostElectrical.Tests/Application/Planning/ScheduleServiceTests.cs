using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class ScheduleServiceTests
{
    private sealed class FakeScheduleRepo : IScheduleRepository
    {
        public List<Schedule> Schedules { get; } = new();
        public Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Schedules.FirstOrDefault(s => s.Id == id));
        public Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default)
            => Task.FromResult(Schedules.ToList());
        public Task SaveAsync(Schedule schedule, CancellationToken ct = default) { Schedules.RemoveAll(s => s.Id == schedule.Id); Schedules.Add(schedule); return Task.CompletedTask; }
        public Task DeleteAsync(ScheduleId id, CancellationToken ct = default) { Schedules.RemoveAll(s => s.Id == id); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(ScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Schedules.Any(s => s.Id == id));
    }

    private sealed class FakeWbsRepo : IWbsPlanningRepository
    {
        public List<WbsPlanningNode> Nodes { get; } = new();
        public Task<WbsPlanningNode?> GetByIdAsync(WbsPlanningNodeId id, CancellationToken ct = default)
            => Task.FromResult(Nodes.FirstOrDefault(n => n.Id == id));
        public Task<List<WbsPlanningNode>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Nodes.Where(n => n.ScheduleId == scheduleId).ToList());
        public Task SaveAsync(WbsPlanningNode node, CancellationToken ct = default) { Nodes.RemoveAll(n => n.Id == node.Id); Nodes.Add(node); return Task.CompletedTask; }
        public Task SaveBatchAsync(List<WbsPlanningNode> nodes, CancellationToken ct = default) { foreach (var n in nodes) { Nodes.RemoveAll(x => x.Id == n.Id); Nodes.Add(n); } return Task.CompletedTask; }
        public Task DeleteAsync(WbsPlanningNodeId id, CancellationToken ct = default) { Nodes.RemoveAll(n => n.Id == id); return Task.CompletedTask; }
        public Task<bool> CodeExistsAsync(ScheduleId scheduleId, string wbsCode, CancellationToken ct = default)
            => Task.FromResult(Nodes.Any(n => n.ScheduleId == scheduleId && n.WbsCode == wbsCode && !n.IsDeleted));
        public Task<int> GetMaxSortOrderAsync(ScheduleId scheduleId, WbsPlanningNodeId? parentId, CancellationToken ct = default)
            => Task.FromResult(Nodes.Where(n => n.ScheduleId == scheduleId && n.ParentId == parentId).Select(n => n.SortOrder).DefaultIfEmpty(0).Max());
    }

    private sealed class FakeActivityRepo : IActivityRepository
    {
        public List<Activity> Activities { get; } = new();
        public Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default)
            => Task.FromResult(Activities.FirstOrDefault(a => a.Id == id));
        public Task<List<Activity>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
            => Task.FromResult(Activities.Where(a => a.ScheduleId == scheduleId && !a.IsDeleted).ToList());
        public Task<List<Activity>> GetByWbsNodeAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default)
            => Task.FromResult(Activities.Where(a => a.WbsNodeId == wbsNodeId && !a.IsDeleted).ToList());
        public Task<ActivitySearchResult> SearchAsync(ActivitySearchRequest request, CancellationToken ct = default)
        {
            var filtered = Activities.Where(a => !a.IsDeleted).AsEnumerable();
            if (request.ScheduleId != null) filtered = filtered.Where(a => a.ScheduleId == request.ScheduleId);
            if (request.Status.HasValue) filtered = filtered.Where(a => a.Status == request.Status);
            if (request.Type.HasValue) filtered = filtered.Where(a => a.Type == request.Type);
            var list = filtered.ToList();
            return Task.FromResult(new ActivitySearchResult { Activities = list.AsReadOnly(), TotalCount = Activities.Count, FilteredCount = list.Count });
        }
        public Task SaveAsync(Activity activity, CancellationToken ct = default) { Activities.RemoveAll(a => a.Id == activity.Id); Activities.Add(activity); return Task.CompletedTask; }
        public Task SaveBatchAsync(List<Activity> activities, CancellationToken ct = default) { foreach (var a in activities) { Activities.RemoveAll(x => x.Id == a.Id); Activities.Add(a); } return Task.CompletedTask; }
        public Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default) { if (softDelete) { var a = Activities.FirstOrDefault(x => x.Id == id); if (a != null) a.IsDeleted = true; } else Activities.RemoveAll(a => a.Id == id); return Task.CompletedTask; }
        public Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult("0001");
    }

    private sealed class FakeCalendarRepo : ICalendarRepository
    {
        public List<Calendar> Calendars { get; } = new();
        public Task<Calendar?> GetByIdAsync(CalendarId id, CancellationToken ct = default)
            => Task.FromResult(Calendars.FirstOrDefault(c => c.Id == id));
        public Task<List<Calendar>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Calendars.ToList());
        public Task<List<Calendar>> GetByProjectAsync(string projectId, CancellationToken ct = default) => Task.FromResult(Calendars.ToList());
        public Task SaveAsync(Calendar calendar, CancellationToken ct = default) { Calendars.RemoveAll(c => c.Id == calendar.Id); Calendars.Add(calendar); return Task.CompletedTask; }
        public Task DeleteAsync(CalendarId id, CancellationToken ct = default) { Calendars.RemoveAll(c => c.Id == id); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(CalendarId id, CancellationToken ct = default)
            => Task.FromResult(Calendars.Any(c => c.Id == id));
    }

    private sealed class FakeAuditRepo : IPlanningAuditRepository
    {
        public List<PlanningAuditEvent> Events { get; } = new();
        public Task RecordAsync(PlanningAuditEvent auditEvent, CancellationToken ct = default) { Events.Add(auditEvent); return Task.CompletedTask; }
        public Task<List<PlanningAuditEvent>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.ScheduleId == scheduleId.ToString()).Take(limit).ToList());
        public Task<List<PlanningAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult(Events.Where(e => e.EntityType == entityType && e.EntityId == entityId).Take(limit).ToList());
    }

    private readonly FakeScheduleRepo scheduleRepo = new();
    private readonly FakeWbsRepo wbsRepo = new();
    private readonly FakeActivityRepo activityRepo = new();
    private readonly FakeCalendarRepo calendarRepo = new();
    private readonly FakeAuditRepo auditRepo = new();
    private readonly CalendarCalculationService calendarCalc = new();
    private readonly ScheduleService service;

    public ScheduleServiceTests()
    {
        service = new ScheduleService(scheduleRepo, wbsRepo, activityRepo, calendarRepo, auditRepo, calendarCalc);
    }

    [Fact]
    public async Task CreateScheduleAsync_CreatesAndReturnsSchedule()
    {
        var calendarId = CalendarId.New();
        var schedule = await service.CreateScheduleAsync("Test", "America/Santiago", calendarId, "user1");
        Assert.NotNull(schedule);
        Assert.Equal("Test", schedule.Name);
        Assert.Equal("America/Santiago", schedule.TimeZoneId);
        Assert.Equal(calendarId, schedule.DefaultCalendarId);
    }

    [Fact]
    public async Task GetScheduleAsync_ReturnsNull_WhenNotFound()
    {
        var result = await service.GetScheduleAsync(ScheduleId.New());
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateScheduleAsync_ReturnsIssues_WhenNotFound()
    {
        var result = await service.UpdateScheduleAsync(new Schedule(ScheduleId.New(), "N", "UTC", CalendarId.New(), "u"), "u");
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task DeleteScheduleAsync_ReturnsIssues_WhenNotFound()
    {
        var result = await service.DeleteScheduleAsync(ScheduleId.New());
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task CreateWbsNodeAsync_CreatesNode()
    {
        var calId = CalendarId.New();
        var sched = await service.CreateScheduleAsync("Test", "UTC", calId, "user1");
        var node = await service.CreateWbsNodeAsync(sched.Id, "1", "Proyecto", WbsNodeType.Project, null, "user1");
        Assert.NotNull(node);
        Assert.Equal("1", node.WbsCode);
        Assert.Equal("Proyecto", node.Name);
    }

    [Fact]
    public async Task CreateActivityAsync_CreatesActivity()
    {
        var calId = CalendarId.New();
        var sched = await service.CreateScheduleAsync("Test", "UTC", calId, "user1");
        var node = await service.CreateWbsNodeAsync(sched.Id, "1.1", "Fase", WbsNodeType.Phase, null, "user1");
        var duration = new PlanningDuration(5, DurationUnit.WorkDays);
        var activity = await service.CreateActivityAsync(sched.Id, node.Id, "A001", "Excavar", ActivityType.Task, duration, "user1");
        Assert.NotNull(activity);
        Assert.Equal("A001", activity.Code);
        Assert.Equal("Excavar", activity.Name);
    }

    [Fact]
    public async Task Milestone_Create_ForcesZeroDuration()
    {
        var calId = CalendarId.New();
        var sched = await service.CreateScheduleAsync("Test", "UTC", calId, "user1");
        var node = await service.CreateWbsNodeAsync(sched.Id, "1.1", "Fase", WbsNodeType.Phase, null, "user1");
        var activity = await service.CreateActivityAsync(sched.Id, node.Id, "M001", "Hito Final", ActivityType.Milestone, new PlanningDuration(5, DurationUnit.WorkDays), "user1");
        Assert.Equal(0, activity.PlannedDuration.Value);
    }

    [Fact]
    public async Task GetScheduleSummaryAsync_ReturnsCorrectCounts()
    {
        var calId = CalendarId.New();
        var sched = await service.CreateScheduleAsync("Test", "UTC", calId, "user1");
        var node = await service.CreateWbsNodeAsync(sched.Id, "1", "Root", WbsNodeType.Project, null, "user1");
        await service.CreateActivityAsync(sched.Id, node.Id, "A001", "Act1", ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours), "user1");
        var summary = await service.GetScheduleSummaryAsync(sched.Id);
        Assert.Equal(1, summary.TotalWbsNodes);
        Assert.Equal(1, summary.TotalActivities);
    }

    [Fact]
    public async Task ValidateScheduleAsync_EmptyName_ReturnsValidationIssue()
    {
        var calId = CalendarId.New();
        var sched = new Schedule(ScheduleId.New(), "", "UTC", calId, "user1");
        await scheduleRepo.SaveAsync(sched);
        var issues = await service.ValidateScheduleAsync(sched.Id);
        Assert.NotEmpty(issues);
    }

    [Fact]
    public async Task AuditEvent_Recorded_OnScheduleCreate()
    {
        var calId = CalendarId.New();
        await service.CreateScheduleAsync("AuditTest", "UTC", calId, "user1");
        Assert.NotEmpty(auditRepo.Events);
        Assert.Contains(auditRepo.Events, e => e.Action == "Create" && e.EntityType == "Schedule");
    }
}
