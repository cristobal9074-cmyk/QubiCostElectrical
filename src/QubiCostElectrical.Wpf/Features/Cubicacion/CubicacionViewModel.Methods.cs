using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IDisposable
{
    internal IDisposable SuppressSessionReload()
    {
        suppressSessionReload++;
        return new RelayDisposable(() => suppressSessionReload = Math.Max(0, suppressSessionReload - 1));
    }

    private sealed class RelayDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private PartidaListItemVm? selectedPartida;
    private string partidaFilter = "";
    private System.Windows.Threading.DispatcherTimer? partidaFilterDebounceTimer;
    private bool isLeftPanelCollapsed;
    private bool isRightPanelCollapsed;
    private bool isLoading;
    private bool hasPendingChanges;
    private string saveStatusText = "Sin cambios";
    private bool hasPendingUndoForEdit;
    private bool showEmptyState;
    // Índice código→material cacheado para validar filas. Reconstruirlo en cada tecleo
    // (antes: uno por revalidación de fila) era el mayor costo percibido al escribir en la
    // grilla; se invalida solo cuando el catálogo de materiales realmente cambia.
    private IReadOnlyDictionary<string, MaterialRow>? validationMaterialesIndex;
    // Totales
    private decimal totalMateriales;
    private decimal totalManoObra;
    private decimal totalCostoDirecto;
    private decimal precioUnitario;
    private decimal totalPartida;

    // Panel contextual
    public CubicacionRightPanelVm RightPanel { get; }

    public event Action<string>? RowAdded;
    internal void NotifyRowAdded(string rowId) => RowAdded?.Invoke(rowId);
    internal void NotifyProperty(string name) => OnPropertyChanged(name);

    public CubicacionViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Cubicacion, host.Itemizado, host.UndoService,
               host.CubicacionService, host.CubicacionTakeoffQuery, host.ProjectRepository,
               host.MaterialSearch, host.PriceBankStore, host.UserPreferencesStore)
    { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public CubicacionViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService projectWorkspace,
        CubicacionPresenter cubicacionPresenter,
        ItemizadoPresenter itemizadoPresenter,
        WpfUndoService undo,
        ICubicacionService cubSvc,
        ICubicacionTakeoffQueryService querySvc,
        IProjectRepository repo,
        IMaterialSearchService materialSearch,
        IPriceBankStore priceBankStore,
        IUserPreferencesStore? preferencesStore = null)
    {
        this.shell = shell;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.cubicacionPresenter = cubicacionPresenter;
        this.itemizadoPresenter = itemizadoPresenter;
        this.undo = undo;
        this.cubSvc = cubSvc;
        this.querySvc = querySvc;
        this.repo = repo;
        this.materialSearch = materialSearch;
        this.priceBankStore = priceBankStore;
        this.preferencesStore = preferencesStore;
        conjuntoService = new ConjuntoElectricoService();
        totalsCalculator = new CubicacionTotalsCalculator(this, cubicacionPresenter);
        autocompleteService = new CubicacionAutocompleteService(this, session, materialSearch, priceBankStore);
        sessionCoordinator = new CubicacionSessionCoordinator(this, session);
        persistenceService = new CubicacionPersistenceService(this, session, projectWorkspace, undo, shell, cubicacionPresenter, conjuntoService, priceBankStore);
        loadingService = new CubicacionLoadingService(this, session, querySvc, cubSvc);
        sectorService = new CubicacionSectorService(this, session, undo, querySvc, cubicacionPresenter);
        rowMutationService = new CubicacionRowMutationService(this, session, undo, cubSvc, cubicacionPresenter, priceBankStore);
        clipboardCoordinator = new CubicacionClipboardCoordinator(this, session, undo, cubicacionPresenter, querySvc);
        baselineCoordinator = new CubicacionBaselineCoordinator(this, session);
        navigationCoordinator = new CubicacionNavigationCoordinator(this, session, shell);
        partidaCoordinator = new CubicacionPartidaCoordinator(this, session, undo);

        RightPanel = new CubicacionRightPanelVm(session, this);
        Partidas = new ObservableCollection<PartidaListItemVm>();
        Rows = new ObservableCollection<CubicacionRowVm>();
        // Un solo punto de entrada para propagar el valor de UF del proyecto a cada fila que se
        // agrega a Rows (sin importar cuál de los ~8 sitios la crea: carga inicial, pegado,
        // duplicar, deshacer, etc.) — evita tener que tocar cada uno de esos sitios a mano y
        // olvidar alguno (una fila con ValorUfClp=0 mostraría UF UNIT siempre en 0).
        Rows.CollectionChanged += (_, e) =>
        {
            if (e.NewItems == null) return;
            foreach (CubicacionRowVm vm in e.NewItems)
                vm.ValorUfClp = session.ProjectInfo.ValorUfClp;
        };
        FilteredPartidas = new ObservableCollection<PartidaListItemVm>();

        AddRowCommand = new RelayCommand(() => rowMutationService.AddRow(), () => CanEdit && SelectedPartida?.EsPartida == true);
        QuickAddMaterialCommand = new RelayCommand(() => BeginQuickAddMaterial(), () => CanEdit && SelectedPartida?.EsPartida == true);
        PasteFromClipboardCommand = new RelayCommand(PasteFromClipboard, () => CanEdit && SelectedPartida?.EsPartida == true);
        InsertRowCommand = new RelayCommand(() => rowMutationService.InsertRow(), () => CanEdit && SelectedPartida?.EsPartida == true);
        DeleteRowCommand = new RelayCommand(() => rowMutationService.DeleteSelectedRows(), () => CanEdit && (Rows.Any(r => r.IsSelected) || SelectedRow != null));
        DuplicateRowCommand = new RelayCommand(() => rowMutationService.DuplicateSelectedRow(), () => CanEdit && (Rows.Any(r => r.IsSelected) || SelectedRow != null));
        SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !session.IsReadOnly);
        CollapseLeftPanelCommand = new RelayCommand(() => IsLeftPanelCollapsed = !IsLeftPanelCollapsed);
        CollapseRightPanelCommand = new RelayCommand(() => IsRightPanelCollapsed = !IsRightPanelCollapsed);
        ApplyMaterialCommand = new RelayCommand<MaterialRow>(m => rowMutationService.ApplyMaterial(m), m => m != null && CanEdit && SelectedPartida?.EsPartida == true);
        MoveRowUpCommand = new RelayCommand(() => rowMutationService.MoveSelectedRow(-1), () => CanEdit && rowMutationService.CanMoveRow(-1));
        MoveRowDownCommand = new RelayCommand(() => rowMutationService.MoveSelectedRow(1), () => CanEdit && rowMutationService.CanMoveRow(1));
        AplicarConjuntoCommand = new RelayCommand(AplicarConjunto, () => !session.IsReadOnly);
        CrearConjuntoDesdePartidaCommand = new RelayCommand(CrearConjuntoDesdePartida, () => !session.IsReadOnly && SelectedPartida?.EsPartida == true);
        RefreshPricesCommand = new RelayCommand(() => rowMutationService.RefreshPricesForSelectedRows(), () => CanEdit && (Rows.Any(r => r.IsSelected) || SelectedRow != null));
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        NavigateToApuCommand = new RelayCommand(NavigateToApu, () => SelectedPartida?.EsPartida == true);

        InitializeSectorMatrix();
        InitializeShortcutCommands();
        InitializePartidaPanelCommands();
        LoadPartidas();
        ScheduleCaptureBaseline();
        InitializeSessionHandlers();

        // Diferir la selección inicial al siguiente frame de render para que el clic en la
        // pestaña sea inmediatamente responsivo en lugar de bloquear el hilo UI.
        var nav = session.PendingTakeoffNavigation;
        _ = System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (nav != null)
            {
                var target = Partidas.FirstOrDefault(p => string.Equals(p.Id, nav.ItemId, StringComparison.OrdinalIgnoreCase));
                if (target != null)
                {
                    SelectPartida(target);
                    SelectRowByTarget(nav.TargetRowId, nav.TargetCode);
                }
                else
                    SelectFirstPartida();
                session.PendingTakeoffNavigation = null;
            }
            else
            {
                SelectFirstPartida();
            }

            PromptTakeoffQueueContinuation();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Avisa si quedan partidas en cola tras Cubicar multi-selección.</summary>
    internal void PromptTakeoffQueueContinuation() => navigationCoordinator.PromptTakeoffQueueContinuation();

    public void SelectRowByTarget(string? rowId, string? code) => navigationCoordinator.SelectRowByTarget(rowId, code);

    partial void InitializeSessionHandlers();

    public ObservableCollection<PartidaListItemVm> Partidas { get; }
    public ObservableCollection<PartidaListItemVm> FilteredPartidas { get; }
    public ObservableCollection<CubicacionRowVm> Rows { get; }

    // Llamado cuando cambia ValorUfClp (pestaña Moneda) — ver CubicacionSessionCoordinator,
    // ProjectSessionChangeKind.ProjectInfo. Empuja el nuevo valor a las filas YA cargadas (las
    // que se cargan después ya lo reciben solas vía Rows.CollectionChanged, arriba).
    internal void RefreshValorUfEnFilasCargadas()
    {
        decimal valorUf = session.ProjectInfo.ValorUfClp;
        foreach (var row in Rows)
            row.ValorUfClp = valorUf;
    }

    // Llamado cuando cambia el catálogo de materiales del proyecto (p. ej. al aplicar precios
    // del Banco de Precios) — ver CubicacionSessionCoordinator, ProjectSessionChangeKind.Materiales.
    // Sin esto, las líneas ya cubicadas con un código que recibió precio nuevo se quedaban con el
    // precio viejo hasta que el usuario reabría la partida.
    internal void RefreshPricesFromMaterialesCatalog()
    {
        int updated = cubSvc.SyncPricesFromCatalog(
            session.Cubicacion,
            session.Materiales,
            session.ProjectInfo.ValorUfClp,
            session.CubicacionValorizaciones);
        if (updated == 0) return;

        foreach (var row in Rows)
            row.RefreshDisplayProperties();
        MarkDirty();
        ScheduleRefreshTotals();
        NotifyCubicacionSummaryChanged();
    }

    public PartidaListItemVm? SelectedPartida
    {
        get => selectedPartida;
        set
        {
            if (ReferenceEquals(selectedPartida, value)) return;
            // El binding TwoWay del ListBox de partidas empuja aquí cualquier ítem que el
            // usuario clickee, incluidos los títulos de sección. Un título nunca debe
            // reemplazar la partida activa ni vaciar la grilla — se ignora la asignación.
            if (value != null && !value.EsPartida) return;
            if (!TryCommitOpenCell())
            {
                SuppressPartidaFocus = true;
                try { OnPropertyChanged(nameof(SelectedPartida)); }
                finally { SuppressPartidaFocus = false; }
                return;
            }
            if (selectedPartida != null) selectedPartida.IsActiva = false;
            selectedPartida = value;
            if (selectedPartida != null) selectedPartida.IsActiva = true;
            LoadCubicacionRows();
            OnPropertyChanged(nameof(SelectedPartida)); OnPropertyChanged(nameof(PartidaCodigo)); OnPropertyChanged(nameof(PartidaDescripcion));
            OnPropertyChanged(nameof(PartidaUnidad)); OnPropertyChanged(nameof(PartidaUnidadText)); OnPropertyChanged(nameof(PartidaPositionText));
            OnPropertyChanged(nameof(PartidaCantidadText)); OnPropertyChanged(nameof(PartidaCantidadToolTip)); OnPropertyChanged(nameof(PartidaObservacionesText)); OnPropertyChanged(nameof(ExcluidoCubicacion));
            OnPropertyChanged(nameof(MotivoNoCubicarText)); OnPropertyChanged(nameof(HasPartidaNote)); OnPropertyChanged(nameof(PartidaNoteLabel));
            OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(HasPartida)); OnPropertyChanged(nameof(IsDetailHeaderVisible));
            OnPropertyChanged(nameof(IsReadOnly)); OnPropertyChanged(nameof(PartidaEstadoText));
            NotifyCubicacionSummaryChanged();
            NotifyGlobalValorizacionChanged();
            ((RelayCommand)AplicarConjuntoCommand).RaiseCanExecuteChanged(); ((RelayCommand)CrearConjuntoDesdePartidaCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddRowCommand).RaiseCanExecuteChanged(); ((RelayCommand)InsertRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeletePartidaCommand).RaiseCanExecuteChanged(); ((RelayCommand)AddPartidaCommand).RaiseCanExecuteChanged();
        }
    }

    private CubicacionRowVm? selectedRow;
    public CubicacionRowVm? SelectedRow
    {
        get => selectedRow;
        set
        {
            if (SetProperty(ref selectedRow, value))
            {
                RightPanel.LoadRow(value);
            }
        }
    }

    public string PartidaFilter
    {
        get => partidaFilter;
        set
        {
            if (SetProperty(ref partidaFilter, value))
            {
                OnPropertyChanged(nameof(HasPartidaFilter));
                if (string.IsNullOrEmpty(value)) { partidaFilterDebounceTimer?.Stop(); ApplyFilter(); }
                else SchedulePartidaFilter();
            }
        }
    }

    public bool HasPartidaFilter => !string.IsNullOrWhiteSpace(partidaFilter);

    private void SchedulePartidaFilter()
    {
        partidaFilterDebounceTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        partidaFilterDebounceTimer.Stop();
        partidaFilterDebounceTimer.Tick -= OnPartidaFilterDebounceTick;
        partidaFilterDebounceTimer.Tick += OnPartidaFilterDebounceTick;
        partidaFilterDebounceTimer.Start();
    }

    private void OnPartidaFilterDebounceTick(object? sender, EventArgs e) { partidaFilterDebounceTimer?.Stop(); ApplyFilter(); }

    private CubicacionStatusFilterMode partidaStatusFilter = CubicacionStatusFilterMode.Todos;
    public CubicacionStatusFilterMode PartidaStatusFilter
    {
        get => partidaStatusFilter;
        set { if (SetProperty(ref partidaStatusFilter, value)) { OnPropertyChanged(nameof(IsPartidaFilterTodos)); OnPropertyChanged(nameof(IsPartidaFilterListas)); OnPropertyChanged(nameof(IsPartidaFilterPendientes)); ApplyFilter(); } }
    }

    public bool IsPartidaFilterTodos { get => partidaStatusFilter == CubicacionStatusFilterMode.Todos; set { if (value) PartidaStatusFilter = CubicacionStatusFilterMode.Todos; } }
    public bool IsPartidaFilterListas { get => partidaStatusFilter == CubicacionStatusFilterMode.Listas; set { if (value) PartidaStatusFilter = CubicacionStatusFilterMode.Listas; } }
    public bool IsPartidaFilterPendientes { get => partidaStatusFilter == CubicacionStatusFilterMode.Pendientes; set { if (value) PartidaStatusFilter = CubicacionStatusFilterMode.Pendientes; } }

    private int totalPartidasCount, listasPartidasCount, pendientesPartidasCount;
    public int TotalPartidasCount { get => totalPartidasCount; set => SetProperty(ref totalPartidasCount, value); }
    public int ListasPartidasCount { get => listasPartidasCount; set => SetProperty(ref listasPartidasCount, value); }
    public int PendientesPartidasCount { get => pendientesPartidasCount; set => SetProperty(ref pendientesPartidasCount, value); }
    public string TotalFilterLabel => $"Todas ({totalPartidasCount})";
    public string ListasFilterLabel => $"✓ Listas ({listasPartidasCount})";
    public string PendientesFilterLabel => $"⚠️ Pendientes ({pendientesPartidasCount})";

    public void NotifyFilterCountsChanged() { OnPropertyChanged(nameof(TotalFilterLabel)); OnPropertyChanged(nameof(ListasFilterLabel)); OnPropertyChanged(nameof(PendientesFilterLabel)); }
    internal void UpdateFilterCounts() => loadingService.UpdateFilterCounts();

    // No oculta filas (rompería los índices que usa el pegado desde Excel): busca y
    // selecciona la siguiente coincidencia, con ciclo al llegar al final de la lista.
    public string ResourceSearchText
    {
        get => navigationCoordinator.ResourceSearchText;
        set => navigationCoordinator.ResourceSearchText = value;
    }

    public CubicacionRowVm? FindNextResource() => navigationCoordinator.FindNextResource();

    public bool IsLeftPanelCollapsed
    {
        get => isLeftPanelCollapsed;
        set => SetProperty(ref isLeftPanelCollapsed, value);
    }

    public bool IsRightPanelCollapsed
    {
        get => isRightPanelCollapsed;
        set => SetProperty(ref isRightPanelCollapsed, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        internal set
        {
            if (SetProperty(ref isLoading, value))
                OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => !isLoading && SelectedPartida?.EsPartida == true && !Rows.Any(r => r.HasContent);
    public bool HasPartida => SelectedPartida?.EsPartida == true;
    public bool IsReadOnly => session.IsReadOnly || ExcluidoCubicacion;

    internal bool CanEdit => !session.IsReadOnly && !ExcluidoCubicacion;

    public bool HasPendingChanges
    {
        get => hasPendingChanges;
        internal set => SetProperty(ref hasPendingChanges, value);
    }

    public string SaveStatusText
    {
        get => saveStatusText;
        internal set
        {
            if (!SetProperty(ref saveStatusText, value))
                return;
            if (value.StartsWith("Guardado", StringComparison.Ordinal)
                || value.Contains("No se pudo", StringComparison.OrdinalIgnoreCase)
                || value.Contains("no se aplicó", StringComparison.OrdinalIgnoreCase)
                || value.Contains("Sin coincidencias", StringComparison.OrdinalIgnoreCase))
                ShellActivity.Report(value);
        }
    }

    private string formulaStatusText = "";

    public string FormulaStatusText
    {
        get => formulaStatusText;
        private set
        {
            if (SetProperty(ref formulaStatusText, value ?? ""))
                OnPropertyChanged(nameof(HasFormulaStatus));
        }
    }

    public bool HasFormulaStatus => formulaStatusText.Length > 0;

    public string FormulaDialectText => QuantityFormulaDialect.Summary;

    internal void ReportFormulaIssue(string? error)
    {
        string text = string.IsNullOrWhiteSpace(error) ? "La fórmula no se puede calcular." : error.Trim();
        if (!text.StartsWith("fx", StringComparison.OrdinalIgnoreCase))
            text = "fx · " + text;
        SetOpenEditorError(text);
        FormulaStatusText = text;
        SaveStatusText = text;
    }

    internal void ClearFormulaIssue()
    {
        if (formulaStatusText.Length == 0)
            return;
        FormulaStatusText = "";
    }

    public string PartidaEstadoText => partidaCoordinator.PartidaEstadoText;
    public bool HasPartidaNote => partidaCoordinator.HasPartidaNote;
    public string PartidaNoteLabel => partidaCoordinator.PartidaNoteLabel;

    /// <summary>Exclusión de cubicación (Ctrl+E). Independiente de la observación.</summary>
    public bool ExcluidoCubicacion
    {
        get => partidaCoordinator.ExcluidoCubicacion;
        set => partidaCoordinator.ExcluidoCubicacion = value;
    }

    /// <summary>
    /// Observación interna de la partida (valorizada o no). No excluye de cubicación y no viaja a la oferta.
    /// Para nota comercial de oferta use Observaciones en modo Detalle.
    /// </summary>
    public string MotivoNoCubicarText
    {
        get => partidaCoordinator.MotivoNoCubicarText;
        set => partidaCoordinator.MotivoNoCubicarText = value;
    }

    public IReadOnlyList<ObservationChipPreset> ObservationChips => partidaCoordinator.ObservationChips;

    public void ApplyObservationChip(string text) => partidaCoordinator.ApplyObservationChip(text);

    public void ClearObservation() => partidaCoordinator.ClearObservation();

    public string PartidaResumenText => partidaCoordinator.PartidaResumenText;

    public string PartidaCodigo
    {
        get => partidaCoordinator.PartidaCodigo;
        set => partidaCoordinator.PartidaCodigo = value;
    }

    public string PartidaDescripcion
    {
        get => partidaCoordinator.PartidaDescripcion;
        set => partidaCoordinator.PartidaDescripcion = value;
    }

    public string PartidaUnidad => partidaCoordinator.PartidaUnidad;

    public string PartidaUnidadText
    {
        get => partidaCoordinator.PartidaUnidadText;
        set => partidaCoordinator.PartidaUnidadText = value;
    }

    public string ResolveUnidadReference(string input) => partidaCoordinator.ResolveUnidadReference(input);

    public string PartidaPositionText => partidaCoordinator.PartidaPositionText;

    public string PartidaCantidadText
    {
        get => partidaCoordinator.PartidaCantidadText;
        set => partidaCoordinator.PartidaCantidadText = value;
    }

    public string PartidaCantidadToolTip => partidaCoordinator.PartidaCantidadToolTip;

    public bool HasAlertaSinValorizacionGlobal => partidaCoordinator.HasAlertaSinValorizacionGlobal;
    public bool IsPartidaUnidadMissing => partidaCoordinator.IsPartidaUnidadMissing;
    public bool IsPartidaCantidadMissing => partidaCoordinator.IsPartidaCantidadMissing;
    public string AlertaSinValorizacionGlobalMensaje => partidaCoordinator.AlertaSinValorizacionGlobalMensaje;
    public string AlertaSinValorizacionGlobalBadgeText => partidaCoordinator.AlertaSinValorizacionGlobalBadgeText;

    public void NotifyGlobalValorizacionChanged() => partidaCoordinator.NotifyGlobalValorizacionChanged();

    public Dictionary<string, decimal> BuildPartidaCantidadVariables()
        => partidaCoordinator.BuildPartidaCantidadVariables();

    public string PartidaObservacionesText
    {
        get => partidaCoordinator.PartidaObservacionesText;
        set => partidaCoordinator.PartidaObservacionesText = value;
    }

    // Totales
    public decimal TotalMateriales { get => totalMateriales; private set => SetProperty(ref totalMateriales, value); }
    public decimal TotalManoObra { get => totalManoObra; private set => SetProperty(ref totalManoObra, value); }
    public decimal TotalCostoDirecto { get => totalCostoDirecto; private set => SetProperty(ref totalCostoDirecto, value); }
    public decimal PrecioUnitario { get => precioUnitario; private set => SetProperty(ref precioUnitario, value); }
    public decimal TotalPartida { get => totalPartida; private set => SetProperty(ref totalPartida, value); }
    public decimal TotalMaterialesUf => PriceConversion.ClpToUf(TotalMateriales, ValorUfVigente);
    public decimal TotalManoObraUf => PriceConversion.ClpToUf(TotalManoObra, ValorUfVigente);
    public decimal TotalCostoDirectoUf => PriceConversion.ClpToUf(TotalCostoDirecto, ValorUfVigente);
    public decimal PrecioUnitarioUf => PriceConversion.ClpToUf(PrecioUnitario, ValorUfVigente);
    public decimal TotalPartidaUf => PriceConversion.ClpToUf(TotalPartida, ValorUfVigente);
    private decimal ValorUfVigente => session.ValorUfClp > 0m ? session.ValorUfClp : PriceConversion.DefaultValorUfClp;

    /// <summary>
    /// Regla de moneda del proyecto: con CLP no se expone ninguna cifra en UF (columnas UF UNIT y
    /// SUBTOTAL UF, equivalentes UF de la barra de totales y equivalencia del panel derecho).
    /// </summary>
    public bool MuestraUf
        => !string.Equals(session.ProjectInfo.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);

    /// <summary>Notifica el cambio de política de moneda para que la vista reaplique la grilla.</summary>
    internal void NotifyMonedaChanged()
    {
        OnPropertyChanged(nameof(MuestraUf));
        OnPropertyChanged(nameof(TotalMaterialesUf));
        OnPropertyChanged(nameof(TotalManoObraUf));
        OnPropertyChanged(nameof(TotalCostoDirectoUf));
        OnPropertyChanged(nameof(PrecioUnitarioUf));
        OnPropertyChanged(nameof(TotalPartidaUf));
    }

    // Comandos
    public ICommand AddRowCommand { get; }
    public ICommand QuickAddMaterialCommand { get; }
    public ICommand PasteFromClipboardCommand { get; }
    public ICommand InsertRowCommand { get; }
    public ICommand DeleteRowCommand { get; }
    public ICommand DuplicateRowCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CollapseLeftPanelCommand { get; }
    public ICommand CollapseRightPanelCommand { get; }
    public ICommand ApplyMaterialCommand { get; }
    public ICommand MoveRowUpCommand { get; }
    public ICommand MoveRowDownCommand { get; }
    public ICommand AplicarConjuntoCommand { get; }
    public ICommand CrearConjuntoDesdePartidaCommand { get; }
    public ICommand RefreshPricesCommand { get; private set; } = null!;
    public ICommand OpenProjectCommand { get; }
    public ICommand NavigateToApuCommand { get; }

    private void NavigateToApu() => navigationCoordinator.NavigateToApu();

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    internal void UpdateEmptyState() =>
        ShowEmptyState = !projectWorkspace.HasOpenProject;

    public ProjectFileData? CaptureSnapshot() => baselineCoordinator.CaptureSnapshot();

    // IRefreshablePage
    public void Refresh()
    {
        UpdateEmptyState();
        validationMaterialesIndex = null;
        var prev = selectedPartida;
        LoadPartidas();

        // Re-seleccionar la partida en el mismo tick (síncrono) para evitar que WPF pinte
        // el estado intermedio con la grilla vacía — que era la causa del parpadeo en Ctrl+Z.
        if (prev != null)
        {
            var still = Partidas.FirstOrDefault(p => p.Id == prev.Id);
            if (still != null) { selectedPartida = null; SelectedPartida = still; }
            else SelectFirstPartida();
        }
        else SelectFirstPartida();
        if (!hasPendingChanges)
            ScheduleCaptureBaseline();
    }

    // IPageWithPendingChanges / IPageWithCommitChanges — edición en vivo sobre session.Cubicacion.
    // Leave/Save persiste porque MarkDirty ya llama session.MarkDirty(ProjectDirtyScope.Cubicacion).
    // Discard restaura baseline JSON de cubicación (+ capas) capturado en Load/Refresh/Apply.
    bool IPageWithPendingChanges.HasPendingChanges => hasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() => TryDiscardChanges();
    bool IPageWithCommitChanges.TryApplyChanges() => TryApplyChanges();
    bool IPageWithOpenEditor.TryCommitOpenEditor() => TryCommitOpenCell();
    string IPageWithOpenEditor.OpenEditorError => OpenEditorError;

    /// <summary>La vista lo asigna en su constructor. Sin vista, no hay editor que confirmar.</summary>
    internal Func<bool>? CommitOpenCellHandler { get; set; }

    internal bool SuppressPartidaFocus { get; private set; }

    internal string OpenEditorError { get; private set; } = "";

    internal void SetOpenEditorError(string? error) => OpenEditorError = error ?? "";

    internal void ClearOpenEditorError() => OpenEditorError = "";

    internal bool TryCommitOpenCell()
    {
        var handler = CommitOpenCellHandler;
        if (handler == null)
        {
            ClearOpenEditorError();
            return true;
        }

        return handler();
    }

    internal QuantityFormulaResult EvaluateOpenSectorExpression(CubicacionRowVm row, int offset, string? text)
        => querySvc.EvaluateCandidateExpression(session, row.Source, offset, text);

    public bool TryDiscardChanges() => baselineCoordinator.TryDiscardChanges();

    public bool TryApplyChanges() => baselineCoordinator.TryApplyChanges();

    internal void ScheduleCaptureBaseline() => baselineCoordinator.ScheduleCaptureBaseline();

    internal void CaptureBaseline() => baselineCoordinator.CaptureBaseline();

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;
        PasteClipboardDirect(text, showPreviewDialog: true);
    }
}
