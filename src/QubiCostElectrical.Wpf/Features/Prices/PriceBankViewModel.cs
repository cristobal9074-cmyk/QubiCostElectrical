using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Prices;

public sealed partial class PriceBankViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly IPermissionService permissions;
    private readonly IMaterialSearchService materialSearch;
    private readonly IShellNavigationHost? shell;
    private readonly IPriceBankStore store;
    private readonly PriceBankApplicationService priceBank;
    private readonly PriceBankService priceBankDomain = new();

    private readonly ObservableCollection<PriceBankEntryVm> entries = new();
    private readonly ObservableCollection<PriceBankUpdateVm> previewUpdates = new();
    private readonly ObservableCollection<string> importLog = new();
    private readonly ObservableCollection<string> categoryOptions = new();
    private readonly ObservableCollection<string> providerOptions = new();
    private readonly ObservableCollection<string> currencyOptions = new();

    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand importFileCommand;
    private readonly RelayCommand downloadTemplateCommand;
    private readonly RelayCommand importProjectCommand;
    private readonly RelayCommand saveSelectedCommand;
    private readonly RelayCommand deleteSelectedCommand;
    private readonly RelayCommand deleteAllCommand;
    private readonly RelayCommand newEntryCommand;
    private readonly RelayCommand applyProjectPricesCommand;
    private readonly RelayCommand clearFiltersCommand;
    private readonly RelayCommand clearSearchCommand;

    private readonly ICollectionView entriesView;

    private PriceBankEntryVm? selectedEntry;
    private string searchText = "";
    private string cachedNormalizedSearch = "";
    private string selectedCategory = "Todos";
    private string selectedProvider = "Todos";
    private string selectedCurrency = "Todas";
    private string statusMessage = "";
    private string summaryText = "0 precios";
    private string importSummary = "Importación no ejecutada.";
    private string projectSummary = "Sin proyecto abierto.";
    private bool isBusy;
    private bool showEmptyState = true;
    private bool hasNoFilterResults;
    private System.Windows.Threading.DispatcherTimer? searchDebounceTimer;

    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone ninguna cifra en UF (columnas
    /// PRECIO UF / MO UF). El precio fuente es siempre CLP; la UF es solo una equivalencia
    /// derivada y se oculta cuando el proyecto está en pesos.
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Notifica el cambio de política de moneda para que la vista reaplique la grilla.</summary>
    private void NotifyMonedaChanged() => OnPropertyChanged(nameof(MuestraUf));

    public Func<string, bool> ConfirmDeleteAll { get; set; } = msg => WpfDialogService.Confirm("Eliminar todo el banco de precios", msg, isWarning: true);
}
