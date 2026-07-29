using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.WbsPlanning;

public sealed class WbsNodeVm
{
    public WbsPlanningNodeId Id { get; init; } = null!;
    public string WbsCode { get; init; } = "";
    public string Name { get; init; } = "";
    public WbsNodeType NodeType { get; init; }
    public int Level { get; init; }
    public ScheduleStatus Status { get; init; }
    public string? Responsible { get; init; }
    public int SortOrder { get; init; }
}

public sealed partial class WbsPlanningViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<WbsNodeVm> nodes = new();
    private WbsNodeVm? selectedNode;
    private bool isEditing;
    private string newNodeCode = "";
    private string newNodeName = "";
    private WbsNodeType newNodeType = WbsNodeType.WorkPackage;
    private string statusMessage = "";
    private bool isLoading;
    private ScheduleId? currentScheduleId;

    public static WbsNodeType[] NodeTypeValues { get; } = (WbsNodeType[])Enum.GetValues(typeof(WbsNodeType));

    public WbsPlanningViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        AddNodeCommand = new RelayCommand(async () => await AddNodeAsync());
        DeleteNodeCommand = new RelayCommand(async () => await DeleteNodeAsync(), () => SelectedNode != null);
        RefreshCommand = new RelayCommand(async () => await LoadAsync());
    }

    public ObservableCollection<WbsNodeVm> Nodes => nodes;
    public WbsNodeVm? SelectedNode { get => selectedNode; set => SetProperty(ref selectedNode, value); }
    public bool IsEditing { get => isEditing; set => SetProperty(ref isEditing, value); }
    public string NewNodeCode { get => newNodeCode; set => SetProperty(ref newNodeCode, value); }
    public string NewNodeName { get => newNodeName; set => SetProperty(ref newNodeName, value); }
    public WbsNodeType NewNodeType { get => newNodeType; set => SetProperty(ref newNodeType, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }

    public ICommand AddNodeCommand { get; }
    public ICommand DeleteNodeCommand { get; }
    public ICommand RefreshCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Reset);
    }
}
