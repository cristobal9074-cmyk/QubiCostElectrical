using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ActivityEditor;

public sealed class ActivityVm
{
    public ActivityId Id { get; init; } = null!;
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public ActivityType Type { get; init; }
    public ActivityStatus Status { get; init; }
    public string? ResponsibleId { get; init; }
    public CalendarId? CalendarId { get; init; }
    public PlanningDuration PlannedDuration { get; init; } = null!;
    public DateTime? PlannedStart { get; init; }
    public DateTime? PlannedFinish { get; init; }
    public decimal PlannedProgressPct { get; init; }
    public DateTime? EarlyStart { get; init; }
    public DateTime? EarlyFinish { get; init; }
    public DateTime? LateStart { get; init; }
    public DateTime? LateFinish { get; init; }
    public double? TotalFloat { get; init; }
    public double? FreeFloat { get; init; }
    public bool IsCritical { get; init; }
    public double GanttLeftOffset { get; set; }
    public double GanttWidth { get; set; }
    public double GanttFloatWidth { get; set; }
    public string Author { get; init; } = "";
}

public sealed partial class ActivityEditorViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<ActivityVm> activities = new();
    private readonly ObservableCollection<Calendar> calendars = new();
    private ActivityVm? selectedActivity;
    private bool isEditing;
    private string activityCode = "";
    private string activityName = "";
    private ActivityType activityType;
    private double durationValue = 1;
    private DurationUnit durationUnit = DurationUnit.Hours;
    private CalendarId? selectedCalendarId;
    private string responsibleId = "";
    private ActivityStatus status;
    private string searchText = "";
    private string statusMessage = "";
    private bool isLoading;
    private ScheduleId? currentScheduleId;

    public static ActivityType[] ActivityTypeValues { get; } = (ActivityType[])Enum.GetValues(typeof(ActivityType));
    public static ActivityStatus[] ActivityStatusValues { get; } = (ActivityStatus[])Enum.GetValues(typeof(ActivityStatus));
    public static DurationUnit[] DurationUnitValues { get; } = (DurationUnit[])Enum.GetValues(typeof(DurationUnit));

    public ActivityEditorViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        NewActivityCommand = new RelayCommand(NewActivity);
        SaveActivityCommand = new RelayCommand(async () => await SaveActivityAsync());
        DeleteActivityCommand = new RelayCommand(async () => await DeleteActivityAsync(), () => SelectedActivity != null);
        EditDependenciesCommand = new RelayCommand(EditDependencies, () => SelectedActivity != null);
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
    }

    public ObservableCollection<ActivityVm> Activities => activities;
    public ObservableCollection<Calendar> Calendars => calendars;
    public ActivityVm? SelectedActivity { get => selectedActivity; set => SetProperty(ref selectedActivity, value); }
    public bool IsEditing { get => isEditing; set => SetProperty(ref isEditing, value); }
    public string ActivityCode { get => activityCode; set => SetProperty(ref activityCode, value); }
    public string ActivityName { get => activityName; set => SetProperty(ref activityName, value); }
    public ActivityType ActivityType { get => activityType; set => SetProperty(ref activityType, value); }
    public double DurationValue { get => durationValue; set => SetProperty(ref durationValue, value); }
    public DurationUnit DurationUnit { get => durationUnit; set => SetProperty(ref durationUnit, value); }
    public CalendarId? SelectedCalendarId { get => selectedCalendarId; set => SetProperty(ref selectedCalendarId, value); }
    public string ResponsibleId { get => responsibleId; set => SetProperty(ref responsibleId, value); }
    public ActivityStatus Status { get => status; set => SetProperty(ref status, value); }
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }

    public ICommand NewActivityCommand { get; }
    public ICommand SaveActivityCommand { get; }
    public ICommand DeleteActivityCommand { get; }
    public ICommand EditDependenciesCommand { get; }
    public ICommand RefreshCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Reset);
    }
}
