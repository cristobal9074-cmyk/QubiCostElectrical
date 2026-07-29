using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ScenarioWorkspace;

public sealed record ScenarioVm
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string ScenarioType { get; set; } = "Custom";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public decimal DirectCost { get; set; }
    public decimal IndirectCost { get; set; }
    public decimal Utility { get; set; }
    public decimal Taxes { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public decimal VariationPct { get; set; }
    public bool IsCalculated { get; set; }
    public bool IsBase { get; set; }
}

public sealed partial class ScenarioWorkspaceViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<ScenarioVm> scenarios = new();
    private ScenarioVm? selectedScenario;
    private CancellationTokenSource? calculationCts;
    private bool isCalculating;
    private string statusMessage = "";
    private ScenarioVm? baseScenario;

    public ScenarioWorkspaceViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        CreateCommand = new RelayCommand(CreateScenario);
        DuplicateCommand = new RelayCommand<ScenarioVm?>(DuplicateScenario, s => s != null && !s.IsBase);
        DeleteCommand = new RelayCommand<ScenarioVm?>(DeleteScenario, s => s != null && !s.IsBase);
        CalculateCommand = new RelayCommand(async () => await CalculateAsync(), () => !IsCalculating);
        ApplyToBaseCommand = new RelayCommand(async () => await ApplyToBaseAsync(), () => SelectedScenario != null && !SelectedScenario.IsBase);
        CompareWithBaseCommand = new RelayCommand<ScenarioVm?>(CompareWithBase, s => s != null && !s.IsBase);
    }

    public ObservableCollection<ScenarioVm> Scenarios => scenarios;
    public ScenarioVm? SelectedScenario { get => selectedScenario; set => SetProperty(ref selectedScenario, value); }
    public bool IsCalculating { get => isCalculating; private set => SetProperty(ref isCalculating, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public ScenarioVm? BaseScenario { get => baseScenario; private set => SetProperty(ref baseScenario, value); }

    public ICommand CreateCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CalculateCommand { get; }
    public ICommand ApplyToBaseCommand { get; }
    public ICommand CompareWithBaseCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Clear);
    }
}
