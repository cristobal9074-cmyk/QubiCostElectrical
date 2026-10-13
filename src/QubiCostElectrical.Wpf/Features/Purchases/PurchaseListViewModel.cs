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
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Purchases;

public sealed class PurchaseListViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IPermissionService permissions;
    private readonly IMaterialSearchService materialSearch;
    private readonly IPurchaseListService purchaseList;
    private readonly IPurchaseListExcelExporter purchaseListExcel;
    private readonly ObservableCollection<CompraMaterialViewModel> rows = new();
    private readonly ObservableCollection<string> categoryOptions = new();
    private readonly ObservableCollection<MaterialUsageBreakdown> materialUsages = new();
    private readonly ICollectionView rowsView;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand exportExcelCommand;
    private readonly RelayCommand exportRfqCommand;
    private readonly RelayCommand clearFiltersCommand;
    private readonly RelayCommand goToOriginCommand;
    private readonly RelayCommand goToUsageCommand;
    private readonly RelayCommand groupByProviderCommand;
    private readonly System.Windows.Threading.DispatcherTimer searchDebounceTimer;

    private string searchText = "";
    private string selectedCategory = "Todas";
    private decimal toleranciaPct = 5m;
    private string statusMessage = "Lista de compra no calculada.";
    private string summaryText = "0 materiales";
    private string projectSummary = "Sin proyecto abierto.";
    private bool showEmptyState = true;
    private bool isGroupedByProvider;
    private CompraMaterialViewModel? selectedRow;
    private MaterialUsageBreakdown? selectedUsage;

    public PurchaseListViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Permissions, host.Services.MaterialSearch, host.Services.PurchaseList, host.Services.PurchaseListExcel) { }

    public PurchaseListViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, IPermissionService permissions, IMaterialSearchService materialSearch, IPurchaseListService purchaseList, IPurchaseListExcelExporter purchaseListExcel) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.permissions = permissions;
        this.materialSearch = materialSearch;
        this.purchaseList = purchaseList;
        this.purchaseListExcel = purchaseListExcel;

        refreshCommand = new RelayCommand(Refresh);
        exportExcelCommand = new RelayCommand(ExportExcel, CanExportExcel);
        exportRfqCommand = new RelayCommand(ExportRfq, CanExportExcel);
        clearFiltersCommand = new RelayCommand(ClearFilters);
        goToOriginCommand = new RelayCommand(GoToOrigin, () => selectedRow != null);
        goToUsageCommand = new RelayCommand(GoToSelectedUsage, () => SelectedUsage != null || selectedRow != null);
        groupByProviderCommand = new RelayCommand(ToggleGroupByProvider);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());

        rowsView = CollectionViewSource.GetDefaultView(rows);
        rowsView.Filter = FilterRow;
        rowsView.SortDescriptions.Add(new SortDescription(nameof(CompraMaterialViewModel.TotalCompraUf), ListSortDirection.Descending));
        rowsView.SortDescriptions.Add(new SortDescription(nameof(CompraMaterialViewModel.Codigo), ListSortDirection.Ascending));

        searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        searchDebounceTimer.Tick += (_, _) =>
        {
            searchDebounceTimer.Stop();
            rowsView.Refresh();
            UpdateSummary();
        };

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            Refresh();
            NotifyMonedaChanged();
        });

    public ObservableCollection<CompraMaterialViewModel> Rows => rows;
    public ICollectionView FilteredRows => rowsView;
    public ObservableCollection<string> CategoryOptions => categoryOptions;
    public bool HasProject => workspace.HasOpenProject;

    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone ninguna cifra en UF (ni columnas
    /// PRECIO UF / NETO UF / COMPRA UF ni la equivalencia UF del resumen). La vista reaplica las
    /// columnas y esta VM recalcula el mensaje de estado.
    /// </summary>
    public bool MuestraUf =>
        !string.Equals(session.ProjectInfo.MonedaPresupuesto, CurrencyConversionService.CurrencyClp, StringComparison.OrdinalIgnoreCase);

    /// <summary>Notifica el cambio de política de moneda para que la vista reaplique la grilla.</summary>
    private void NotifyMonedaChanged()
    {
        OnPropertyChanged(nameof(MuestraUf));
        UpdateSummary();
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            if (System.Windows.Application.Current == null)
            {
                rowsView.Refresh();
                UpdateSummary();
            }
            else
            {
                searchDebounceTimer.Stop();
                searchDebounceTimer.Start();
            }
        }
    }

    public string SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (!SetProperty(ref selectedCategory, value))
                return;
            rowsView.Refresh();
            UpdateSummary();
        }
    }

    public decimal ToleranciaPct
    {
        get => toleranciaPct;
        set
        {
            if (!SetProperty(ref toleranciaPct, Math.Clamp(value, 0m, 50m)))
                return;
            UpdateRows();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    public string SummaryText
    {
        get => summaryText;
        private set => SetProperty(ref summaryText, value);
    }

    public string ProjectSummary
    {
        get => projectSummary;
        private set => SetProperty(ref projectSummary, value);
    }

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }

    public string EmptyStateTitle => HasProject ? "Lista vacía" : "Sin proyecto abierto";

    public string EmptyStateDescription => HasProject
        ? "Aún no hay recursos comprables en la cubicación activa."
        : "Abra un proyecto para consolidar la lista de compra desde la cubicación.";

    public bool IsGroupedByProvider
    {
        get => isGroupedByProvider;
        private set => SetProperty(ref isGroupedByProvider, value);
    }

    public ObservableCollection<MaterialUsageBreakdown> MaterialUsages => materialUsages;

    public MaterialUsageBreakdown? SelectedUsage
    {
        get => selectedUsage;
        set
        {
            if (!SetProperty(ref selectedUsage, value))
                return;
            goToUsageCommand.RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedUsage => materialUsages.Count > 0;
    public string SelectedMaterialHeader => selectedRow == null ? "Seleccione un material" : $"{selectedRow.Codigo} · {selectedRow.Descripcion}";
    public string SelectedMaterialTotalText => selectedRow == null ? "" : $"{selectedRow.CantidadCompra:N2} {selectedRow.Unidad} (en {materialUsages.Count:N0} partida/área)";

    public CompraMaterialViewModel? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (!SetProperty(ref selectedRow, value))
                return;
            goToOriginCommand.RaiseCanExecuteChanged();
            goToUsageCommand.RaiseCanExecuteChanged();
            UpdateMaterialUsages();
        }
    }

    public ICommand RefreshCommand => refreshCommand;
    public ICommand ExportExcelCommand => exportExcelCommand;
    public ICommand ExportRfqCommand => exportRfqCommand;
    public ICommand ClearFiltersCommand => clearFiltersCommand;
    public ICommand GoToOriginCommand => goToOriginCommand;
    public ICommand GoToUsageCommand => goToUsageCommand;
    public ICommand GroupByProviderCommand => groupByProviderCommand;
    public ICommand OpenProjectCommand { get; }

    public void Refresh()
    {
        UpdateProjectSummary();
        UpdateRows();
    }

    private void UpdateRows()
    {
        rows.Clear();
        if (HasProject)
        {
            foreach (var row in purchaseList.Build(session.Cubicacion, session.Materiales, ToleranciaPct))
                rows.Add(row);
        }

        BuildCategoryOptions();
        rowsView.Refresh();
        UpdateSummary();
        UpdateProjectSummary();
        ShowEmptyState = !HasProject || rows.Count == 0;
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
        StatusMessage = !HasProject
            ? "Abre o crea un proyecto para consolidar la lista de compra."
            : rows.Count == 0
                ? "No hay recursos comprables en la cubicación actual."
                : $"Lista consolidada con {rows.Count:N0} materiales.";
        exportExcelCommand.RaiseCanExecuteChanged();
        exportRfqCommand.RaiseCanExecuteChanged();
    }

    private void BuildCategoryOptions()
    {
        var values = rows
            .Select(x => string.IsNullOrWhiteSpace(x.Categoria) ? "Sin categoría" : x.Categoria.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        categoryOptions.Clear();
        categoryOptions.Add("Todas");
        foreach (var value in values)
            categoryOptions.Add(value);

        if (!categoryOptions.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
            SelectedCategory = "Todas";
    }

    private bool FilterRow(object obj)
    {
        if (obj is not CompraMaterialViewModel row)
            return false;

        string query = materialSearch.Normalize(SearchText);
        if (!string.IsNullOrWhiteSpace(query))
        {
            string searchable = materialSearch.Normalize(string.Join(' ', new[]
            {
                row.Codigo,
                row.Descripcion,
                row.Categoria,
                row.Unidad
            }));

            if (!searchable.Contains(query, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.Equals(SelectedCategory, "Todas", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(row.Categoria, SelectedCategory, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void UpdateSummary()
    {
        if (!HasProject)
        {
            SummaryText = "Sin proyecto abierto";
            StatusMessage = "Abre o crea un proyecto para consolidar la lista de compra.";
            exportExcelCommand.RaiseCanExecuteChanged();
            exportRfqCommand.RaiseCanExecuteChanged();
            return;
        }

        int visible = rowsView.Cast<object>().Count();
        decimal totalNeto = rowsView.Cast<CompraMaterialViewModel>().Sum(x => x.TotalNetoUf);
        decimal totalCompra = rowsView.Cast<CompraMaterialViewModel>().Sum(x => x.TotalCompraUf);
        decimal valorUf = session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;
        decimal totalNetoClp = rowsView.Cast<CompraMaterialViewModel>().Sum(x => x.TotalNetoClp > 0m ? x.TotalNetoClp : x.TotalNetoUf * valorUf);
        decimal totalCompraClp = rowsView.Cast<CompraMaterialViewModel>().Sum(x => x.TotalCompraClp > 0m ? x.TotalCompraClp : x.TotalCompraUf * valorUf);
        int missingPrices = rowsView.Cast<CompraMaterialViewModel>().Count(x => x.MatUfUnitario <= 0m && x.MatClpUnitario <= 0m);
        SummaryText = $"{visible:N0} visibles · {rows.Count:N0} total · {missingPrices:N0} sin precio";
        StatusMessage = rows.Count == 0
            ? "No hay recursos comprables en la cubicación actual."
            : MuestraUf
                ? $"Lista consolidada: {totalNeto:N3} UF ($ {totalNetoClp:N0}) neto · {totalCompra:N3} UF ($ {totalCompraClp:N0}) compra."
                : $"Lista consolidada: $ {totalNetoClp:N0} neto · $ {totalCompraClp:N0} compra.";
        exportExcelCommand.RaiseCanExecuteChanged();
        exportRfqCommand.RaiseCanExecuteChanged();
    }

    private void UpdateProjectSummary()
    {
        ProjectSummary = !HasProject
            ? "Sin proyecto abierto."
            : $"{session.ProjectInfo.Nombre} · {session.Cubicacion.Count:N0} recursos";
    }

    private bool CanExportExcel() => HasProject && rows.Count > 0 && permissions.CanExportDocuments;

    private void ExportExcel()
    {
        if (!CanExportExcel())
            return;

        try
        {
            string? file = PromptSaveFile();
            if (file == null)
                return;

            var snapshot = rows.ToList();
            purchaseListExcel.Export(file, new PurchaseListExcelRequest(
                session.ProjectInfo.Nombre,
                ToleranciaPct,
                snapshot,
                MuestraUf,
                session.ValorUfClp));

            StatusMessage = $"Lista de compra exportada: {Path.GetFileName(file)}";
            WpfDialogService.Info("Lista de compra", "La exportación se completó correctamente.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Purchases", "Falló exportar lista de compra", ex);
            var userMessage = OperationFailureMessages.ToUserMessage(ex);
            StatusMessage = userMessage;
            WpfDialogService.Error("Exportar lista de compra", userMessage);
        }
    }

    private void ExportRfq()
    {
        if (!CanExportExcel())
            return;

        try
        {
            string defaultName = string.IsNullOrWhiteSpace(session.ProjectInfo.Nombre)
                ? "RFQ_Cotizacion_Materiales.xlsx"
                : $"RFQ_{SanitizeFilename(session.ProjectInfo.Nombre)}.xlsx";

            var dialog = new SaveFileDialog
            {
                Title = "Exportar Solicitud de Cotización para Distribuidores (RFQ)",
                Filter = "Excel (*.xlsx)|*.xlsx",
                FileName = defaultName,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true)
                return;

            var snapshot = rows.ToList();
            string company = !string.IsNullOrWhiteSpace(session.ProjectInfo.OfertaPdf?.CompanyName)
                ? session.ProjectInfo.OfertaPdf.CompanyName
                : (!string.IsNullOrWhiteSpace(session.ProjectInfo.Cliente)
                    ? session.ProjectInfo.Cliente
                    : session.ProjectInfo.Nombre);

            purchaseListExcel.ExportRfq(dialog.FileName, new PurchaseListRfqRequest(
                session.ProjectInfo.Nombre,
                company,
                ToleranciaPct,
                snapshot));

            StatusMessage = $"Solicitud RFQ exportada: {Path.GetFileName(dialog.FileName)}";
            WpfDialogService.Info("Solicitud de Cotización (RFQ)", "La plantilla para distribuidores se exportó correctamente lista para enviar por email.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Purchases", "Falló exportar solicitud RFQ", ex);
            var userMessage = OperationFailureMessages.ToUserMessage(ex);
            StatusMessage = userMessage;
            WpfDialogService.Error("Exportar Solicitud RFQ", userMessage);
        }
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalid.Contains(c))).Replace(' ', '_');
    }

    private void ClearFilters()
    {
        searchDebounceTimer.Stop();
        searchText = "";
        OnPropertyChanged(nameof(SearchText));
        selectedCategory = "Todas";
        OnPropertyChanged(nameof(SelectedCategory));
        rowsView.Refresh();
        UpdateSummary();
    }

    private void ToggleGroupByProvider()
    {
        IsGroupedByProvider = !IsGroupedByProvider;
        rowsView.GroupDescriptions.Clear();
        if (IsGroupedByProvider)
        {
            rowsView.GroupDescriptions.Add(
                new PropertyGroupDescription(nameof(CompraMaterialViewModel.GrupoProveedor)));
        }

        rowsView.Refresh();
    }

    private void UpdateMaterialUsages()
    {
        materialUsages.Clear();
        if (selectedRow != null && HasProject)
        {
            foreach (var usage in purchaseList.GetMaterialUsage(selectedRow.Codigo, session.Cubicacion, session.Itemizado))
                materialUsages.Add(usage);
        }

        SelectedUsage = materialUsages.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelectedUsage));
        OnPropertyChanged(nameof(SelectedMaterialHeader));
        OnPropertyChanged(nameof(SelectedMaterialTotalText));
    }

    private void GoToOrigin()
    {
        if (selectedRow == null)
            return;

        var itemId = session.Cubicacion
            .Where(c => string.Equals(c.Codigo, selectedRow.Codigo, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.ItemId)
            .FirstOrDefault();

        if (itemId == null)
        {
            shell.RequestNavigate(ShellRoute.Takeoff);
            return;
        }

        shell.RequestNavigate(
            ShellRoute.Takeoff,
            new TakeoffNavigationContext(itemId, selectedRow.Codigo, selectedRow.Descripcion));
    }

    private void GoToSelectedUsage()
    {
        var target = SelectedUsage;
        if (target != null && !string.IsNullOrWhiteSpace(target.ItemId))
        {
            shell.RequestNavigate(
                ShellRoute.Takeoff,
                new TakeoffNavigationContext(target.ItemId, target.Codigo, target.Descripcion));
            return;
        }

        GoToOrigin();
    }

    private static string? PromptSaveFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar lista de compra",
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = "ListaCompra.xlsx",
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
