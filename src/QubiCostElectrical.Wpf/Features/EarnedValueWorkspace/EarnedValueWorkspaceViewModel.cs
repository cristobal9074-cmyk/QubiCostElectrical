using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.EarnedValueWorkspace;

public sealed class EvmIndicatorRow
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Light { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed partial class EarnedValueWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost _shell;
    private readonly WpfAppHost _host;
    private readonly ObservableCollection<EvmIndicatorRow> _indicators = new();
    private readonly ObservableCollection<EarnedValueHistoryEntry> _history = new();
    private readonly ObservableCollection<EarnedValueSnapshot> _snapshots = new();

    private ScheduleId? _scheduleId;
    private string _statusMessage = string.Empty;
    private string _explanation = string.Empty;
    private string _comparisonText = string.Empty;
    private string _bacInput = string.Empty;
    private bool _isBusy;
    private EarnedValueSnapshot? _selectedSnapshotA;
    private EarnedValueSnapshot? _selectedSnapshotB;

    public EarnedValueWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !_isBusy);
        CalculateCommand = new RelayCommand(async () => await CalculateAsync(), () => !_isBusy && !IsReadOnly);
        SnapshotCommand = new RelayCommand(async () => await CreateSnapshotAsync(), () => !_isBusy && !IsReadOnly);
        CompareCommand = new RelayCommand(async () => await CompareAsync(), () => !_isBusy && SelectedSnapshotA != null && SelectedSnapshotB != null);
        ExportCommand = new RelayCommand(async () => await ExportAsync(), () => !_isBusy);
    }

    public ObservableCollection<EvmIndicatorRow> Indicators => _indicators;
    public ObservableCollection<EarnedValueHistoryEntry> History => _history;
    public ObservableCollection<EarnedValueSnapshot> Snapshots => _snapshots;

    public EarnedValueSnapshot? SelectedSnapshotA
    {
        get => _selectedSnapshotA;
        set => SetProperty(ref _selectedSnapshotA, value);
    }

    public EarnedValueSnapshot? SelectedSnapshotB
    {
        get => _selectedSnapshotB;
        set => SetProperty(ref _selectedSnapshotB, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string Explanation { get => _explanation; private set => SetProperty(ref _explanation, value); }
    public string ComparisonText { get => _comparisonText; private set => SetProperty(ref _comparisonText, value); }
    public string BacInput { get => _bacInput; set => SetProperty(ref _bacInput, value); }
    public bool IsReadOnly => _host.Session.IsReadOnly;

    public ICommand RefreshCommand { get; }
    public ICommand CalculateCommand { get; }
    public ICommand SnapshotCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand ExportCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) => _ = LoadAsync();
}
