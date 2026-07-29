using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.ApuCatalog;

public sealed record ApuCatalogEntryVm
{
    public string ApuId { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Unit { get; init; } = "UN";
    public int VersionCount { get; init; }
    public int ActiveVersionNumber { get; init; }
    public string ActiveVersionStatus { get; init; } = "Draft";
    public decimal LastPrice { get; init; }
    public bool IsActive { get; init; } = true;
    public string Owner { get; init; } = "";
}

public sealed partial class ApuCatalogViewModel : SessionAwareViewModelBase
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly ObservableCollection<ApuCatalogEntryVm> entries = new();
    private ApuCatalogEntryVm? selectedEntry;
    private CancellationTokenSource? searchCts;
    private bool isSearching;
    private string searchText = "";
    private string statusMessage = "";
    private int totalCount;
    private int currentPage;
    private int pageSize = 20;
    private bool hasMore;
    private string? categoryFilter;

    public ApuCatalogViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        SearchCommand = new RelayCommand(async () => await SearchAsync());
        NextPageCommand = new RelayCommand(async () => await LoadPageAsync(currentPage + 1), () => HasMore);
        PreviousPageCommand = new RelayCommand(async () => await LoadPageAsync(currentPage - 1), () => currentPage > 0);
        CompareCommand = new RelayCommand<ApuCatalogEntryVm?>(CompareEntry, e => e != null);
    }

    public ObservableCollection<ApuCatalogEntryVm> Entries => entries;
    public ApuCatalogEntryVm? SelectedEntry { get => selectedEntry; set => SetProperty(ref selectedEntry, value); }
    public bool IsSearching { get => isSearching; private set => SetProperty(ref isSearching, value); }
    public string SearchText { get => searchText; set => SetProperty(ref searchText, value); }
    public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
    public int TotalCount { get => totalCount; private set => SetProperty(ref totalCount, value); }
    public int CurrentPage { get => currentPage; private set => SetProperty(ref currentPage, value); }
    public bool HasMore { get => hasMore; private set => SetProperty(ref hasMore, value); }
    public string? CategoryFilter { get => categoryFilter; set => SetProperty(ref categoryFilter, value); }

    public ICommand SearchCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand CompareCommand { get; }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (e.Kind == ProjectSessionChangeKind.Reset)
            App.Current?.Dispatcher.InvokeAsync(Clear);
    }
}
