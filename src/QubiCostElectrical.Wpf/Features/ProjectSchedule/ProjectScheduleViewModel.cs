using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Features.ActivityEditor;

namespace QubiCostElectrical.Wpf.Features.ProjectSchedule;

public sealed partial class ProjectScheduleViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<ScheduleSummary> summaries = new();
    private readonly ObservableCollection<ActivityVm> activities = new();
    private readonly ObservableCollection<ActivityVm> criticalPathActivities = new();
    private ScheduleSummary? selectedSummary;
    private bool isEditing;
    private string scheduleName = "";
    private string timeZoneId = "Pacific/Easter";
    private DateTime? projectStartDate;
    private DateTime? targetEndDate;
    private ScheduleStatus status;
    private string statusMessage = "";
    private bool isLoading;

    public static ScheduleStatus[] StatusValues { get; } = (ScheduleStatus[])Enum.GetValues(typeof(ScheduleStatus));

    public ProjectScheduleViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        NewScheduleCommand = new RelayCommand(NewSchedule);
        SaveCommand = new RelayCommand(async () => await SaveAsync());
        DeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => SelectedSummary != null);
        OpenWbsCommand = new RelayCommand(OpenWbs, () => SelectedSummary != null);
        OpenCalendarCommand = new RelayCommand(OpenCalendar, () => SelectedSummary != null);
        RecalculateCpmCommand = new RelayCommand(async () => await RecalculateCpmAsync(), () => SelectedSummary != null);
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
    }

    public ObservableCollection<ScheduleSummary> Summaries => summaries;
    public ObservableCollection<ActivityVm> Activities => activities;
    public ObservableCollection<ActivityVm> CriticalPathActivities => criticalPathActivities;
    public ScheduleSummary? SelectedSummary 
    { 
        get => selectedSummary; 
        set 
        { 
            if (SetProperty(ref selectedSummary, value)) 
            {
                ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)OpenWbsCommand).RaiseCanExecuteChanged();
                ((RelayCommand)OpenCalendarCommand).RaiseCanExecuteChanged();
                ((RelayCommand)RecalculateCpmCommand).RaiseCanExecuteChanged();
                _ = LoadScheduleDetailsAsync();
            }
        } 
    }
    public bool IsEditing { get => isEditing; set => SetProperty(ref isEditing, value); }
    public string ScheduleName { get => scheduleName; set => SetProperty(ref scheduleName, value); }
    public string TimeZoneId { get => timeZoneId; set => SetProperty(ref timeZoneId, value); }
    public DateTime? ProjectStartDate { get => projectStartDate; set => SetProperty(ref projectStartDate, value); }
    public DateTime? TargetEndDate { get => targetEndDate; set => SetProperty(ref targetEndDate, value); }
    public ScheduleStatus Status { get => status; set => SetProperty(ref status, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }

    public ICommand NewScheduleCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenWbsCommand { get; }
    public ICommand OpenCalendarCommand { get; }
    public ICommand RecalculateCpmCommand { get; }
    public ICommand RefreshCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Reset);
    }
}
