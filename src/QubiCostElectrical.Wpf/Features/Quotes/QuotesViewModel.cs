using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Quotes;

public sealed class QuoteProviderSummaryVm
{
    public string Proveedor { get; init; } = "";
    public int Materiales { get; init; }
    public int Categorias { get; init; }
    public decimal PrecioPromedioUf { get; init; }
    public decimal PrecioMinUf { get; init; }
    public decimal PrecioMaxUf { get; init; }
    public DateTime UltimaFechaPrecio { get; init; }
}

public sealed class QuoteEntryVm
{
    public QuoteEntryVm(PriceBankEntry source, decimal valorUf = 0m)
    {
        Source = source;
        ValorUf = valorUf > 0m ? valorUf : PriceConversion.DefaultValorUfClp;
    }

    public PriceBankEntry Source { get; }
    public decimal ValorUf { get; }
    public string Codigo => Source.Codigo;
    public string Descripcion => Source.Descripcion;
    public string Unidad => Source.Unidad;
    public string Categoria => Source.Categoria;
    public decimal MatClp => Source.MatClpConFallback > 0m ? Source.MatClpConFallback : PriceConversion.UfToClp(Source.MatUf, ValorUf);
    public decimal MoClp => Source.MoClpConFallback > 0m ? Source.MoClpConFallback : PriceConversion.UfToClp(Source.MoUf, ValorUf);
    public decimal PrecioClp => MatClp + MoClp;
    public decimal MatUf => Source.MatUf > 0m ? Source.MatUf : PriceConversion.ClpToUf(MatClp, ValorUf);
    public decimal MoUf => Source.MoUf > 0m ? Source.MoUf : PriceConversion.ClpToUf(MoClp, ValorUf);
    public decimal PrecioUf => MatUf + MoUf;
    public string Proveedor => Source.Proveedor;
    public string Marca => Source.Marca;
    public string Moneda => Source.MonedaPrecio;
    public DateTime FechaPrecio => Source.FechaPrecio;
    public string Origen => Source.OrigenPrecio;
}

public sealed class QuoteComparisonRowVm
{
    public QuoteComparisonRowVm(
        string codigo,
        string descripcion,
        string proveedorSeleccionado,
        decimal precioSeleccionado,
        string proveedorMejor,
        decimal precioMejor,
        string categoria,
        decimal precioSeleccionadoClp = 0m,
        decimal precioMejorClp = 0m)
    {
        Codigo = codigo;
        Descripcion = descripcion;
        ProveedorSeleccionado = proveedorSeleccionado;
        PrecioSeleccionado = precioSeleccionado;
        ProveedorMejor = proveedorMejor;
        PrecioMejor = precioMejor;
        Categoria = categoria;
        PrecioSeleccionadoClp = precioSeleccionadoClp;
        PrecioMejorClp = precioMejorClp;
    }

    public string Codigo { get; }
    public string Descripcion { get; }
    public string ProveedorSeleccionado { get; }
    public decimal PrecioSeleccionado { get; }
    public string ProveedorMejor { get; }
    public decimal PrecioMejor { get; }
    public decimal DeltaUf => PrecioSeleccionado - PrecioMejor;
    public decimal DeltaPct => PrecioMejor <= 0m ? 0m : (PrecioSeleccionado - PrecioMejor) / PrecioMejor * 100m;
    public string Categoria { get; }
    public decimal PrecioSeleccionadoClp { get; }
    public decimal PrecioMejorClp { get; }
}

public sealed partial class QuotesViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IMaterialSearchService materialSearch;
    private readonly IPriceBankStore store;
    private readonly ObservableCollection<QuoteProviderSummaryVm> providerSummaries = new();
    private readonly ObservableCollection<QuoteEntryVm> providerEntries = new();
    private readonly ObservableCollection<QuoteComparisonRowVm> comparisonRows = new();
    private readonly ObservableCollection<string> categoryOptions = new();
    private readonly ICollectionView providerSummaryView;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand clearFiltersCommand;
    private readonly RelayCommand importQuoteCommand;
    private readonly RelayCommand<QuoteComparisonRowVm> applyPriceCommand;
    private readonly RelayCommand applyAllBestPricesCommand;
    private readonly RelayCommand applySelectionToBankCommand;
    private readonly System.Windows.Threading.DispatcherTimer searchDebounceTimer;

    private string searchText = "";
    private string cachedNormalizedSearch = "";
    private string selectedCategory = "Todas";
    private QuoteProviderSummaryVm? selectedProvider;
    private QuoteComparisonRowVm? selectedComparisonRow;
    private string statusMessage = "Cotizaciones no cargadas.";
    private string summaryText = "0 proveedores";
    private string projectSummary = "Sin proyecto abierto.";
    private bool showEmptyState = true;
    private IReadOnlyList<PriceBankEntry> allEntries = Array.Empty<PriceBankEntry>();
    // Índice de proveedores (normalizados) que tienen al menos una entrada en la categoría
    // seleccionada — evita que FilterProvider escanee TODO el banco de precios por cada
    // proveedor visible en cada refiltrado (O(proveedores×entradas) por tecla cuando hay
    // categoría activa). Se recalcula solo cuando cambia SelectedCategory o allEntries.
    private readonly HashSet<string> providersInSelectedCategory = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone ninguna cifra en UF (columnas
    /// PRECIO UF / MEJOR UF / DELTA UF). El precio fuente de la cotización siempre es CLP.
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, Moneda.CLP, StringComparison.OrdinalIgnoreCase);

    /// <summary>Notifica el cambio de política de moneda para que la vista reaplique la grilla.</summary>
    private void NotifyMonedaChanged() => OnPropertyChanged(nameof(MuestraUf));
}
