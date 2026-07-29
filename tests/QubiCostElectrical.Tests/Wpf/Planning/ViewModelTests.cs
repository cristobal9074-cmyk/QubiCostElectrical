using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf.Planning;

public sealed class ViewModelTests
{
    private sealed class FakeScheduleRepo : IScheduleRepository
    {
        public List<Schedule> Schedules { get; } = new();
        public Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default)
        {
            var found = Schedules.FirstOrDefault(s => s.Id == id);
            if (found == null) return Task.FromResult<Schedule?>(null);
            var clone = new Schedule(ScheduleId.Parse(found.Id.Value), found.Name, found.TimeZoneId, found.DefaultCalendarId, found.CreatedBy);
            clone.Description = found.Description;
            clone.Status = found.Status;
            clone.Version = found.Version;
            clone.ModifiedAt = found.ModifiedAt;
            clone.ModifiedBy = found.ModifiedBy;
            clone.RowVersion = found.RowVersion;
            clone.Remarks = found.Remarks;
            clone.CompatibilityMetadata = found.CompatibilityMetadata;
            clone.ProjectStartDate = found.ProjectStartDate;
            clone.TargetEndDate = found.TargetEndDate;
            return Task.FromResult<Schedule?>(clone);
        }
        public Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default)
            => Task.FromResult(Schedules.ToList());
        public Task SaveAsync(Schedule schedule, CancellationToken ct = default)
        {
            Schedules.RemoveAll(s => s.Id == schedule.Id);
            var clone = new Schedule(ScheduleId.Parse(schedule.Id.Value), schedule.Name, schedule.TimeZoneId, schedule.DefaultCalendarId, schedule.CreatedBy);
            clone.Description = schedule.Description;
            clone.Status = schedule.Status;
            clone.Version = schedule.Version;
            clone.ModifiedAt = schedule.ModifiedAt;
            clone.ModifiedBy = schedule.ModifiedBy;
            clone.RowVersion = schedule.RowVersion;
            clone.Remarks = schedule.Remarks;
            clone.CompatibilityMetadata = schedule.CompatibilityMetadata;
            clone.ProjectStartDate = schedule.ProjectStartDate;
            clone.TargetEndDate = schedule.TargetEndDate;
            Schedules.Add(clone);
            return Task.CompletedTask;
        }
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
            var list = Activities.Where(a => !a.IsDeleted).ToList();
            return Task.FromResult(new ActivitySearchResult { Activities = list.AsReadOnly(), TotalCount = list.Count, FilteredCount = list.Count });
        }
        public Task SaveAsync(Activity activity, CancellationToken ct = default) { Activities.RemoveAll(a => a.Id == activity.Id); Activities.Add(activity); return Task.CompletedTask; }
        public Task SaveBatchAsync(List<Activity> activities, CancellationToken ct = default) { foreach (var a in activities) { Activities.RemoveAll(x => x.Id == a.Id); Activities.Add(a); } return Task.CompletedTask; }
        public Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default) { var a = Activities.FirstOrDefault(x => x.Id == id); if (a != null) a.IsDeleted = softDelete; else Activities.RemoveAll(a => a.Id == id); return Task.CompletedTask; }
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

    private sealed class TestScheduleViewModel : INotifyPropertyChanged
    {
        private readonly IScheduleService _service;
        private Schedule? _schedule;
        private bool _isBusy;
        private bool _isReadOnly;
        private bool _hasPendingChanges;
        private string _validationMessage = string.Empty;
        private bool _commandExecuting;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Schedule? Schedule { get => _schedule; private set { _schedule = value; OnPropertyChanged(); } }
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        public bool IsReadOnly { get => _isReadOnly; set { _isReadOnly = value; OnPropertyChanged(); } }
        public bool HasPendingChanges { get => _hasPendingChanges; set { _hasPendingChanges = value; OnPropertyChanged(); } }
        public string ValidationMessage { get => _validationMessage; set { _validationMessage = value; OnPropertyChanged(); } }

        public TestScheduleViewModel(IScheduleService service)
        {
            _service = service;
        }

        public async Task LoadScheduleAsync(ScheduleId id)
        {
            IsBusy = true;
            try
            {
                Schedule = await _service.GetScheduleAsync(id);
                HasPendingChanges = false;
            }
            finally { IsBusy = false; }
        }

        public async Task<bool> SaveAsync()
        {
            if (IsReadOnly) { ValidationMessage = "Read-only"; return false; }
            if (Schedule == null) { ValidationMessage = "No schedule"; return false; }
            if (_commandExecuting) return false;
            _commandExecuting = true;
            IsBusy = true;
            try
            {
                var issues = await _service.UpdateScheduleAsync(Schedule, Schedule.ModifiedBy);
                if (issues.Count > 0)
                {
                    ValidationMessage = issues[0].Message;
                    return false;
                }
                HasPendingChanges = false;
                ValidationMessage = string.Empty;
                return true;
            }
            finally { IsBusy = false; _commandExecuting = false; }
        }

        public async Task<bool> CreateActivityAsync(string code, string name, ActivityType type, PlanningDuration duration)
        {
            if (IsReadOnly) { ValidationMessage = "Read-only"; return false; }
            if (Schedule == null) { ValidationMessage = "No schedule"; return false; }
            IsBusy = true;
            try
            {
                var nodeId = WbsPlanningNodeId.New();
                var node = await _service.GetWbsTreeAsync(Schedule.Id);
                var parentNodeId = node.FirstOrDefault()?.Id ?? WbsPlanningNodeId.New();
                var activity = await _service.CreateActivityAsync(Schedule.Id, parentNodeId, code, name, type, duration, "user1");
                HasPendingChanges = true;
                return true;
            }
            finally { IsBusy = false; }
        }

        public void MarkDirty() => HasPendingChanges = true;
        public void ResetDirty() => HasPendingChanges = false;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    [Fact]
    public async Task LoadSchedule_ExistingId_LoadsSchedule()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var calId = CalendarId.New();
        var schedule = await svc.CreateScheduleAsync("Test", "UTC", calId, "user1");
        var vm = new TestScheduleViewModel(svc);
        await vm.LoadScheduleAsync(schedule.Id);
        Assert.NotNull(vm.Schedule);
        Assert.Equal("Test", vm.Schedule!.Name);
    }

    [Fact]
    public async Task CreateActivityCommand_CreatesActivity()
    {
        var schedRepo = new FakeScheduleRepo();
        var wbsRepo = new FakeWbsRepo();
        var svc = new ScheduleService(schedRepo, wbsRepo, new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var calId = CalendarId.New();
        var schedule = await svc.CreateScheduleAsync("Test", "UTC", calId, "user1");
        var node = await svc.CreateWbsNodeAsync(schedule.Id, "1", "Root", WbsNodeType.Project, null, "user1");
        var vm = new TestScheduleViewModel(svc);
        await vm.LoadScheduleAsync(schedule.Id);
        var result = await vm.CreateActivityAsync("A001", "Excavar", ActivityType.Task, new PlanningDuration(3, DurationUnit.WorkDays));
        Assert.True(result);
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public async Task EditSchedule_ChangesDirtyState()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var schedule = await svc.CreateScheduleAsync("Test", "UTC", CalendarId.New(), "user1");
        var vm = new TestScheduleViewModel(svc);
        await vm.LoadScheduleAsync(schedule.Id);
        vm.MarkDirty();
        Assert.True(vm.HasPendingChanges);
    }

    [Fact]
    public async Task SaveCommand_PersistsChanges()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var schedule = await svc.CreateScheduleAsync("Original", "UTC", CalendarId.New(), "user1");
        var vm = new TestScheduleViewModel(svc);
        await vm.LoadScheduleAsync(schedule.Id);
        vm.Schedule!.Name = "Modified";
        vm.MarkDirty();
        var success = await vm.SaveAsync();
        Assert.True(success);
        Assert.False(vm.HasPendingChanges);
        var loaded = await svc.GetScheduleAsync(schedule.Id);
        Assert.Equal("Modified", loaded!.Name);
    }

    [Fact]
    public async Task CancelCommand_DoesNotPersist()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var schedule = await svc.CreateScheduleAsync("Preserve", "UTC", CalendarId.New(), "user1");
        var vm = new TestScheduleViewModel(svc);
        await vm.LoadScheduleAsync(schedule.Id);
        vm.Schedule!.Name = "Changed";
        vm.ResetDirty();
        Assert.False(vm.HasPendingChanges);
        var loaded = await schedRepo.GetByIdAsync(schedule.Id);
        Assert.Equal("Preserve", loaded!.Name);
    }

    [Fact]
    public async Task NonReentrantCommand_PreventsDoubleExecution()
    {
        var vm = new TestScheduleViewModel(null!);
        var result = await vm.SaveAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task ValidationMessage_ShownOnError()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var vm = new TestScheduleViewModel(svc);
        vm.MarkDirty();
        var result = await vm.SaveAsync();
        Assert.False(result);
        Assert.NotEmpty(vm.ValidationMessage);
    }

    [Fact]
    public async Task BlockedByReadOnly_ReturnsFalse()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var schedule = await svc.CreateScheduleAsync("RO", "UTC", CalendarId.New(), "user1");
        var vm = new TestScheduleViewModel(svc);
        await vm.LoadScheduleAsync(schedule.Id);
        vm.IsReadOnly = true;
        var result = await vm.CreateActivityAsync("A1", "Test", ActivityType.Task, new PlanningDuration(1, DurationUnit.Hours));
        Assert.False(result);
    }

    [Fact]
    public async Task ViewModel_TracksBusyState()
    {
        var schedRepo = new FakeScheduleRepo();
        var svc = new ScheduleService(schedRepo, new FakeWbsRepo(), new FakeActivityRepo(), new FakeCalendarRepo(), new FakeAuditRepo(), new CalendarCalculationService());
        var schedule = await svc.CreateScheduleAsync("Busy", "UTC", CalendarId.New(), "user1");
        var vm = new TestScheduleViewModel(svc);
        var loadTask = vm.LoadScheduleAsync(schedule.Id);
        Assert.False(vm.IsBusy);
        await loadTask;
        Assert.NotNull(vm.Schedule);
    }
}
