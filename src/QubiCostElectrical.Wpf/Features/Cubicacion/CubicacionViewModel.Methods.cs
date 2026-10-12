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

    public CubicacionViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Cubicacion, host.Itemizado, host.UndoService,
               host.Services.Cubicacion, host.Services.CubicacionTakeoffQuery, host.Services.ProjectRepository,
               host.Services.MaterialSearch, host.Services.PriceBankStore)
    { }

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
        IPriceBankStore priceBankStore)
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
        conjuntoService = new ConjuntoElectricoService();
        totalsCalculator = new CubicacionTotalsCalculator(this, cubicacionPresenter);
        autocompleteService = new CubicacionAutocompleteService(this, session, materialSearch, priceBankStore);
        sessionCoordinator = new CubicacionSessionCoordinator(this, session);
        persistenceService = new CubicacionPersistenceService(this, session, projectWorkspace, undo, shell, cubicacionPresenter, conjuntoService);
        loadingService = new CubicacionLoadingService(this, session, querySvc, cubSvc);
        sectorService = new CubicacionSectorService(this, session, undo, querySvc, cubicacionPresenter);
        rowMutationService = new CubicacionRowMutationService(this, session, undo, cubSvc, cubicacionPresenter, priceBankStore);
        clipboardCoordinator = new CubicacionClipboardCoordinator(this, session, undo, cubicacionPresenter, querySvc);

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
    internal void PromptTakeoffQueueContinuation()
    {
        var remaining = session.PendingTakeoffQueueCount;
        if (remaining <= 0)
            return;

        ShellNotificationService.Instance.ShowInfo(
            "Cola de cubicación",
            remaining == 1
                ? "Queda 1 partida pendiente."
                : $"Quedan {remaining:N0} partidas pendientes.",
            "Siguiente",
            AdvanceTakeoffQueue);
    }

    private void AdvanceTakeoffQueue()
    {
        var nextId = session.TryDequeueNextTakeoff();
        if (string.IsNullOrWhiteSpace(nextId))
            return;

        var target = Partidas.FirstOrDefault(p =>
            string.Equals(p.Id, nextId, StringComparison.OrdinalIgnoreCase) && p.EsPartida);
        if (target == null)
        {
            WpfDialogService.Info("Cubicar", "La siguiente partida de la cola ya no está disponible.");
            PromptTakeoffQueueContinuation();
            return;
        }

        SelectPartida(target);
        PromptTakeoffQueueContinuation();
    }

    public void SelectRowByTarget(string? rowId, string? code)
    {
        if (Rows.Count == 0) return;
        var match = Rows.FirstOrDefault(r =>
            (!string.IsNullOrWhiteSpace(rowId) && string.Equals(r.Id, rowId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(code) && string.Equals(r.Codigo, code, StringComparison.OrdinalIgnoreCase)));
        if (match != null)
        {
            SelectedRow = match;
        }
    }

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
            if (selectedPartida != null) selectedPartida.IsActiva = false;
            selectedPartida = value;
            if (selectedPartida != null) selectedPartida.IsActiva = true;
            OnPropertyChanged(nameof(SelectedPartida)); OnPropertyChanged(nameof(PartidaCodigo)); OnPropertyChanged(nameof(PartidaDescripcion));
            OnPropertyChanged(nameof(PartidaUnidad)); OnPropertyChanged(nameof(PartidaUnidadText)); OnPropertyChanged(nameof(PartidaPositionText));
            OnPropertyChanged(nameof(PartidaCantidadText)); OnPropertyChanged(nameof(PartidaObservacionesText)); OnPropertyChanged(nameof(ExcluidoCubicacion));
            OnPropertyChanged(nameof(MotivoNoCubicarText)); OnPropertyChanged(nameof(HasPartidaNote)); OnPropertyChanged(nameof(PartidaNoteLabel));
            OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(HasPartida)); OnPropertyChanged(nameof(IsDetailHeaderVisible));
            OnPropertyChanged(nameof(IsReadOnly)); OnPropertyChanged(nameof(PartidaEstadoText));
            NotifyCubicacionSummaryChanged();
            LoadCubicacionRows();
            ((RelayCommand)AplicarConjuntoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)InsertRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeletePartidaCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddPartidaCommand).RaiseCanExecuteChanged();
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

    public string PartidaFilter { get => partidaFilter; set { if (SetProperty(ref partidaFilter, value)) SchedulePartidaFilter(); } }

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

    private string resourceSearchText = "";

    // No oculta filas (rompería los índices que usa el pegado desde Excel): busca y
    // selecciona la siguiente coincidencia, con ciclo al llegar al final de la lista.
    public string ResourceSearchText
    {
        get => resourceSearchText;
        set => SetProperty(ref resourceSearchText, value);
    }

    public CubicacionRowVm? FindNextResource()
    {
        var needle = resourceSearchText.Trim();
        if (string.IsNullOrEmpty(needle) || Rows.Count == 0)
            return null;

        int startIndex = SelectedRow != null ? Rows.IndexOf(SelectedRow) + 1 : 0;
        for (int i = 0; i < Rows.Count; i++)
        {
            var row = Rows[(startIndex + i) % Rows.Count];
            if (row.Codigo.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.Descripcion.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                SelectedRow = row;
                return row;
            }
        }
        return null;
    }

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
        internal set => SetProperty(ref saveStatusText, value);
    }

    public string PartidaEstadoText
    {
        get
        {
            if (!HasPartida)
                return "Sin partida seleccionada";
            if (HasPartidaNote)
                return MotivoNoCubicarText.Trim();
            return SelectedPartida?.TieneCubicacion == true ? "Partida cubicada" : "Partida vacía";
        }
    }

    /// <summary>Hay texto de observación (no implica exclusión).</summary>
    public bool HasPartidaNote => !string.IsNullOrWhiteSpace(MotivoNoCubicarText);

    public string PartidaNoteLabel => HasPartidaNote ? "Observación:" : "";

    /// <summary>Exclusión de cubicación (Ctrl+E). Independiente de la observación.</summary>
    public bool ExcluidoCubicacion
    {
        get => SelectedPartida?.ExcluidoCubicacion == true;
        set
        {
            if (session.IsReadOnly || SelectedPartida?.EsPartida != true)
                return;

            var item = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null || item.ExcluidoCubicacion == value)
                return;

            undo.PushUndoPoint(session,
                value ? $"Excluir de cubicación {SelectedPartida.Codigo}" : $"Incluir en cubicación {SelectedPartida.Codigo}");
            item.ExcluidoCubicacion = value;
            SelectedPartida.ExcluidoCubicacion = value;
            MarkDirty();
            OnPropertyChanged(nameof(ExcluidoCubicacion));
            OnPropertyChanged(nameof(IsReadOnly));
            OnPropertyChanged(nameof(PartidaEstadoText));
            ((RelayCommand)AddRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)InsertRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AplicarConjuntoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)OpenScTomarCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Observación interna de la partida (valorizada o no). No excluye de cubicación y no viaja a la oferta.
    /// Para nota comercial de oferta use Observaciones en modo Detalle.
    /// </summary>
    public string MotivoNoCubicarText
    {
        get
        {
            if (selectedPartida == null)
                return "";
            var item = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            return item?.MotivoNoCubicar ?? "";
        }
        set
        {
            if (session.IsReadOnly || selectedPartida?.EsPartida != true)
                return;
            var item = session.Itemizado.FirstOrDefault(x =>
                string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return;

            string clean = value ?? "";
            if (string.Equals(item.MotivoNoCubicar, clean, StringComparison.Ordinal))
                return;

            undo.PushUndoPoint(session, $"Observación partida {selectedPartida.Codigo}");
            item.MotivoNoCubicar = clean;
            if (!string.IsNullOrWhiteSpace(clean))
            {
                ObservationMemoryStore.Remember(UserPreferencesStore.Load(), clean);
            }

            OnPropertyChanged(nameof(MotivoNoCubicarText));
            OnPropertyChanged(nameof(HasPartidaNote));
            OnPropertyChanged(nameof(PartidaNoteLabel));
            OnPropertyChanged(nameof(PartidaEstadoText));
            MarkDirty();
        }
    }

    public IReadOnlyList<ObservationChipPreset> ObservationChips => ObservationMemoryStore.StandardChips;

    public void ApplyObservationChip(string text)
    {
        if (session.IsReadOnly || selectedPartida?.EsPartida != true)
            return;

        MotivoNoCubicarText = text;
        SaveStatusText = $"Observación aplicada: '{text}'.";
    }

    public void ClearObservation()
    {
        if (session.IsReadOnly || selectedPartida?.EsPartida != true)
            return;

        MotivoNoCubicarText = string.Empty;
        SaveStatusText = "Observación eliminada.";
    }

    public string PartidaResumenText => !HasPartida
        ? "Seleccione una partida para ver el detalle"
        : $"{Rows.Count(r => r.HasContent):N0} recursos · $ {TotalPartida:N0} CLP";

    public string PartidaCodigo
    {
        get => selectedPartida?.Codigo ?? "";
        set
        {
            if (session.IsReadOnly || selectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (selectedPartida.Codigo == clean) return;

            undo.PushUndoPoint(session, $"Cambiar ítem partida {selectedPartida.Codigo} a {clean}");
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                item.Item = clean;

            selectedPartida.Codigo = clean;
            OnPropertyChanged(nameof(PartidaCodigo));
            OnPropertyChanged(nameof(PartidaPositionText));
            MarkDirty();
        }
    }

    public string PartidaDescripcion
    {
        get => selectedPartida?.Descripcion ?? "";
        set
        {
            if (session.IsReadOnly || selectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (selectedPartida.Descripcion == clean) return;

            undo.PushUndoPoint(session, $"Cambiar nombre partida {selectedPartida.Codigo}");
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                item.Descripcion = clean;

            selectedPartida.Descripcion = clean;
            OnPropertyChanged(nameof(PartidaDescripcion));
            MarkDirty();
        }
    }
    public string PartidaUnidad => selectedPartida?.Unidad ?? "";

    // Unidad editable de la partida (modo Detalle) — cuando el itemizado no trae unidad, se
    // escribe manual aquí y esa es la que viaja al Resumen del presupuesto.
    public string PartidaUnidadText
    {
        get => PartidaUnidad;
        set
        {
            if (session.IsReadOnly) return;
            if (selectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (selectedPartida.Unidad == clean) return;

            undo.PushUndoPoint(session, $"Cambiar unidad partida {selectedPartida.Codigo}");
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item != null)
                item.Unidad = clean;

            selectedPartida.Unidad = clean;
            OnPropertyChanged(nameof(PartidaUnidadText));
            OnPropertyChanged(nameof(PartidaUnidad));
            MarkDirty();
        }
    }

    public string PartidaPositionText
    {
        get
        {
            if (selectedPartida == null) return "";
            var partidas = FilteredPartidas.Where(p => p.EsPartida).ToList();
            int idx = partidas.IndexOf(selectedPartida);
            return idx >= 0 ? $"Partida {idx + 1}/{partidas.Count}" : "";
        }
    }

    public string PartidaCantidadText
    {
        get
        {
            if (selectedPartida == null) return "";
            return selectedPartida.Cantidad.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
        }
        set
        {
            if (session.IsReadOnly) return;
            if (selectedPartida == null) return;
            string clean = (value ?? "").Trim();
            decimal parsed;
            bool success;
            if (string.IsNullOrEmpty(clean))
            {
                parsed = 0m;
                success = true;
            }
            else if (QuantityFormulaService.IsFormulaLike(clean))
            {
                // Fórmula de suma/resta/multiplicación/división sobre las cantidades de los
                // recursos de abajo — cada uno se referencia como R1, R2... (R{n} = fila n de
                // la grilla, en el orden visible). Solo se guarda el resultado; la fórmula en
                // sí no persiste (no hay campo para eso en ItemizadoRow).
                var variables = BuildPartidaCantidadVariables();
                var result = QuantityFormulaService.Evaluate(clean, variables);
                success = result.Success;
                parsed = result.Value;
            }
            else
            {
                success = decimal.TryParse(clean, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out parsed);
            }

            if (success)
            {
                if (selectedPartida.Cantidad != parsed)
                {
                    undo.PushUndoPoint(session, $"Modificar cantidad en Partida {selectedPartida.Codigo} ({selectedPartida.Cantidad:G29} → {parsed:G29})");

                    var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        item.Cantidad = parsed;
                    }

                    selectedPartida.Cantidad = parsed;
                    OnPropertyChanged(nameof(PartidaCantidadText));
                    MarkDirty();
                    ScheduleRefreshTotals();
                }
                else
                {
                    OnPropertyChanged(nameof(PartidaCantidadText));
                }
            }
            else
            {
                OnPropertyChanged(nameof(PartidaCantidadText));
            }
        }
    }

    // R1, R2... = Cantidad de cada recurso de la partida activa, en el orden visible de la
    // grilla — para que la Cantidad oficial (CANT, modo Detalle) pueda sumar/restar/multiplicar/
    // dividir filas de recursos escribiendo o "pickeando" referencias tipo Excel.
    private Dictionary<string, decimal> BuildPartidaCantidadVariables()
        => PartidaCantidadVariablesBuilder.Build(Rows.Select(r => r.Cantidad).ToList());

    // Observación de la partida para la constructora — a diferencia de CubicacionRowVm.NotaInterna
    // (por recurso, solo revisión interna), esta viaja al Resumen/oferta. Se guarda en
    // ItemizadoRow.Observaciones (campo ya existente, sin usar en la UI hasta ahora).
    public string PartidaObservacionesText
    {
        get
        {
            var item = selectedPartida == null
                ? null
                : session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            return item?.Observaciones ?? "";
        }
        set
        {
            if (session.IsReadOnly) return;
            if (selectedPartida == null) return;
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null) return;

            string clean = value ?? "";
            if (item.Observaciones == clean) return;

            undo.PushUndoPoint(session, $"Editar observación de {selectedPartida.Codigo}");
            item.Observaciones = clean;
            OnPropertyChanged(nameof(PartidaObservacionesText));
            MarkDirty();
        }
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
    public ICommand RefreshPricesCommand { get; private set; } = null!;
    public ICommand OpenProjectCommand { get; }
    public ICommand NavigateToApuCommand { get; }

    private void NavigateToApu()
    {
        if (SelectedPartida != null && !string.IsNullOrWhiteSpace(SelectedPartida.Id))
        {
            session.PendingItemizadoSelectionId = SelectedPartida.Id;
        }
        shell.RequestNavigate(QubiCostElectrical.Wpf.Navigation.ShellRoute.Apu);
    }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    internal void UpdateEmptyState() =>
        ShowEmptyState = !projectWorkspace.HasOpenProject;

    public ProjectFileData? CaptureSnapshot() => session.IsReadOnly ? null : session.CreateSnapshot();

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

    public bool TryDiscardChanges()
    {
        if (!hasPendingChanges)
            return true;

        RestoreBaseline();
        HasPendingChanges = false;
        SaveStatusText = "Cambios descartados";
        Refresh();
        return true;
    }

    public bool TryApplyChanges()
    {
        if (session.IsReadOnly)
            return !hasPendingChanges;

        if (!session.IsDirty)
        {
            using (SuppressSessionReload())
                session.MarkDirty(ProjectDirtyScope.Cubicacion);
        }

        CaptureBaseline();
        MarkClean();
        return true;
    }

    internal void ScheduleCaptureBaseline()
    {
        // Serializar miles de filas en el ctor/Refresh bloquea la UI (~2s en proyectos grandes).
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            // Tests headless: sync solo en proyectos chicos (perf 800×15 no debe pagar JSON).
            if (session.Cubicacion.Count <= 2_000)
                CaptureBaseline();
            return;
        }

        _ = dispatcher.InvokeAsync(() =>
        {
            if (!hasPendingChanges)
                CaptureBaseline();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    internal void CaptureBaseline()
    {
        baselineCubicacion = CloneJson(session.Cubicacion.ToList());
        baselineRecursos = CloneJson(session.CubicacionRecursos.ToList());
        baselineZonas = CloneJson(session.CubicacionZonaCantidades.ToList());
        baselineValorizaciones = CloneJson(session.CubicacionValorizaciones.ToList());
    }

    private void RestoreBaseline()
    {
        if (baselineCubicacion == null)
            return;

        ReplaceList(session.Cubicacion, CloneJson(baselineCubicacion));
        if (baselineRecursos != null)
            ReplaceList(session.CubicacionRecursos, CloneJson(baselineRecursos));
        if (baselineZonas != null)
            ReplaceList(session.CubicacionZonaCantidades, CloneJson(baselineZonas));
        if (baselineValorizaciones != null)
            ReplaceList(session.CubicacionValorizaciones, CloneJson(baselineValorizaciones));
    }

    private static void ReplaceList<T>(List<T> target, List<T> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    private static T CloneJson<T>(T source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source, BaselineJsonOptions);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, BaselineJsonOptions)
               ?? throw new InvalidOperationException($"No se pudo clonar {typeof(T).Name} para baseline Cubicación.");
    }

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;
        PasteClipboardDirect(text, showPreviewDialog: true);
    }
}
