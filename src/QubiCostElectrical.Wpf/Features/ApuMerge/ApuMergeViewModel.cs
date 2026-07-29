using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ApuMerge;

public sealed record ConflictVm
{
    public string Dimension { get; init; } = "";
    public string LocalValue { get; init; } = "";
    public string CatalogValue { get; init; } = "";
    public string BaseValue { get; init; } = "";
    public string Resolution { get; set; } = "KeepLocal";
}

public sealed record DiffVm
{
    public string Dimension { get; init; } = "";
    public string LocalValue { get; init; } = "";
    public string CatalogValue { get; init; } = "";
    public string Impact { get; init; } = "";
}

public sealed partial class ApuMergeViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<ConflictVm> conflicts = new();
    private readonly ObservableCollection<DiffVm> diffs = new();
    private string apuId = "";
    private string code = "";
    private string statusMessage = "";
    private bool isReady;

    public ApuMergeViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        ResolveKeepLocalCommand = new RelayCommand<ConflictVm?>(ResolveKeepLocal, c => c != null);
        ResolveAcceptCatalogCommand = new RelayCommand<ConflictVm?>(ResolveAcceptCatalog, c => c != null);
        ResolveKeepBaseCommand = new RelayCommand<ConflictVm?>(ResolveKeepBase, c => c != null);
        CreateMergeVersionCommand = new RelayCommand(async () => await CreateMergeVersionAsync(), () => IsReady);
        CancelCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.ApuCatalog));
    }

    public ObservableCollection<ConflictVm> Conflicts => conflicts;
    public ObservableCollection<DiffVm> Diffs => diffs;
    public string ApuId { get => apuId; private set => SetProperty(ref apuId, value); }
    public string Code { get => code; private set => SetProperty(ref code, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public bool IsReady { get => isReady; private set => SetProperty(ref isReady, value); }

    public ICommand ResolveKeepLocalCommand { get; }
    public ICommand ResolveAcceptCatalogCommand { get; }
    public ICommand ResolveKeepBaseCommand { get; }
    public ICommand CreateMergeVersionCommand { get; }
    public ICommand CancelCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            CancelCommand.Execute(null);
    }
}
