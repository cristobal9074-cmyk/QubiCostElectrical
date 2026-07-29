using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ProgressWorkspace;

public sealed class ProgressRowVm
{
    public string ActivityId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal PercentComplete { get; init; }
    public decimal PhysicalProgress { get; init; }
    public decimal FinancialProgress { get; init; }
    public DateTime? PlannedStart { get; init; }
    public DateTime? PlannedFinish { get; init; }
    public DateTime? ActualStart { get; init; }
    public DateTime? ActualFinish { get; init; }
    public DateTime? ForecastStart { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public DateTime? BaselineStart { get; init; }
    public DateTime? BaselineFinish { get; init; }
    public bool IsCritical { get; init; }
    public bool IsSuspended { get; init; }
    public bool HasOutOfSequence { get; init; }
    public string CriticalLabel => IsCritical ? "Crítica" : "Normal";
    public string LayerLabel => $"BL/Plan/Actual/Forecast";
}

public sealed partial class ProgressWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ProgressRowVm> _rows = new();
    private readonly ObservableCollection<ProgressRowVm> _filteredRows = new();

    private DateTime _statusDate = DateTime.Today;
    private string _statusComment = string.Empty;
    private string _statusMessage = string.Empty;
    private string _summaryText = string.Empty;
    private bool _isLoading;
    private bool _isBusy;
    private bool _filterNotStarted;
    private bool _filterInProgress;
    private bool _filterCompleted;
    private bool _filterDelayed;
    private bool _filterAhead;
    private bool _filterCritical;
    private bool _filterBlocked;
    private bool _filterSuspended;
    private bool _filterOos;
    private bool _filterWithCost;
    private bool _filterWithoutUpdate;
    private ProgressRowVm? _selectedRow;
    private ScheduleId? _scheduleId;

    public ProgressWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        SetStatusDateCommand = new RelayCommand(async () => await SetStatusDateAsync(), () => !_isBusy && !IsReadOnly);
        SaveProgressCommand = new RelayCommand(async () => await SaveSelectedProgressAsync(), () => SelectedRow != null && !_isBusy && !IsReadOnly);
        CompleteCommand = new RelayCommand(async () => await CompleteAsync(), () => SelectedRow != null && !_isBusy && !IsReadOnly);
        SuspendCommand = new RelayCommand(async () => await SuspendAsync(), () => SelectedRow != null && !_isBusy && !IsReadOnly);
        ResumeCommand = new RelayCommand(async () => await ResumeAsync(), () => SelectedRow != null && !_isBusy && !IsReadOnly);
        RescheduleCommand = new RelayCommand(async () => await RescheduleAsync(), () => !_isBusy && !IsReadOnly);
        SnapshotCommand = new RelayCommand(async () => await CreateSnapshotAsync(), () => !_isBusy && !IsReadOnly);
        CompareSnapshotsCommand = new RelayCommand(async () => await CompareSnapshotsAsync(), () => !_isBusy);
        RecalculateCommand = new RelayCommand(async () => await RecalculateAsync(), () => !_isBusy);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !_isBusy);
        ApplyFiltersCommand = new RelayCommand(ApplyFilters);
    }

    public ObservableCollection<ProgressRowVm> Rows => _filteredRows;
    public ProgressRowVm? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetProperty(ref _selectedRow, value) && value is not null)
                LoadEditorFromRow(value);
        }
    }

    public DateTime StatusDate
    {
        get => _statusDate;
        set => SetProperty(ref _statusDate, value);
    }

    public string StatusComment
    {
        get => _statusComment;
        set => SetProperty(ref _statusComment, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsReadOnly => _host.Session.IsReadOnly;

    // Editor fields (no formulas — values sent to service)
    public DateTime? EditActualStart { get; set; }
    public DateTime? EditActualFinish { get; set; }
    public decimal EditPercent { get; set; }
    public decimal EditExecutedQuantity { get; set; }
    public decimal EditActualHours { get; set; }
    public decimal EditActualCost { get; set; }
    public decimal EditCommittedCost { get; set; }
    public string EditComment { get; set; } = string.Empty;
    public string EditEvidence { get; set; } = string.Empty;
    public string EditDelayReason { get; set; } = "Other";
    public decimal EditRemainingDays { get; set; }

    public bool FilterNotStarted { get => _filterNotStarted; set { if (SetProperty(ref _filterNotStarted, value)) ApplyFilters(); } }
    public bool FilterInProgress { get => _filterInProgress; set { if (SetProperty(ref _filterInProgress, value)) ApplyFilters(); } }
    public bool FilterCompleted { get => _filterCompleted; set { if (SetProperty(ref _filterCompleted, value)) ApplyFilters(); } }
    public bool FilterDelayed { get => _filterDelayed; set { if (SetProperty(ref _filterDelayed, value)) ApplyFilters(); } }
    public bool FilterAhead { get => _filterAhead; set { if (SetProperty(ref _filterAhead, value)) ApplyFilters(); } }
    public bool FilterCritical { get => _filterCritical; set { if (SetProperty(ref _filterCritical, value)) ApplyFilters(); } }
    public bool FilterBlocked { get => _filterBlocked; set { if (SetProperty(ref _filterBlocked, value)) ApplyFilters(); } }
    public bool FilterSuspended { get => _filterSuspended; set { if (SetProperty(ref _filterSuspended, value)) ApplyFilters(); } }
    public bool FilterOos { get => _filterOos; set { if (SetProperty(ref _filterOos, value)) ApplyFilters(); } }
    public bool FilterWithCost { get => _filterWithCost; set { if (SetProperty(ref _filterWithCost, value)) ApplyFilters(); } }
    public bool FilterWithoutUpdate { get => _filterWithoutUpdate; set { if (SetProperty(ref _filterWithoutUpdate, value)) ApplyFilters(); } }

    public ICommand RefreshCommand { get; }
    public ICommand SetStatusDateCommand { get; }
    public ICommand SaveProgressCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand SuspendCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand RescheduleCommand { get; }
    public ICommand SnapshotCommand { get; }
    public ICommand CompareSnapshotsCommand { get; }
    public ICommand RecalculateCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ApplyFiltersCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsReadOnly));
        _ = LoadAsync();
    }

    private void LoadEditorFromRow(ProgressRowVm row)
    {
        EditActualStart = row.ActualStart;
        EditActualFinish = row.ActualFinish;
        EditPercent = row.PercentComplete;
        EditComment = string.Empty;
        OnPropertyChanged(nameof(EditActualStart));
        OnPropertyChanged(nameof(EditActualFinish));
        OnPropertyChanged(nameof(EditPercent));
    }
}
