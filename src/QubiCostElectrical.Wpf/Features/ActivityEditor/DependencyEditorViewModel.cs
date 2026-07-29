using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ActivityEditor;

public sealed class DependencyEditorViewModel : ViewModelBase
{
    private readonly WpfAppHost _host;
    private readonly ActivityId _activityId;
    private readonly ScheduleId _scheduleId;

    private readonly ObservableCollection<DependencyRowVm> _activeDependencies = new();
    private readonly ObservableCollection<ActivityComboItem> _availablePredecessors = new();

    private DependencyRowVm? _selectedDependency;
    private ActivityComboItem? _newPredecessor;
    private DependencyType _newType = DependencyType.FinishToStart;
    private string _newLagValue = "0";
    private DurationUnit _newLagUnit = DurationUnit.WorkDays;
    private bool _isLead;

    public DependencyEditorViewModel(WpfAppHost host, ActivityId activityId, ScheduleId scheduleId)
    {
        _host = host;
        _activityId = activityId;
        _scheduleId = scheduleId;

        AddDependencyCommand = new RelayCommand(AddDependency);
        DeleteDependencyCommand = new RelayCommand(DeleteDependency, () => SelectedDependency != null);
        SaveCommand = new RelayCommand(async () => await SaveAsync());

        _ = LoadDataAsync();
    }

    public ObservableCollection<DependencyRowVm> ActiveDependencies => _activeDependencies;
    public ObservableCollection<ActivityComboItem> AvailablePredecessors => _availablePredecessors;

    public DependencyRowVm? SelectedDependency
    {
        get => _selectedDependency;
        set { SetProperty(ref _selectedDependency, value); ((RelayCommand)DeleteDependencyCommand).RaiseCanExecuteChanged(); }
    }

    public ActivityComboItem? NewPredecessor { get => _newPredecessor; set => SetProperty(ref _newPredecessor, value); }
    public DependencyType NewType { get => _newType; set => SetProperty(ref _newType, value); }
    public string NewLagValue { get => _newLagValue; set => SetProperty(ref _newLagValue, value); }
    public DurationUnit NewLagUnit { get => _newLagUnit; set => SetProperty(ref _newLagUnit, value); }
    public bool IsLead { get => _isLead; set => SetProperty(ref _isLead, value); }

    public DependencyType[] DependencyTypes => (DependencyType[])Enum.GetValues(typeof(DependencyType));
    public DurationUnit[] DurationUnits => (DurationUnit[])Enum.GetValues(typeof(DurationUnit));

    public ICommand AddDependencyCommand { get; }
    public ICommand DeleteDependencyCommand { get; }
    public ICommand SaveCommand { get; }

    private async Task LoadDataAsync()
    {
        var allActivities = await _host.Services.ActivityRepository.GetByScheduleAsync(_scheduleId);
        
        // Exclude current activity from being a predecessor
        foreach (var act in allActivities.Where(a => a.Id != _activityId))
        {
            _availablePredecessors.Add(new ActivityComboItem { Id = act.Id, CodeName = $"[{act.Code}] {act.Name}" });
        }

        var deps = await _host.Services.ActivityDependencyRepository.GetBySuccessorAsync(_activityId);
        foreach (var d in deps)
        {
            var predAct = allActivities.FirstOrDefault(a => a.Id == d.PredecessorActivityId);
            var predName = predAct != null ? $"[{predAct.Code}] {predAct.Name}" : d.PredecessorActivityId.Value;
            _activeDependencies.Add(new DependencyRowVm
            {
                Id = d.Id,
                PredecessorId = d.PredecessorActivityId,
                PredecessorCodeName = predName,
                Type = d.Type,
                Lag = d.Lag,
                IsLead = d.IsLead
            });
        }
    }

    private void AddDependency()
    {
        if (NewPredecessor == null) return;
        if (double.TryParse(NewLagValue, out var lag) && lag >= 0)
        {
            var exists = _activeDependencies.Any(d => d.PredecessorId == NewPredecessor.Id);
            if (exists) return; // Prevent duplicate dependency

            _activeDependencies.Add(new DependencyRowVm
            {
                Id = Guid.NewGuid().ToString("N"),
                PredecessorId = NewPredecessor.Id,
                PredecessorCodeName = NewPredecessor.CodeName,
                Type = NewType,
                Lag = new PlanningDuration(lag, NewLagUnit),
                IsLead = IsLead
            });
        }
    }

    private void DeleteDependency()
    {
        if (SelectedDependency != null)
        {
            _activeDependencies.Remove(SelectedDependency);
        }
    }

    private async Task SaveAsync()
    {
        // 1. Delete old dependencies for this activity
        var existing = await _host.Services.ActivityDependencyRepository.GetBySuccessorAsync(_activityId);
        foreach (var e in existing)
        {
            await _host.Services.ActivityDependencyRepository.DeleteAsync(e.Id);
        }

        // 2. Save active dependencies
        foreach (var d in _activeDependencies)
        {
            var dep = new ActivityDependency(
                d.Id,
                _scheduleId,
                d.PredecessorId,
                _activityId,
                d.Type,
                d.Lag,
                "System"
            )
            {
                IsLead = d.IsLead
            };
            await _host.Services.ActivityDependencyRepository.SaveAsync(dep);
        }
    }
}

public sealed class DependencyRowVm
{
    public string Id { get; set; } = null!;
    public ActivityId PredecessorId { get; set; } = null!;
    public string PredecessorCodeName { get; set; } = string.Empty;
    public DependencyType Type { get; set; }
    public PlanningDuration Lag { get; set; } = null!;
    public bool IsLead { get; set; }
    public string LagDescription => (IsLead ? "-" : "") + $"{Lag.Value} {Lag.Unit}";
}

public sealed class ActivityComboItem
{
    public ActivityId Id { get; set; } = null!;
    public string CodeName { get; set; } = string.Empty;
}
