using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.CommandPalette;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Coordina la búsqueda rápida, Command Palette (Ctrl+K) y Spotlight de acciones en el Shell.
/// Gestiona filtros por categoría, resultados, navegación por teclado y ejecución de comandos.
/// </summary>
public sealed class ShellCommandPaletteCoordinator : ObservableObject
{
    private readonly BudgetProjectSession session;
    private readonly IShellNavigationHost navigationHost;
    private readonly IPriceBankStore priceBankStore;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly WpfUndoService undo;
    private readonly Action<string>? onShellPropertyChanged;

    private string searchText = string.Empty;
    private bool isSearchPaletteOpen;
    private CommandPaletteCategory selectedPaletteCategory = CommandPaletteCategory.All;
    private ShellViewModel.QuickSearchItemVm? selectedQuickSearchItem;
    private readonly ObservableCollection<ShellViewModel.QuickSearchItemVm> quickSearchResults = new();
    private CommandPaletteEngine? commandPaletteEngine;

    public ShellCommandPaletteCoordinator(
        BudgetProjectSession session,
        IShellNavigationHost navigationHost,
        IPriceBankStore priceBankStore,
        IProjectWorkspaceService projectWorkspace,
        WpfUndoService undo,
        Action<string>? onShellPropertyChanged = null)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.navigationHost = navigationHost ?? throw new ArgumentNullException(nameof(navigationHost));
        this.priceBankStore = priceBankStore ?? throw new ArgumentNullException(nameof(priceBankStore));
        this.projectWorkspace = projectWorkspace ?? throw new ArgumentNullException(nameof(projectWorkspace));
        this.undo = undo ?? throw new ArgumentNullException(nameof(undo));
        this.onShellPropertyChanged = onShellPropertyChanged;

        ClearSearchCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ClearSearch);
        SetPaletteCategoryCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(SetPaletteCategory);
        ExecuteQuickSearchItemCommand = new AsyncRelayCommand<ShellViewModel.QuickSearchItemVm>(
            ExecuteQuickSearchItemAsync,
            CanExecuteQuickSearchItem);
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            Notify(nameof(SearchText));
            Notify(nameof(HasSearchText));
            RefreshQuickSearchResults();
            IsSearchPaletteOpen = true;
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(searchText);
    public bool HasQuickSearchResults => quickSearchResults.Count > 0;
    public string ResultCountLabel => quickSearchResults.Count == 1 ? "1 coincidencia" : $"{quickSearchResults.Count} coincidencias";

    public bool IsSearchPaletteOpen
    {
        get => isSearchPaletteOpen;
        set
        {
            if (SetProperty(ref isSearchPaletteOpen, value))
            {
                Notify(nameof(IsSearchPaletteOpen));
            }
        }
    }

    public CommandPaletteCategory SelectedPaletteCategory
    {
        get => selectedPaletteCategory;
        set
        {
            if (SetProperty(ref selectedPaletteCategory, value))
            {
                Notify(nameof(SelectedPaletteCategory));
                RefreshQuickSearchResults();
            }
        }
    }

    public ObservableCollection<ShellViewModel.QuickSearchItemVm> QuickSearchResults => quickSearchResults;

    public ShellViewModel.QuickSearchItemVm? SelectedQuickSearchItem
    {
        get => selectedQuickSearchItem;
        set
        {
            if (!SetProperty(ref selectedQuickSearchItem, value))
                return;
            Notify(nameof(SelectedQuickSearchItem));
            ExecuteQuickSearchItemCommand.NotifyCanExecuteChanged();
        }
    }

    public IRelayCommand ClearSearchCommand { get; }
    public IRelayCommand<object> SetPaletteCategoryCommand { get; }
    public IAsyncRelayCommand<ShellViewModel.QuickSearchItemVm> ExecuteQuickSearchItemCommand { get; }

    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    public void SetPaletteCategory(object? param)
    {
        if (param is CommandPaletteCategory cat)
            SelectedPaletteCategory = cat;
        else if (param is string str && Enum.TryParse<CommandPaletteCategory>(str, true, out var parsed))
            SelectedPaletteCategory = parsed;
    }

    public void OpenQuickSearch()
    {
        RefreshQuickSearchResults();
        IsSearchPaletteOpen = true;
    }

    public void CloseQuickSearch()
    {
        IsSearchPaletteOpen = false;
    }

    public async Task ExecuteQuickSearchAsync()
    {
        var item = SelectedQuickSearchItem ?? QuickSearchResults.FirstOrDefault();
        if (item == null)
            return;

        CloseQuickSearch();
        await item.ExecuteAsync();
    }

    public void SelectQuickSearchIndex(int offset)
    {
        if (QuickSearchResults.Count == 0)
            return;

        var currentIndex = SelectedQuickSearchItem != null ? QuickSearchResults.IndexOf(SelectedQuickSearchItem) : -1;
        var nextIndex = currentIndex < 0 ? 0 : currentIndex + offset;
        if (nextIndex < 0)
            nextIndex = QuickSearchResults.Count - 1;
        if (nextIndex >= QuickSearchResults.Count)
            nextIndex = 0;

        SelectedQuickSearchItem = QuickSearchResults[nextIndex];
    }

    public void SelectFirstQuickSearchItem()
    {
        if (QuickSearchResults.Count > 0)
            SelectedQuickSearchItem = QuickSearchResults[0];
    }

    private CommandPaletteEngine PaletteEngine => commandPaletteEngine ??= new CommandPaletteEngine(
        session,
        navigationHost,
        priceBankStore,
        projectWorkspace,
        undo);

    public void RefreshQuickSearchResults()
    {
        var query = SearchText ?? string.Empty;
        var previousSelection = SelectedQuickSearchItem;

        quickSearchResults.Clear();
        var results = PaletteEngine.Search(query, SelectedPaletteCategory, msg => ShellNotificationService.Instance.ShowSuccess("Comando ejecutado", msg));
        foreach (var item in results)
        {
            quickSearchResults.Add(new ShellViewModel.QuickSearchItemVm(item));
        }

        Notify(nameof(HasQuickSearchResults));
        Notify(nameof(ResultCountLabel));

        if (quickSearchResults.Count == 0)
        {
            SelectedQuickSearchItem = null;
            return;
        }

        if (previousSelection != null)
        {
            var match = quickSearchResults.FirstOrDefault(item => item.Title == previousSelection.Title);
            if (match != null)
            {
                SelectedQuickSearchItem = match;
                return;
            }
        }

        SelectedQuickSearchItem = quickSearchResults[0];
    }

    private Task ExecuteQuickSearchItemAsync(ShellViewModel.QuickSearchItemVm? item)
    {
        if (item == null)
            return Task.CompletedTask;

        CloseQuickSearch();
        return item.ExecuteAsync();
    }

    private bool CanExecuteQuickSearchItem(ShellViewModel.QuickSearchItemVm? item) => item != null;

    private void Notify(string propertyName)
    {
        OnPropertyChanged(propertyName);
        onShellPropertyChanged?.Invoke(propertyName);
    }
}
