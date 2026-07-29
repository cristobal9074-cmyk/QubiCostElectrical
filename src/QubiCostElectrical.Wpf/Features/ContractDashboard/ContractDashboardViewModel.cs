using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ContractDashboard;

public sealed class ContractDashboardKpiRow
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Interpretation { get; init; } = string.Empty;
    public string Trend { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Icon { get; init; } = "●";
}

public sealed class ContractDashboardAlertRow
{
    public string AlertId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Responsible { get; init; } = string.Empty;
    public string CreatedUtc { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
}

public sealed class ContractSnapshotRow
{
    public string SnapshotId { get; init; } = string.Empty;
    public int Version { get; init; }
    public string StatusDate { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string CreatedUtc { get; init; } = string.Empty;
}

public sealed class ContractComparisonResultRow
{
    public string Scope { get; init; } = string.Empty;
    public string LabelA { get; init; } = string.Empty;
    public string LabelB { get; init; } = string.Empty;
    public string AmountChange { get; init; } = string.Empty;
    public string DaysChange { get; init; } = string.Empty;
    public string Findings { get; init; } = string.Empty;
}

public sealed partial class ContractDashboardViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<ContractDashboardKpiRow> _kpis = new();
    private readonly ObservableCollection<ContractDashboardAlertRow> _alerts = new();
    private readonly ObservableCollection<ContractSnapshotRow> _snapshots = new();
    private readonly ObservableCollection<ContractComparisonResultRow> _comparisonResults = new();

    private string _statusMessage = string.Empty;
    private string _header = string.Empty;
    private string _summary = string.Empty;
    private string _quality = string.Empty;
    private string _comparisonText = string.Empty;
    private bool _isBusy;
    private ContractSnapshotRow? _snapshotA;
    private ContractSnapshotRow? _snapshotB;

    public ContractDashboardViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        SnapshotCommand = new RelayCommand(async () => await CreateSnapshotAsync(), () => !_isBusy && !IsReadOnly);
        CompareCommand = new RelayCommand(async () => await CompareAsync(), () => !_isBusy && SnapshotA is not null && SnapshotB is not null);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !_isBusy);
        AcknowledgeCommand = new RelayCommand(async () => await AckAlertAsync(), () => SelectedAlert is not null && !IsReadOnly);
        ResolveCommand = new RelayCommand(async () => await ResolveAlertAsync(), () => SelectedAlert is not null && !IsReadOnly);
    }

    public ObservableCollection<ContractDashboardKpiRow> Kpis => _kpis;
    public ObservableCollection<ContractDashboardAlertRow> Alerts => _alerts;
    public ObservableCollection<ContractSnapshotRow> Snapshots => _snapshots;
    public ObservableCollection<ContractComparisonResultRow> ComparisonResults => _comparisonResults;

    public ContractSnapshotRow? SnapshotA { get => _snapshotA; set { if (SetProperty(ref _snapshotA, value)) UpdateCompareCommand(); } }
    public ContractSnapshotRow? SnapshotB { get => _snapshotB; set { if (SetProperty(ref _snapshotB, value)) UpdateCompareCommand(); } }
    private ContractDashboardAlertRow? _selectedAlert;
    public ContractDashboardAlertRow? SelectedAlert { get => _selectedAlert; set => SetProperty(ref _selectedAlert, value); }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string Header { get => _header; private set => SetProperty(ref _header, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public string Quality { get => _quality; private set => SetProperty(ref _quality, value); }
    public string ComparisonText { get => _comparisonText; private set => SetProperty(ref _comparisonText, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand RefreshCommand { get; }
    public ICommand SnapshotCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AcknowledgeCommand { get; }
    public ICommand ResolveCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) => _ = LoadAsync();

    private void UpdateCompareCommand()
    {
        ((RelayCommand)CompareCommand).RaiseCanExecuteChanged();
    }
}
