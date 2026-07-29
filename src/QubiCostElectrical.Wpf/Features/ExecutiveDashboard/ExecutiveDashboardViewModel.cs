using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ExecutiveDashboard;

public sealed class DashboardKpiRow
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Interpretation { get; init; } = string.Empty;
    public string Trend { get; init; } = string.Empty;
    public string Icon { get; init; } = "●";
}

public sealed class SCurveBarVm
{
    public string Label { get; init; } = string.Empty;
    public double PvHeight { get; init; }
    public double EvHeight { get; init; }
    public double AcHeight { get; init; }
    public string Tooltip { get; init; } = string.Empty;
}

public sealed partial class ExecutiveDashboardViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<DashboardKpiRow> _kpis = new();
    private readonly ObservableCollection<DashboardAlert> _alerts = new();
    private readonly ObservableCollection<DashboardDeviationItem> _deviations = new();
    private readonly ObservableCollection<DashboardWbsNode> _wbs = new();
    private readonly ObservableCollection<SCurveBarVm> _sCurve = new();
    private readonly ObservableCollection<DashboardSnapshot> _snapshots = new();

    private ScheduleId? _scheduleId;
    private string _statusMessage = string.Empty;
    private string _header = string.Empty;
    private string _summary = string.Empty;
    private string _quality = string.Empty;
    private string _comparisonText = string.Empty;
    private bool _isBusy;
    private DashboardAlert? _selectedAlert;
    private DashboardSnapshot? _snapshotA;
    private DashboardSnapshot? _snapshotB;
    private string _granularity = "Weekly";

    public ExecutiveDashboardViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        SnapshotCommand = new RelayCommand(async () => await CreateSnapshotAsync(), () => !_isBusy && !IsReadOnly);
        CompareCommand = new RelayCommand(async () => await CompareAsync(), () => !_isBusy);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !_isBusy);
        AcknowledgeCommand = new RelayCommand(async () => await AckAsync(), () => SelectedAlert is not null && !IsReadOnly);
        ResolveCommand = new RelayCommand(async () => await ResolveAsync(), () => SelectedAlert is not null && !IsReadOnly);
        DrillDownCommand = new RelayCommand(DrillDown, () => SelectedAlert is not null);
        GoOperationalCommand = new RelayCommand(() => _shell.RequestNavigate(ShellRoute.PlanningOperationalDashboard));
    }

    public ObservableCollection<DashboardKpiRow> Kpis => _kpis;
    public ObservableCollection<DashboardAlert> Alerts => _alerts;
    public ObservableCollection<DashboardDeviationItem> Deviations => _deviations;
    public ObservableCollection<DashboardWbsNode> Wbs => _wbs;
    public ObservableCollection<SCurveBarVm> SCurve => _sCurve;
    public ObservableCollection<DashboardSnapshot> Snapshots => _snapshots;

    public DashboardAlert? SelectedAlert { get => _selectedAlert; set => SetProperty(ref _selectedAlert, value); }
    public DashboardSnapshot? SnapshotA { get => _snapshotA; set => SetProperty(ref _snapshotA, value); }
    public DashboardSnapshot? SnapshotB { get => _snapshotB; set => SetProperty(ref _snapshotB, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string Header { get => _header; private set => SetProperty(ref _header, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string Quality { get => _quality; private set => SetProperty(ref _quality, value); }
    public string ComparisonText { get => _comparisonText; private set => SetProperty(ref _comparisonText, value); }
    public string Granularity { get => _granularity; set { if (SetProperty(ref _granularity, value)) _ = LoadAsync(); } }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand RefreshCommand { get; }
    public ICommand SnapshotCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AcknowledgeCommand { get; }
    public ICommand ResolveCommand { get; }
    public ICommand DrillDownCommand { get; }
    public ICommand GoOperationalCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) => _ = LoadAsync();
}
