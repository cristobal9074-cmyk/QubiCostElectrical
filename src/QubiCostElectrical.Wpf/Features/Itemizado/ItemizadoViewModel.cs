using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly ProjectAuditRecorder audit;
    private readonly WpfUndoService undo;
    private readonly ItemizadoPresenter presenter;
    private readonly IItemizadoWorkspaceService workspace;
    private readonly ItemizadoImportCoordinator importCoordinator;
    private readonly List<ItemizadoRowVm> allRows = new();
    private readonly Dictionary<string, ItemizadoRowVm> rowsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ItemizadoBreadcrumbCoordinator breadcrumbCoordinator;
    private ICollectionView? rowsView;
    private ItemizadoRowVm? selectedRow;
    private ItemizadoFilterMode filterMode = ItemizadoFilterMode.Todos;
    private string searchText = "";
    private string debouncedSearchText = "";
    private DispatcherTimer? searchDebounceTimer;
    private bool showEmptyState = true;
    private bool showEmptyItemizado;
    private bool isLoading;
    private string headerSubtitle = "", saveStateLabel = "", statusMessage = "";
    private string summaryPartidas = "0", summaryCompletas = "0", summaryIncompletas = "0", summaryCapitulos = "0", summarySinPrecio = "0", summaryStripText = "", filteredCountLabel = "";
    private List<ItemizadoRowPresentation>? filterPresentationCache;
    private string? pendingSelectionId;
    private readonly Dictionary<string, decimal> costTotalsByItem = new(StringComparer.OrdinalIgnoreCase);
    private bool hasPendingUndoForEdit;

    public Func<string, string, bool> ConfirmReplace { get; set; } = (title, msg) => WpfDialogService.Confirm(title, msg, isWarning: true);
    private readonly HashSet<ItemizadoRowVm> wiredRows = new();
    private EventHandler<ProjectSessionChangedEventArgs>? sessionSavedHandler;
    private ItemizadoTotalsSnapshot totalsSnapshot = ItemizadoTotalsCoordinator.Calculate(Array.Empty<ItemizadoRowVm>(), Array.Empty<ItemizadoRowVm>(), 38_500m, false, "");

    // La vista se suscribe para entrar en modo edición automáticamente sobre la fila
    // recién agregada (Descripción), sin requerir F2 ni clic manual. Solo se dispara
    // tras Agregar partida / Agregar título; el resto de los flujos de edición no
    // se modifica.
    public event Action<string>? RowAdded;

    public ItemizadoViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Audit, host.UndoService, host.Itemizado, host.ItemizadoWorkspace) { }

    public ItemizadoViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService projectWorkspace, ProjectAuditRecorder audit, WpfUndoService undo, ItemizadoPresenter presenter, IItemizadoWorkspaceService workspace) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.audit = audit;
        this.undo = undo;
        this.presenter = presenter;
        this.workspace = workspace;
        importCoordinator = new ItemizadoImportCoordinator(presenter);
        breadcrumbCoordinator = new ItemizadoBreadcrumbCoordinator(row =>
        {
            SelectedRow = row;
            EnsureSelectionVisible();
        });
        rowsView = CollectionViewSource.GetDefaultView(Rows);
        rowsView.Filter = FilterRow;

        searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        searchDebounceTimer.Tick += OnSearchDebounceTick;

        ImportExcelCommand = new RelayCommand(() => _ = ImportExcelAsync(), () => !ShowEmptyState && !session.IsReadOnly);
        PasteExcelCommand = new RelayCommand(() => _ = PasteFromClipboardAsync(), () => !ShowEmptyState && !session.IsReadOnly);
        AddPartidaCommand = new RelayCommand(AddPartida, () => !ShowEmptyState && !session.IsReadOnly);
        AddTitleCommand = new RelayCommand(AddTitle, () => !ShowEmptyState && !session.IsReadOnly);
        CubicarCommand = new RelayCommand(() => _ = CubicarSelectedAsync(), CanCubicar);
        MoveUpCommand = new RelayCommand(MoveUp, () => HasSelection && !session.IsReadOnly);
        MoveDownCommand = new RelayCommand(MoveDown, () => HasSelection && !session.IsReadOnly);
        DuplicateCommand = new RelayCommand(DuplicateSelected, CanDuplicate);
        DeleteCommand = new RelayCommand(DeleteSelected, () => HasSelection && !session.IsReadOnly);
        ClearAllCommand = new RelayCommand(ClearAll, () => !ShowEmptyState && !ShowEmptyItemizado && !session.IsReadOnly);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        SetFilterCommand = new RelayCommand<ItemizadoFilterMode>(mode => FilterMode = mode);
        ClearSearchCommand = new RelayCommand(ClearSearch, () => IsSearchActive);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => !ShowEmptyState);
        ConfigureCommercialModelCommand = new RelayCommand(ConfigureCommercialModel, () => HasSelection && !session.IsReadOnly && SelectedRow?.EsTitulo != true);
        LoadStandardTemplateCommand = new RelayCommand<string>(LoadStandardTemplate, _ => !ShowEmptyState && !session.IsReadOnly);
        ToggleChapterCollapseCommand = new RelayCommand<ItemizadoRowVm>(ToggleChapterCollapse);
        OpenApuCommand = new RelayCommand<ItemizadoRowVm>(row =>
        {
            var target = row ?? SelectedRow;
            if (target == null || (target.EsTitulo && !presenter.TryPromoteLeafTitleToPartida(target.Model))) return;
            target.IsTitleRow = target.Model.EsTitulo;
            shell.RequestNavigate(ShellRoute.Apu, new QubiCostElectrical.Wpf.Features.Apu.ApuNavContext(target.Id, target.Item, target.Descripcion));
        });
        CubicarRowCommand = new RelayCommand<ItemizadoRowVm>(row =>
        {
            var target = row ?? SelectedRow;
            if (target == null || (target.EsTitulo && presenter.HasChildPartidas(target.Id))) return;
            SelectedRow = target;
            _ = CubicarSelectedAsync();
        });

        // Marshalling al Dispatcher para evitar NotSupportedException al refrescar colecciones UI
        sessionSavedHandler = (_, _) => RunOnUiThread(() =>
        {
            hasPendingUndoForEdit = false;
            UpdateSaveState();
        });
        session.Saved += sessionSavedHandler;

        SyncCurrencyFromProject();
        Reload(syncOnly: false);
        ProcessPendingShellAction();
    }

    protected override void DisposeCore()
    {
        if (sessionSavedHandler != null)
            session.Saved -= sessionSavedHandler;
        searchDebounceTimer?.Stop();
        foreach (var vm in wiredRows.ToList())
            UnwireRowVm(vm);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.InvokeAsync(action);
    }

    public string FilteredCountLabel { get => filteredCountLabel; private set => SetProperty(ref filteredCountLabel, value); }
    private int totalPartidasCount, listasPartidasCount, pendientesPartidasCount;
    public int TotalPartidasCount { get => totalPartidasCount; set => SetProperty(ref totalPartidasCount, value); }
    public int ListasPartidasCount { get => listasPartidasCount; set => SetProperty(ref listasPartidasCount, value); }
    public int PendientesPartidasCount { get => pendientesPartidasCount; set => SetProperty(ref pendientesPartidasCount, value); }
    public string TotalFilterLabel => $"Todas ({totalPartidasCount})";
    public string ListasFilterLabel => $"✓ Listas ({listasPartidasCount})";
    public string PendientesFilterLabel => $"⚠️ Pendientes ({pendientesPartidasCount})";
    public string CubicarButtonLabel => "Cubicar partida";
    public ObservableCollection<ItemizadoRowVm> Rows { get; } = new();
    public ICollectionView FilteredRows => rowsView ?? CollectionViewSource.GetDefaultView(Rows);
    public ItemizadoBreadcrumbCoordinator Breadcrumb => breadcrumbCoordinator;
    public bool HasBreadcrumb => breadcrumbCoordinator.HasNodes;
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(searchText);

    public ItemizadoRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (!SetProperty(ref selectedRow, value))
                return;
            breadcrumbCoordinator.UpdateBreadcrumb(selectedRow, rowsById, allRows);
            OnPropertyChanged(nameof(HasBreadcrumb));
            RaiseCommandStates();
        }
    }

    public ItemizadoFilterMode FilterMode
    {
        get => filterMode;
        set
        {
            if (!SetProperty(ref filterMode, value))
                return;
            OnPropertyChanged(nameof(IsFilterTodos));
            OnPropertyChanged(nameof(IsFilterIncompletos));
            OnPropertyChanged(nameof(IsFilterCompletos));
            OnPropertyChanged(nameof(IsFilterSinCubicar));
            OnPropertyChanged(nameof(IsFilterSinPrecio));
            OnPropertyChanged(nameof(IsFilterListas));
            OnPropertyChanged(nameof(IsFilterPendientes));
            SafeRefreshView();
            EnsureSelectionVisible();
            UpdateFilterCounts();
            OnPropertyChanged(nameof(CubicarButtonLabel));
        }
    }

    public bool IsFilterTodos { get => FilterMode == ItemizadoFilterMode.Todos; set { if (value) FilterMode = ItemizadoFilterMode.Todos; } }
    public bool IsFilterIncompletos { get => FilterMode == ItemizadoFilterMode.Incompletos; set { if (value) FilterMode = ItemizadoFilterMode.Incompletos; } }
    public bool IsFilterCompletos { get => FilterMode == ItemizadoFilterMode.Completos; set { if (value) FilterMode = ItemizadoFilterMode.Completos; } }
    public bool IsFilterSinCubicar => FilterMode == ItemizadoFilterMode.SinCubicar;
    public bool IsFilterSinPrecio => FilterMode == ItemizadoFilterMode.SinPrecio;
    public bool IsFilterListas { get => IsFilterCompletos; set => IsFilterCompletos = value; }
    public bool IsFilterPendientes { get => IsFilterIncompletos; set => IsFilterIncompletos = value; }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value))
                return;
            OnPropertyChanged(nameof(IsSearchActive));
            searchDebounceTimer?.Stop();
            searchDebounceTimer?.Start();
        }
    }

    public bool IsReadOnly => session.IsReadOnly;

    public bool ShowEmptyState { get => showEmptyState; private set { if (SetProperty(ref showEmptyState, value)) RaiseCommandStates(); } }
    public bool ShowEmptyItemizado { get => showEmptyItemizado; private set { if (SetProperty(ref showEmptyItemizado, value)) RaiseCommandStates(); } }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public string HeaderSubtitle { get => headerSubtitle; private set => SetProperty(ref headerSubtitle, value); }
    public string SaveStateLabel { get => saveStateLabel; private set => SetProperty(ref saveStateLabel, value); }
    public string StatusMessage { get => statusMessage; private set { if (SetProperty(ref statusMessage, value)) OnPropertyChanged(nameof(HasStatusMessage)); } }
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public string SummaryPartidas { get => summaryPartidas; private set => SetProperty(ref summaryPartidas, value); }
    public string SummaryCompletas { get => summaryCompletas; private set => SetProperty(ref summaryCompletas, value); }
    public string SummaryIncompletas { get => summaryIncompletas; private set => SetProperty(ref summaryIncompletas, value); }
    public string SummaryCapitulos { get => summaryCapitulos; private set => SetProperty(ref summaryCapitulos, value); }
    public string SummarySinPrecio { get => summarySinPrecio; private set => SetProperty(ref summarySinPrecio, value); }
    public string SummaryStripText { get => summaryStripText; private set => SetProperty(ref summaryStripText, value); }
    public string TotalPresupuestoUfText => totalsSnapshot.TotalBudgetUfText;
    public string TotalPresupuestoClpText => totalsSnapshot.TotalBudgetClpText;
    public string FooterPartidasText => totalsSnapshot.FooterPartidasText;
    public string FooterTotalUfText => totalsSnapshot.FooterTotalUfText;
    public string FooterTotalClpText => string.IsNullOrWhiteSpace(totalsSnapshot.FooterTotalClpText)
        ? ""
        : $"({totalsSnapshot.FooterTotalClpText})";
    public string UfRateCaption => totalsSnapshot.UfRateCaption;
    public bool HasActiveFilter => totalsSnapshot.HasActiveFilter;

    public ICommand ImportExcelCommand { get; }
    public ICommand PasteExcelCommand { get; }
    public ICommand AddPartidaCommand { get; }
    public ICommand AddTitleCommand { get; }
    public ICommand CubicarCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand DuplicateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ConfigureCommercialModelCommand { get; }
    public ICommand LoadStandardTemplateCommand { get; }
    public ICommand ToggleChapterCollapseCommand { get; }
    public ICommand OpenApuCommand { get; }
    public ICommand CubicarRowCommand { get; }

    private bool isViewVenta = true;
    public bool IsViewVenta
    {
        get => isViewVenta;
        set
        {
            if (SetProperty(ref isViewVenta, value))
            {
                OnPropertyChanged(nameof(IsViewCosto));
                OnPropertyChanged(nameof(ViewPerspectiveLabel));
            }
        }
    }

    public bool IsViewCosto
    {
        get => !isViewVenta;
        set => IsViewVenta = !value;
    }

    public string ViewPerspectiveLabel => isViewVenta ? "Venta (P.V.)" : "Costo Directo";

    private string selectedDeliveryCurrency = Moneda.UF;

    /// <summary>
    /// Moneda de presentación del Itemizado. Es un espejo de la moneda del proyecto
    /// (<c>ProjectInfo.MonedaPresupuesto</c>), que es la única fuente de verdad: no se edita
    /// desde esta pantalla.
    /// </summary>
    public string SelectedDeliveryCurrency
    {
        get => selectedDeliveryCurrency;
        private set
        {
            string normalized = (value ?? Moneda.UF).Trim().ToUpperInvariant();
            if (SetProperty(ref selectedDeliveryCurrency, normalized))
            {
                OnPropertyChanged(nameof(IsCurrencyUf));
                OnPropertyChanged(nameof(IsCurrencyClp));
                OnPropertyChanged(nameof(IsCurrencyUsd));
                OnPropertyChanged(nameof(IsCurrencyEur));
                OnPropertyChanged(nameof(HeaderUnitPriceCaption));
                OnPropertyChanged(nameof(HeaderTotalCaption));
                ApplyCurrencyChange();
            }
        }
    }

    public bool IsCurrencyUf => selectedDeliveryCurrency == Moneda.UF;
    public bool IsCurrencyClp => selectedDeliveryCurrency == Moneda.CLP;
    public bool IsCurrencyUsd => selectedDeliveryCurrency == Moneda.USD;
    public bool IsCurrencyEur => selectedDeliveryCurrency == Moneda.EUR;

    /// <summary>
    /// Sincroniza la moneda de presentación del Itemizado con la moneda del proyecto
    /// (única fuente de verdad). Se invoca al abrir la pantalla y cuando el proyecto cambia.
    /// </summary>
    public void SyncCurrencyFromProject()
    {
        if (projectWorkspace.HasOpenProject)
            SelectedDeliveryCurrency = session.ProjectInfo.MonedaPresupuesto;
    }

    public string HeaderUnitPriceCaption => PriceConversion.GetCurrencyHeaderLabel("P.U.", selectedDeliveryCurrency);
    public string HeaderTotalCaption => PriceConversion.GetCurrencyHeaderLabel("TOTAL", selectedDeliveryCurrency);

    public void ApplyCurrencyChange()
    {
        var ctx = ItemizadoCurrencyCoordinator.CreateContext(
            selectedDeliveryCurrency,
            session.ValorUfClp,
            session.ProjectInfo.TipoCambioUsdClp,
            session.ProjectInfo.TipoCambioEurClp);

        foreach (var r in allRows)
            r.ApplyCurrencyContext(ctx);

        UpdateTotals();
    }

    private bool HasSelection => IsSelectionVisible && !ShowEmptyState;
    private bool IsSelectionVisible => SelectedRow != null && PassesCurrentFilter(SelectedRow);
    private bool CanEdit => !session.IsReadOnly;

    public void Refresh()
    {
        SyncCurrencyFromProject();
        Reload(syncOnly: false);
        ProcessPendingShellAction();
    }

    public void ProcessPendingShellAction()
    {
        var action = session.PendingItemizadoAction;
        if (action == ItemizadoPendingAction.None)
            return;

        session.PendingItemizadoAction = ItemizadoPendingAction.None;
        if (action == ItemizadoPendingAction.AddPartida)
            AddPartida();
    }

    public ProjectFileData? CaptureSnapshot() => session.IsReadOnly ? null : session.CreateSnapshot();

    public void ToggleSelectedRowEsTitulo()
    {
        if (session.IsReadOnly || SelectedRow is not { } row)
            return;

        pendingSelectionId = row.Id;
        undo.PushUndoPoint(session, row.EsTitulo ? "Convertir a partida" : "Convertir a título");
        bool newEsTitulo = !row.EsTitulo;

        if (!newEsTitulo)
        {
            if (row.Cantidad <= 0m)
            {
                row.Cantidad = row.Model.CantidadOriginal > 0m
                    ? row.Model.CantidadOriginal
                    : 1m;
            }
            if (string.IsNullOrWhiteSpace(row.Unidad))
            {
                row.Unidad = Unidades.GL;
            }
        }
        else
        {
            row.Cantidad = 0m;
        }

        row.EsTitulo = newEsTitulo;
        session.MarkDirty(ProjectDirtyScope.Itemizado);
        RefreshAfterStructureChange();
    }

    public void OnCellEdited(string? columnHeader = null, ItemizadoRowVm? editedRow = null, ProjectFileData? preEditSnapshot = null)
    {
        if (session.IsReadOnly)
            return;

        if (string.Equals(columnHeader, "Excl. cubic.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(columnHeader, "NO CUBICAR", StringComparison.OrdinalIgnoreCase))
            return;

        var row = editedRow ?? SelectedRow;
        if (row == null)
            return;

        var peers = session.Itemizado;
        if (string.Equals(columnHeader, "Ítem", StringComparison.OrdinalIgnoreCase))
            row.CommitItemEdit(peers);

        row.ValidateEditableCells(peers);

        string fieldLabel = string.Equals(columnHeader, "OBSERVACIONES", StringComparison.OrdinalIgnoreCase) ? "Observaciones" : (columnHeader ?? "campo");
        string detail = !string.IsNullOrWhiteSpace(row.Item) ? $" {fieldLabel} en Partida {row.Item}" : " itemizado";
        string undoDesc = $"Modificar{detail}";
        if (preEditSnapshot != null)
        {
            undo.PushSnapshot(preEditSnapshot, undoDesc);
            hasPendingUndoForEdit = false;
        }
        else if (!hasPendingUndoForEdit)
        {
            undo.PushUndoPoint(session, undoDesc);
            hasPendingUndoForEdit = true;
        }

        session.MarkDirty(ProjectDirtyScope.Itemizado);
        UpdateSaveState();

        bool needsCostRefresh = string.Equals(columnHeader, "Cantidad", StringComparison.OrdinalIgnoreCase);
        if (needsCostRefresh)
        {
            RefreshPresentation(includeCosts: true);
        }
        else if (string.Equals(columnHeader, "Descripción", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(columnHeader, "Ud.", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(columnHeader, "Ítem", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(columnHeader, "Observaciones", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(columnHeader, "OBSERVACIONES", StringComparison.OrdinalIgnoreCase))
        {
            RefreshPresentation(includeCosts: false);
            if (string.Equals(columnHeader, "Ítem", StringComparison.OrdinalIgnoreCase))
                SafeRefreshView();
        }
    }

    public void HandleDeleteKey()
    {
        if (!HasSelection)
            return;
        DeleteSelected();
    }

    private double completedPct;

    public double AvancePercent => completedPct;

    public void UpdateHudState(double pct)
    {
        completedPct = pct;
        OnPropertyChanged(nameof(AvancePercent));
    }

    private void ExportPdf()
    {
        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = $"Itemizado_{session.ProjectInfo.Nombre ?? "Proyecto"}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                if (OperationFailureMessages.IsFileLocked(sfd.FileName))
                {
                    WpfDialogService.Error("Archivo en uso", OperationFailureMessages.FileInUseUserMessage);
                    return;
                }

                ItemizadoPdfExporter.Export(session.ProjectInfo, Rows.ToList(), sfd.FileName);
                WpfDialogService.Info("Exportación Exitosa", $"Se ha exportado el itemizado valorado a {sfd.FileName}");
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error al exportar", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
