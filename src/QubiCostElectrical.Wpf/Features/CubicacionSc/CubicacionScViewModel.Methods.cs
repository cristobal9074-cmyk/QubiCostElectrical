using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed partial class CubicacionScViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public event Action<string>? CircuitoAdded;
    public event Action<CircuitoScRowVm>? CircuitoRemoving;
    /// <summary>
    /// Disparado antes de recargar la grilla (cambio de tablero o perspectiva).
    /// El code-behind debe confirmar la celda en edición para evitar que el valor
    /// pendiente de commit se pierda cuando Circuitos.Clear() destruya las filas.
    /// </summary>
    public event Action? RequestCommitGridEdit;

    public CubicacionScViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.UndoService, host.CubicacionSCReportExcelExporter) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public CubicacionScViewModel(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        WpfUndoService undo,
        ICubicacionSCReportExcelExporter? cubScExcel = null) : base(session)
    {
        this.shell = shell;
        this.workspace = workspace;
        this.undo = undo;
        this.session = session;
        this.cubScExcel = cubScExcel ?? new CubicacionSCTechnicalReportExcelExporter();
        this.exportCoordinator = new CubicacionScExportCoordinator(this.cubScExcel);
        this.clipboardCoordinator = new CubicacionScClipboardCoordinator();

        AddCuadroCommand = new RelayCommand(AddCuadro, () => !showEmptyState && CanEdit);
        RemoveCuadroCommand = new RelayCommand<object>(RemoveCuadro, param => CanEdit && (param is CuadroScRowVm || selectedCuadro != null));
        AddCircuitoCommand = new RelayCommand(AddCircuito, () => selectedCuadro != null && CanEdit);
        RemoveCircuitoCommand = new RelayCommand(RemoveCircuito, () => selectedCircuito != null && CanEdit);
        CopyCircuitoCommand = new RelayCommand(CopyCircuito, () => selectedCircuito != null && CanEdit);
        PasteCircuitoCommand = new RelayCommand(PasteCircuito, () => clipboardCoordinator.HasClipboard && selectedCircuito != null && CanEdit);
        DuplicateCircuitoCommand = new RelayCommand(DuplicateCircuito, () => selectedCircuito != null && selectedCuadro != null && CanEdit);
        AddArtefactoCommand = new RelayCommand(AddArtefacto, () => selectedCircuito != null && CanEdit && !string.IsNullOrWhiteSpace(NuevoArtefactoNombre));
        RemoveArtefactoCommand = new RelayCommand<ArtefactoScRowVm>(RemoveArtefacto, a => a != null && CanEdit);
        ApplyCommand = new RelayCommand(ApplyChanges, () => HasPendingChanges && CanEdit);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        OpenCatalogoCommand = new RelayCommand(AbrirCatalogo, () => !showEmptyState && CanEdit);
        OpenArtefactosCommand = new RelayCommand<CircuitoScRowVm>(AbrirArtefactos, c => c != null && CanEdit);
        OpenResumenCommand = new RelayCommand(AbrirResumen, () => !showEmptyState);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => !showEmptyState);
        BalancearFasesCommand = new RelayCommand(BalancearFases, () => selectedCuadro != null && CanEdit);
        ExportarCuadroSecCommand = new RelayCommand(ExportarCuadroSec, () => !showEmptyState);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => HasSearchText);
        SaveCommand = new RelayCommand(async () =>
        {
            TryApplyChanges();
            await workspace.SaveAsync();
            StatusMessage = "Cambios guardados.";
        }, () => !showEmptyState && CanEdit);
        SelectPerspectiveCommand = new RelayCommand<string>(SelectPerspective);
        MoveCuadroPerspectiveCommand = new RelayCommand<CuadroScRowVm>(MoveCuadroToOtherPerspective, _ => CanEdit);
        ToggleAdvancedModeCommand = new RelayCommand(() =>
        {
            isAdvancedMode = !isAdvancedMode;
            OnPropertyChanged(nameof(IsAdvancedMode));
            OnPropertyChanged(nameof(AdvancedModeLabel));
        });

        Refresh();
    }

    public ICommand ToggleAdvancedModeCommand { get; }
    public ICommand SelectPerspectiveCommand { get; }
    public ICommand MoveCuadroPerspectiveCommand { get; }

    public bool IsPerspectiveGenerales => currentPerspective == CubScPerspective.Generales;
    public bool IsPerspectiveDeptos => currentPerspective == CubScPerspective.Deptos;
    public string SidebarHeaderTitle => IsPerspectiveDeptos ? "TABLEROS DEPTOS" : "TABLEROS GENERALES";
    public string PlanillaTitle => IsPerspectiveDeptos ? "CUBICACIÓN DE CIRCUITOS · DEPARTAMENTOS" : "CUBICACIÓN DE CIRCUITOS · CTOS GENERALES";
    public int CuadrosFiltradosCount => CuadrosFiltrados.Count();

    public bool ShowColUbicacion => IsPerspectiveGenerales;
    public bool ShowColLBand => IsPerspectiveGenerales;
    public bool ShowColBandeja => IsPerspectiveGenerales;
    public bool ShowColBandejaAdvanced => IsPerspectiveGenerales && isAdvancedMode;
    public bool ShowColTmf => IsPerspectiveGenerales;

    public void SelectPerspective(string? perspective)
    {
        var target = string.Equals(perspective, "Deptos", StringComparison.OrdinalIgnoreCase)
            ? CubScPerspective.Deptos
            : CubScPerspective.Generales;

        if (currentPerspective == target) return;
        currentPerspective = target;

        OnPropertyChanged(nameof(IsPerspectiveGenerales));
        OnPropertyChanged(nameof(IsPerspectiveDeptos));
        OnPropertyChanged(nameof(SidebarHeaderTitle));
        OnPropertyChanged(nameof(PlanillaTitle));
        OnPropertyChanged(nameof(CuadrosFiltrados));
        OnPropertyChanged(nameof(CuadrosFiltradosCount));
        OnPropertyChanged(nameof(ShowColUbicacion));
        OnPropertyChanged(nameof(ShowColLBand));
        OnPropertyChanged(nameof(ShowColBandeja));
        OnPropertyChanged(nameof(ShowColBandejaAdvanced));
        OnPropertyChanged(nameof(ShowColTmf));

        if (selectedCuadro == null || (currentPerspective == CubScPerspective.Deptos ? !selectedCuadro.IsDepto : selectedCuadro.IsDepto))
        {
            SelectedCuadro = CuadrosFiltrados.FirstOrDefault();
        }
        UpdateStatusMessage();
    }

    public void MoveCuadroToOtherPerspective(CuadroScRowVm? target = null)
    {
        var cuadro = target ?? selectedCuadro;
        if (cuadro == null || !CanEdit) return;

        undo.PushUndoPoint(session, "Mover tablero de menú");
        pendingUndoForEdit = true;

        cuadro.Categoria = cuadro.IsDepto ? "Generales" : "Deptos";

        OnPropertyChanged(nameof(CuadrosFiltrados));
        OnPropertyChanged(nameof(CuadrosFiltradosCount));

        if (selectedCuadro == cuadro)
        {
            SelectedCuadro = CuadrosFiltrados.FirstOrDefault();
        }
        MarkDirty();
    }

    /// <summary>Modo avanzado: muestra todas las columnas de la planilla de circuitos.
    /// En modo entrada (false) solo se muestran las columnas esenciales: N°, Sist., Alim., Ubicación, Recorridos y Totales.</summary>
    public bool IsAdvancedMode
    {
        get => isAdvancedMode;
        private set
        {
            if (isAdvancedMode == value) return;
            isAdvancedMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AdvancedModeLabel));
            OnPropertyChanged(nameof(ShowColBandejaAdvanced));
        }
    }

    public string AdvancedModeLabel => isAdvancedMode ? "Vista entrada" : "Vista avanzada";

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (suppressSessionReload)
            return;

        // El guardado sustituye la sesión por un clon. La grilla sigue en los circuitos
        // anteriores: si Alt+A confirmó artefactos ahí, hay que pasarlos al clon que
        // lee el resumen y el próximo autoguardado.
        if (e.Kind == ProjectSessionChangeKind.Saved)
        {
            if (SincronizarGrillaConSesion())
                MarkDirty();
            return;
        }

        // Edición propia ya mutó session + UI; no recargar (borraría HasPendingChanges).
        if (HasPendingChanges &&
            string.Equals(e.Scope, "cubicacion_sc", StringComparison.OrdinalIgnoreCase))
            return;

        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);
    }

    private CubicacionScConfig Config => session.CubicacionScConfig;

    // Antes cada propiedad reconstruía un ObservableCollection nuevo en cada acceso — y al
    // estar bindeadas dentro de la plantilla de fila/celda del DataGrid (ver
    // CubicacionScView.xaml), WPF las releía por cada fila virtualizada. Ahora son colecciones
    // estables que se repueblan solo cuando cambia Config (RefreshCatalogCollections, llamado
    // desde Refresh()) o cuando se agrega un artefacto nuevo al catálogo.
    private readonly ObservableCollection<string> tipos = new();
    private readonly ObservableCollection<string> fases = new();
    private readonly ObservableCollection<string> tiposRecorrido = new();
    private readonly ObservableCollection<string> tiposBandeja = new();
    private readonly ObservableCollection<string> tiposTuberia = new();
    private readonly ObservableCollection<string> diametrosTuberia = new();
    private readonly ObservableCollection<string> tiposConductor = new();
    private readonly ObservableCollection<string> seccionesConductor = new();
    private readonly ObservableCollection<string> artefactosCatalogo = new();

    public ObservableCollection<string> Tipos => tipos;
    public ObservableCollection<string> Fases => fases;
    public ObservableCollection<string> TiposRecorrido => tiposRecorrido;
    public ObservableCollection<string> TiposBandeja => tiposBandeja;
    public ObservableCollection<string> TiposTuberia => tiposTuberia;
    public ObservableCollection<string> DiametrosTuberia => diametrosTuberia;
    public ObservableCollection<string> TiposConductor => tiposConductor;
    public ObservableCollection<string> SeccionesConductor => seccionesConductor;
    public ObservableCollection<string> ArtefactosCatalogo => artefactosCatalogo;

    private void RefreshCatalogCollections()
    {
        // Códigos de sistemas de circuito desde el catálogo del proyecto (A, E, CL, CO...)
        var sistemasCircuito = Config.SistemasCircuito;
        if (sistemasCircuito == null || sistemasCircuito.Count == 0)
            sistemasCircuito = CubicacionScConfig.Default().SistemasCircuito;

        ReplaceContents(tipos, sistemasCircuito.Select(s => s.Codigo).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase));
        // Fases abreviadas: Monofásico (M), Trifásico (T), Emergencia (EM)
        ReplaceContents(fases, new List<string> { "M", "T", "EM" });
        ReplaceContents(tiposRecorrido, Config.TiposRecorrido);
        ReplaceContents(tiposBandeja, Config.TiposBandeja);
        ReplaceContents(tiposTuberia, Config.TiposTuberia);
        ReplaceContents(diametrosTuberia, Config.DiametrosTuberia);
        ReplaceContents(tiposConductor, Config.TiposConductor);
        ReplaceContents(seccionesConductor, Config.SeccionesConductor);
        ReplaceContents(artefactosCatalogo, Config.Artefactos.Select(a => a.Nombre));
    }

    private static void ReplaceContents(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source)
            target.Add(item);
    }

    // Autocompleta la categoría al elegir/crear un artefacto: si el nombre ya existe en el
    // catálogo del proyecto se hereda su categoría; si es nuevo, va a "Otro" y el catálogo se
    // amplía con él para la próxima vez (mismo patrón que el catálogo de materiales en
    // Cubicación: primero se busca, si no existe se crea al vuelo).
    private string ResolveCategoriaYRegistrar(string nombre)
    {
        var existente = Config.Artefactos.FirstOrDefault(a =>
            string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        if (existente != null)
            return existente.Categoria;

        Config.Artefactos.Add(new ArtefactoCatalogoItem { Nombre = nombre, Categoria = "Otro" });
        artefactosCatalogo.Add(nombre);
        return "Otro";
    }

    public string NuevoArtefactoNombre
    {
        get => nuevoArtefactoNombre;
        set
        {
            if (!SetProperty(ref nuevoArtefactoNombre, value)) return;
            ((RelayCommand)AddArtefactoCommand).RaiseCanExecuteChanged();
        }
    }

    public decimal NuevoArtefactoCantidad
    {
        get => nuevoArtefactoCantidad;
        set => SetProperty(ref nuevoArtefactoCantidad, value);
    }


    public ObservableCollection<CuadroScRowVm> Cuadros { get; } = new();
    public ObservableCollection<CircuitoScRowVm> Circuitos { get; } = new();

    public event Action? RequestClearGridSelection;

    public CuadroScRowVm? SelectedCuadro
    {
        get => selectedCuadro;
        set
        {
            if (selectedCuadro == value) return;
            RequestClearGridSelection?.Invoke();
            RequestCommitGridEdit?.Invoke();
            if (!SetProperty(ref selectedCuadro, value))
                return;
            LoadCircuitos();
            OnPropertyChanged(nameof(HasSelectedCuadro));
            OnPropertyChanged(nameof(HasPendingChanges));
            OnPropertyChanged(nameof(CircuitosFiltrados));
            OnPropertyChanged(nameof(KpiPotenciaText));
            OnPropertyChanged(nameof(KpiDesbalanceText));
            OnPropertyChanged(nameof(IsDesbalanceConformeSec));
            ((RelayCommand)BalancearFasesCommand).RaiseCanExecuteChanged();
            ((RelayCommand<object>)RemoveCuadroCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddCircuitoCommand).RaiseCanExecuteChanged();
        }
    }

    public bool HasSelectedCuadro => SelectedCuadro != null;

    public CircuitoScRowVm? SelectedCircuito
    {
        get => selectedCircuito;
        set
        {
            if (!SetProperty(ref selectedCircuito, value)) return;
            ((RelayCommand)RemoveCircuitoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CopyCircuitoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DuplicateCircuitoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddArtefactoCommand).RaiseCanExecuteChanged();
            ((RelayCommand<ArtefactoScRowVm>)RemoveArtefactoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)PasteCircuitoCommand).RaiseCanExecuteChanged();
        }
    }

    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetProperty(ref statusMessage, value);
    }

    // ── Filtro de Pisos / búsqueda ───────────────────────────────────────────
    private const string TodosPisos = "Todos los pisos";
    private string pisoFiltro = TodosPisos;
    private string searchText = "";

    public ObservableCollection<string> PisosDisponibles { get; } = new();

    public string SearchText
    {
        get => searchText;
        set
        {
            if (!SetProperty(ref searchText, value)) return;
            OnPropertyChanged(nameof(HasSearchText));
            OnPropertyChanged(nameof(CuadrosFiltrados));
            OnPropertyChanged(nameof(CuadrosFiltradosCount));
            OnPropertyChanged(nameof(CircuitosFiltrados));
            if (ClearSearchCommand is RelayCommand rc) rc.RaiseCanExecuteChanged();
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(searchText);

    public string PisoFiltro
    {
        get => pisoFiltro;
        set
        {
            if (!SetProperty(ref pisoFiltro, value)) return;
            OnPropertyChanged(nameof(CircuitosFiltrados));
        }
    }

    public IEnumerable<CuadroScRowVm> CuadrosFiltrados
    {
        get
        {
            var q = searchText?.Trim() ?? "";
            var items = Cuadros.Where(c => IsPerspectiveDeptos ? c.IsDepto : !c.IsDepto);
            if (string.IsNullOrEmpty(q))
                return items;
            return items.Where(c =>
                c.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Circuitos del tablero seleccionado, filtrados por piso y texto de búsqueda.
    /// </summary>
    public IEnumerable<CircuitoScRowVm> CircuitosFiltrados
    {
        get
        {
            IEnumerable<CircuitoScRowVm> source = Circuitos;
            if (pisoFiltro != TodosPisos && !string.IsNullOrEmpty(pisoFiltro))
                source = source.Where(c => CircuitoPisos(c).Contains(pisoFiltro, StringComparer.OrdinalIgnoreCase));

            var q = searchText?.Trim() ?? "";
            if (!string.IsNullOrEmpty(q))
            {
                source = source.Where(c =>
                    c.Numero.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (c.Tipo?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.Ubicacion?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.Estado?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || CircuitoPisos(c).Any(p => p.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            return source;
        }
    }

    // Pisos que cubre un circuito: los nombres de zona de sus tramos si está cubicado por varios
    // pisos; si no, el texto escrito a mano en Ubicación (un solo piso, o ninguno si está vacío).
    private static IReadOnlyList<string> CircuitoPisos(CircuitoScRowVm vm)
    {
        var row = vm.Source;
        if (row.TienePisos)
            return row.PisosTramos.Select(t => string.IsNullOrWhiteSpace(t.ZonaNombre) ? "Sin zona" : t.ZonaNombre.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return string.IsNullOrWhiteSpace(row.Ubicacion) ? Array.Empty<string>() : new[] { row.Ubicacion.Trim() };
    }

    private void RefreshPisosDisponibles()
    {
        var pisoActual = pisoFiltro;
        PisosDisponibles.Clear();
        PisosDisponibles.Add(TodosPisos);

        var pisos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in Circuitos)
            foreach (var p in CircuitoPisos(c))
                pisos.Add(p);

        // Zonas del proyecto primero, en su orden; luego cualquier piso escrito a mano que no
        // coincida con una zona, alfabético.
        var ordenZona = session.ZonasProyecto.Select((z, i) => (z.Nombre, i))
            .GroupBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        foreach (var p in pisos.OrderBy(p => ordenZona.TryGetValue(p, out int o) ? o : int.MaxValue)
                     .ThenBy(p => p, StringComparer.OrdinalIgnoreCase))
            PisosDisponibles.Add(p);

        pisoFiltro = PisosDisponibles.Contains(pisoActual) ? pisoActual : TodosPisos;
        OnPropertyChanged(nameof(PisoFiltro));
        OnPropertyChanged(nameof(CircuitosFiltrados));
    }

    // Zonas del proyecto (definidas en Cubicación), para el flyout de la esquina de la celda
    // "Ubicación / Pisos" — ver CubicacionScView.xaml.cs (PisosBtn_Click).
    public IReadOnlyList<ZonaProyecto> ZonasProyecto => session.ZonasProyecto;

    public bool CanEdit => !session.IsReadOnly;
    public bool IsGridReadOnly => session.IsReadOnly;
    public bool HasPendingChanges { get; private set; }
    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
    }

    public ICommand AddCuadroCommand { get; }
    public ICommand RemoveCuadroCommand { get; }
    public ICommand AddCircuitoCommand { get; }
    public ICommand RemoveCircuitoCommand { get; }
    public ICommand CopyCircuitoCommand { get; }
    public ICommand PasteCircuitoCommand { get; }
    public ICommand DuplicateCircuitoCommand { get; }
    public ICommand AddArtefactoCommand { get; }
    public ICommand RemoveArtefactoCommand { get; }
    public ICommand ApplyCommand { get; }
    public ICommand OpenProjectCommand { get; }
    public ICommand OpenCatalogoCommand { get; }
    public ICommand OpenArtefactosCommand { get; }
    public ICommand OpenResumenCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand BalancearFasesCommand { get; }
    public ICommand ExportarCuadroSecCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand SaveCommand { get; }

    public void Refresh()
    {
        IsLoading = true;
        try
        {
            RefreshCatalogCollections();
            ShowEmptyState = !workspace.HasOpenProject;
            if (ShowEmptyState)
            {
                Cuadros.Clear();
                Circuitos.Clear();
                SelectedCuadro = null;
                StatusMessage = "";
                HasPendingChanges = false;
                baselineCuadros = null;
                baselineConfig = null;
                return;
            }

            var previousCuadroId = selectedCuadro?.Id;
            var previousCircuitoId = selectedCircuito?.Id;

            Cuadros.Clear();
            foreach (var cuadro in session.CubicacionSC)
                Cuadros.Add(new CuadroScRowVm(cuadro, MarkDirty, () => CanEdit));

            if (Cuadros.Count == 0)
                EnsureDefaultCuadro();

            SelectedCuadro = CuadrosFiltrados.FirstOrDefault(c => string.Equals(c.Id, previousCuadroId, StringComparison.Ordinal))
                          ?? CuadrosFiltrados.FirstOrDefault();

            if (previousCircuitoId != null && SelectedCuadro != null)
            {
                var matchCircuito = Circuitos.FirstOrDefault(c => string.Equals(c.Id, previousCircuitoId, StringComparison.Ordinal));
                if (matchCircuito != null)
                {
                    SelectedCircuito = matchCircuito;
                }
            }
            UpdateStatusMessage();
            HasPendingChanges = false;
            CaptureBaseline();
            NotifyKpiProperties();
            OnPropertyChanged(nameof(CuadrosFiltrados));
            OnPropertyChanged(nameof(CuadrosFiltradosCount));
            OnPropertyChanged(nameof(CircuitosFiltrados));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public bool TryDiscardChanges()
    {
        if (!HasPendingChanges)
            return true;

        RestoreBaseline();
        Refresh();
        StatusMessage = "Cambios descartados";
        return true;
    }

    public bool TryApplyChanges()
    {
        // Confirma la celda activa del DataGrid (si existe) antes de persistir el estado.
        // Esto garantiza que el valor escrito en un TextBox de edición no se pierda cuando
        // el usuario navega a otra pestaña del shell mientras la celda sigue en edición.
        RequestCommitGridEdit?.Invoke();
        ApplyChanges();
        return !HasPendingChanges;
    }

    bool IPageWithPendingChanges.HasPendingChanges => HasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() => TryDiscardChanges();
    bool IPageWithCommitChanges.TryApplyChanges() => TryApplyChanges();

    private void EnsureDefaultCuadro()
    {
        var cuadro = new CuadroCargaSCRow { Nombre = "TDA SC", Descripcion = "Tablero servicios comunes" };
        session.CubicacionSC.Add(cuadro);
        Cuadros.Add(new CuadroScRowVm(cuadro, MarkDirty, () => CanEdit));
        MarkDirty();
    }

    private void LoadCircuitos()
    {
        RequestClearGridSelection?.Invoke();
        Circuitos.Clear();
        SelectedCircuito = null;
        if (selectedCuadro == null)
        {
            RefreshPisosDisponibles();
            return;
        }

        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        if (cuadro == null)
        {
            RefreshPisosDisponibles();
            return;
        }

        foreach (var circuito in cuadro.Circuitos.OrderBy(c => c.Numero))
            Circuitos.Add(new CircuitoScRowVm(
                circuito,
                Config,
                session.ZonasProyecto,
                selectedCuadro.NotifyCircuitosActivosChanged,
                OnCircuitoPisosChanged,
                () => CanEdit,
                MarkDirty));

        RefreshPisosDisponibles();
    }

    private void AddCuadro()
    {
        var undoName = IsPerspectiveDeptos ? "Agregar tablero Deptos" : "Agregar tablero SC";
        undo.PushUndoPoint(session, undoName);
        pendingUndoForEdit = true;
        var prefix = IsPerspectiveDeptos ? "TDA DEPTO" : "TDA SC";
        var countInPerspective = Cuadros.Count(c => IsPerspectiveDeptos ? c.IsDepto : !c.IsDepto);
        var cuadro = new CuadroCargaSCRow
        {
            Nombre = $"{prefix} {countInPerspective + 1}",
            Categoria = IsPerspectiveDeptos ? "Deptos" : "Generales"
        };
        session.CubicacionSC.Add(cuadro);
        var vm = new CuadroScRowVm(cuadro, MarkDirty, () => CanEdit);
        Cuadros.Add(vm);
        OnPropertyChanged(nameof(CuadrosFiltrados));
        OnPropertyChanged(nameof(CuadrosFiltradosCount));
        SelectedCuadro = vm;
        MarkDirty();
        UpdateStatusMessage();
        ((RelayCommand<object>)RemoveCuadroCommand).RaiseCanExecuteChanged();
    }

    private void RemoveCuadro(object? parameter = null)
    {
        var target = parameter as CuadroScRowVm ?? selectedCuadro;
        if (target == null || !CanEdit)
            return;

        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == target.Id);
        int circuitCount = cuadro?.Circuitos?.Count ?? 0;
        string msg = circuitCount > 0
            ? $"¿Está seguro de que desea eliminar el tablero '{target.Nombre}' con sus {circuitCount} circuitos asociados?\nSe eliminarán todas sus canalizaciones y artefactos."
            : $"¿Está seguro de que desea eliminar el tablero '{target.Nombre}'?";

        if (!ConfirmDelete(msg))
            return;

        var undoName = IsPerspectiveDeptos ? "Eliminar tablero Deptos" : "Eliminar tablero SC";
        undo.PushUndoPoint(session, undoName);
        pendingUndoForEdit = true;
        if (cuadro != null)
            session.CubicacionSC.Remove(cuadro);

        Cuadros.Remove(target);
        OnPropertyChanged(nameof(CuadrosFiltrados));
        OnPropertyChanged(nameof(CuadrosFiltradosCount));

        if (selectedCuadro == target || selectedCuadro == null)
        {
            SelectedCuadro = CuadrosFiltrados.FirstOrDefault();
            if (SelectedCuadro == null)
            {
                RequestClearGridSelection?.Invoke();
                Circuitos.Clear();
                SelectedCircuito = null;
                NotifyKpiProperties();
            }
        }
        else
        {
            NotifyKpiProperties();
        }

        MarkDirty();
        UpdateStatusMessage();
        ((RelayCommand<object>)RemoveCuadroCommand).RaiseCanExecuteChanged();
    }

    private void AddCircuito()
    {
        if (selectedCuadro == null)
            return;

        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        if (cuadro == null)
            return;

        undo.PushUndoPoint(session, "Agregar circuito SC");
        pendingUndoForEdit = true;
        // Hereda canalización del circuito activo (conductor/sección/ducto/fase) para cargar
        // muchos circuitos iguales sin retippear — solo cambia REC/ubicación después.
        var circuito = new CircuitoSCRow
        {
            Numero = CubicacionScClipboardCoordinator.NextCircuitoNumero(cuadro),
            Descripcion = "Circuito nuevo"
        };
        if (selectedCircuito != null)
            CubicacionScClipboardCoordinator.ApplyCanalizacionTemplate(selectedCircuito.Source, circuito);

        if (cuadro.EsDepto)
        {
            circuito.MetrosBandeja = 0;
            circuito.TipoRecorrido = "Tubería";
            circuito.TipoBandeja = "";
            circuito.AnchoBandeja = 0;
            circuito.ConductorDistribucion = "";
            circuito.SeccionDistribucion = "";
            // A01 (caja 100×65) no queda fija en 1. En departamentos el estimador
            // escribe la cantidad en la grilla. Duplicar sí copia el valor ya cargado.
            // En tableros generales se mantiene el 1 automático.
            circuito.Cajas100x65 = 0;
        }

        InsertCircuitoVm(cuadro, circuito);
    }

    /// <summary>Marca dirty tras pegar/rellenar celdas desde la grilla (Ctrl+V / Ctrl+D fill).</summary>
    public void NotifyGridCellsEdited()
    {
        if (!pendingUndoForEdit)
        {
            undo.PushUndoPoint(session, "Editar celdas SC");
            pendingUndoForEdit = true;
        }
        MarkDirty();
        pendingUndoForEdit = false;
        StatusMessage = "Celdas actualizadas · Ctrl+D duplica · Ctrl+C en N° copia circuito · Ctrl+V pega sobre el actual";
    }

    public void ClearCircuitoClipboard()
    {
        if (!clipboardCoordinator.HasClipboard) return;
        clipboardCoordinator.ClearClipboard();
        ((RelayCommand)PasteCircuitoCommand).RaiseCanExecuteChanged();
    }

    private void CopyCircuito()
    {
        if (selectedCircuito == null) return;
        var msg = clipboardCoordinator.Copy(selectedCircuito);
        ((RelayCommand)PasteCircuitoCommand).RaiseCanExecuteChanged();
        if (msg != null) StatusMessage = msg;
    }

    private void PasteCircuito()
    {
        if (!clipboardCoordinator.HasClipboard || selectedCircuito == null) return;
        undo.PushUndoPoint(session, "Pegar circuito SC");
        pendingUndoForEdit = true;
        var msg = clipboardCoordinator.Paste(selectedCircuito, selectedCuadro);
        MarkDirty();
        pendingUndoForEdit = false;
        if (msg != null) StatusMessage = msg;
    }

    private void DuplicateCircuito()
    {
        if (selectedCircuito == null || selectedCuadro == null) return;
        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        if (cuadro == null) return;

        undo.PushUndoPoint(session, "Duplicar circuito SC");
        pendingUndoForEdit = true;
        var result = clipboardCoordinator.Duplicate(selectedCircuito, cuadro);
        if (result.HasValue)
        {
            InsertCircuitoVm(cuadro, result.Value.Clone);
            StatusMessage = result.Value.StatusMessage;
        }
    }

    private void InsertCircuitoVm(CuadroCargaSCRow cuadro, CircuitoSCRow circuito)
    {
        if (selectedCuadro == null) return;
        cuadro.Circuitos.Add(circuito);
        var vm = new CircuitoScRowVm(
            circuito,
            Config,
            session.ZonasProyecto,
            selectedCuadro.NotifyCircuitosActivosChanged,
            OnCircuitoPisosChanged,
            () => CanEdit,
            MarkDirty);
        Circuitos.Add(vm);
        SelectedCircuito = vm;
        selectedCuadro.NotifyCircuitosActivosChanged();
        MarkDirty();
        CircuitoAdded?.Invoke(circuito.Id);
    }

    private void AddArtefacto()
    {
        if (selectedCircuito == null || string.IsNullOrWhiteSpace(NuevoArtefactoNombre))
            return;

        undo.PushUndoPoint(session, "Agregar artefacto SC");
        pendingUndoForEdit = true;
        string nombre = NuevoArtefactoNombre.Trim();
        string categoria = ResolveCategoriaYRegistrar(nombre);
        selectedCircuito.AddArtefacto(nombre, categoria, NuevoArtefactoCantidad);

        NuevoArtefactoNombre = "";
        NuevoArtefactoCantidad = 1;
        MarkDirty();
        pendingUndoForEdit = false;
    }

    private void RemoveArtefacto(ArtefactoScRowVm? artefacto)
    {
        if (selectedCircuito == null || artefacto == null)
            return;

        undo.PushUndoPoint(session, "Eliminar artefacto SC");
        pendingUndoForEdit = true;
        selectedCircuito.RemoveArtefacto(artefacto);
        MarkDirty();
        pendingUndoForEdit = false;
    }

    private void RemoveCircuito()
    {
        if (selectedCuadro == null || selectedCircuito == null || !CanEdit)
            return;

        var idLabel = selectedCircuito.Numero > 0
            ? (string.IsNullOrWhiteSpace(selectedCircuito.Descripcion) ? $"N° {selectedCircuito.Numero}" : $"N° {selectedCircuito.Numero} ('{selectedCircuito.Descripcion}')")
            : (string.IsNullOrWhiteSpace(selectedCircuito.Descripcion) ? "seleccionado" : $"'{selectedCircuito.Descripcion}'");

        var msg = $"¿Está seguro de que desea eliminar el circuito {idLabel}?";
        if (selectedCircuito.TieneArtefactos || selectedCircuito.LargoCircuito > 0)
            msg += "\nSe eliminarán sus mediciones de canalización, conductores y artefactos asociados.";

        if (!ConfirmDeleteCircuito(msg))
            return;

        undo.PushUndoPoint(session, "Eliminar circuito SC");
        pendingUndoForEdit = true;
        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        var circuito = cuadro?.Circuitos.FirstOrDefault(c => c.Id == selectedCircuito.Id);
        if (circuito != null)
            cuadro!.Circuitos.Remove(circuito);

        var aRemover = selectedCircuito;
        CircuitoRemoving?.Invoke(aRemover);

        int index = Circuitos.IndexOf(aRemover);
        Circuitos.Remove(aRemover);

        CircuitoScRowVm? next = null;
        if (Circuitos.Count > 0)
        {
            int nextIndex = Math.Clamp(index, 0, Circuitos.Count - 1);
            next = Circuitos[nextIndex];
        }
        SelectedCircuito = next;
        selectedCuadro.NotifyCircuitosActivosChanged();
        MarkDirty();
        pendingUndoForEdit = false;
    }

    // Botón "Catálogo" en la barra de la planilla: abre un diálogo modal para editar
    // conductores/tuberías/bandejas/cajas/fases/artefactos del proyecto (CubicacionScConfig),
    // en vez de una pestaña nueva — pedido explícito del usuario.
    private void AbrirCatalogo()
    {
        var owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
        var dialog = new CubicacionScCatalogoDialog(Config, session.ProjectInfo.Nombre) { Owner = owner };
        if (dialog.ShowDialog() != true || dialog.Result == null)
            return;

        session.CubicacionScConfig = dialog.Result;
        RefreshCatalogCollections();
        // Recarga los circuitos para que su constructor re-sincronice N° conductores / merma con el
        // catálogo nuevo (los VM anteriores capturaban el config viejo).
        LoadCircuitos();
        MarkDirty();
        StatusMessage = "Catálogo actualizado. Use Guardar en la barra superior para persistir el archivo.";
    }

    // Alt+A / botón Artefactos: diálogo del circuito. Si la ubicación tiene varios pisos,
    // solo muestra esos pisos. Elegir un artefacto pregunta la cantidad antes de guardarla.
    private void AbrirArtefactos(CircuitoScRowVm? vm)
    {
        if (vm == null || !CanEdit)
            return;

        var owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
        var dialog = new CubicacionScArtefactosDialog(vm.Source, session.ZonasProyecto, Config) { Owner = owner };
        if (dialog.ShowDialog() != true)
            return;

        // Registra en el catálogo del proyecto los artefactos nuevos creados al vuelo.
        foreach (var nombre in dialog.NombresNuevos)
            ResolveCategoriaYRegistrar(nombre);

        vm.ReloadArtefactos();
        SincronizarGrillaConSesion();
        MarkDirty();
        StatusMessage = "Artefactos por zona actualizados. Use Guardar en la barra superior para persistir el archivo.";
    }

    /// <summary>
    /// Copia artefactos y cantidades de la grilla al circuito de la sesión cuando el
    /// autoguardado ya reemplazó el objeto. El resumen y el .qce leen la sesión:
    /// sin esto, 5/8, A11 y A01 quedan en la grilla y el total de cajas sale en 0.
    /// </summary>
    internal bool SincronizarGrillaConSesion() =>
        CubicacionScSessionSync.SincronizarGrillaConSesion(Circuitos, session);

    // Invocado por cada CircuitoScRowVm (callback onPisosChanged) al confirmar el flyout de zonas
    // o al parsear un rango en Ubicación: solo refresca el filtro de pisos. El dirty lo dispara
    // onChanged → MarkDirty (inyectado en el ctor del CircuitoScRowVm).
    private void OnCircuitoPisosChanged()
    {
        RefreshPisosDisponibles();
    }

    private void UpdateStatusMessage()
    {
        if (showEmptyState) { StatusMessage = ""; return; }
        var inPersp = Cuadros.Where(c => IsPerspectiveDeptos ? c.IsDepto : !c.IsDepto).ToList();
        var circsCount = inPersp.Sum(c => c.Source.Circuitos.Count);
        var label = IsPerspectiveDeptos ? "Deptos" : "Generales";
        StatusMessage = $"{inPersp.Count} tablero(s) {label} · {circsCount} circuitos";
    }

    // Botón "Resumen" de la barra: abre el resumen del proyecto con sub-pestañas (total, por
    // tablero, por piso y artefactos por piso). Solo lectura.
    private void AbrirResumen()
    {
        if (showEmptyState) return;
        if (SincronizarGrillaConSesion())
            MarkDirty();
        var owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
        new CubicacionScResumenDialog(session.CubicacionSC, session.ZonasProyecto, Config, IsPerspectiveDeptos) { Owner = owner }.ShowDialog();
    }

    private void ApplyChanges()
    {
        if (!CanEdit)
            return;

        // Marca sesión por si el usuario pulsó Aplicar sin editar tras un baseline limpio.
        if (!session.IsDirty)
        {
            suppressSessionReload = true;
            try { session.MarkDirty(ProjectDirtyScope.CubicacionSc); }
            finally { suppressSessionReload = false; }
        }

        HasPendingChanges = false;
        OnPropertyChanged(nameof(HasPendingChanges));
        CaptureBaseline();
        StatusMessage = "Cambios aplicados. Use Guardar en la barra superior para persistir el archivo.";
        shell.RefreshChrome();
        ((RelayCommand)ApplyCommand).RaiseCanExecuteChanged();
    }

    private void MarkDirty()
    {
        if (session.IsReadOnly)
            return;

        if (!pendingUndoForEdit && !IsLoading)
        {
            undo.PushUndoPoint(session, "Editar cubicación SC");
            pendingUndoForEdit = true;
        }

        HasPendingChanges = true;
        OnPropertyChanged(nameof(HasPendingChanges));
        NotifyKpiProperties();
        ((RelayCommand)ApplyCommand).RaiseCanExecuteChanged();

        // Chrome "Sin guardar" + Leave/Save persisten el .qce (session.IsDirty).
        suppressSessionReload = true;
        try { session.MarkDirty(ProjectDirtyScope.CubicacionSc); }
        finally
        {
            suppressSessionReload = false;
            pendingUndoForEdit = false;
        }
    }

    private void CaptureBaseline()
    {
        baselineCuadros = CloneJson(session.CubicacionSC.ToList());
        baselineConfig = CloneJson(Config);
    }

    private void RestoreBaseline()
    {
        if (baselineCuadros == null || baselineConfig == null)
            return;

        var cuadros = CloneJson(baselineCuadros);
        var config = CloneJson(baselineConfig);
        session.CubicacionSC.Clear();
        foreach (var cuadro in cuadros)
            session.CubicacionSC.Add(cuadro);
        session.CubicacionScConfig = config;
    }

    private static T CloneJson<T>(T source)
    {
        var json = JsonSerializer.Serialize(source, BaselineJsonOptions);
        return JsonSerializer.Deserialize<T>(json, BaselineJsonOptions)
               ?? throw new InvalidOperationException($"No se pudo clonar {typeof(T).Name} para baseline SC.");
    }

    public string KpiCanalizacionText
    {
        get
        {
            if (showEmptyState) return "0 m";
            var activeCircs = session.CubicacionSC.SelectMany(cuadro => cuadro.Circuitos).Where(c => c.EsActivo).ToList();
            decimal total = activeCircs.Sum(c => c.MetrosBandejaEfectivo + c.MetrosTuberiaEfectivo);
            return $"{total:N1} m";
        }
    }

    public int KpiTablerosCount
    {
        get
        {
            if (showEmptyState) return 0;
            return session.CubicacionSC.Count;
        }
    }

    public int KpiCircuitosCount
    {
        get
        {
            if (showEmptyState) return 0;
            return session.CubicacionSC.Sum(t => t.Circuitos.Count(c => c.EsActivo || c.EsReservaEquipada));
        }
    }

    public string KpiCajasTerminalesText
    {
        get
        {
            if (showEmptyState) return "0 un / 0.0 m";
            var activeCircs = session.CubicacionSC.SelectMany(cuadro => cuadro.Circuitos).Where(c => c.EsActivo).ToList();
            int totalCajas = activeCircs.Sum(c => c.TotalCajas);
            decimal totalTerminales = 0;
            foreach (var c in activeCircs)
            {
                var flex = Config.Flexibles.FirstOrDefault(f => string.Equals(f.Diametro, c.SeccionTuberia, StringComparison.OrdinalIgnoreCase));
                if (flex != null && c.TotalCajas > 0 && flex.MetrosPorCaja > 0m)
                {
                    totalTerminales += c.TotalCajas * flex.MetrosPorCaja;
                }
            }
            return $"{totalCajas} un / {totalTerminales:N1} m";
        }
    }

    public string KpiPotenciaText
    {
        get
        {
            if (showEmptyState) return "0.0 kW";
            var activeCircs = session.CubicacionSC.SelectMany(cuadro => cuadro.Circuitos).Where(c => c.EsActivo || c.EsReservaEquipada).ToList();
            if (activeCircs.Count == 0) return "0.0 kW";
            decimal totalW = activeCircs.Sum(CuadroCargasSecEngine.EstimateCircuitPowerW);
            decimal kw = Math.Round(totalW / 1000m, 1);
            return $"{kw:N1} kW";
        }
    }

    public string KpiDesbalanceText
    {
        get
        {
            if (showEmptyState || selectedCuadro == null) return "0.0% SEC";
            var summary = CuadroCargasSecEngine.CalculateTableroSummary(selectedCuadro.Source);
            return $"{summary.PorcentajeDesbalance:N1}% {(summary.ConformeSec ? "✓ SEC" : "⚠ Desbalance")}";
        }
    }

    public bool IsDesbalanceConformeSec
    {
        get
        {
            if (showEmptyState || selectedCuadro == null) return true;
            var summary = CuadroCargasSecEngine.CalculateTableroSummary(selectedCuadro.Source);
            return summary.ConformeSec;
        }
    }

    private void NotifyKpiProperties()
    {
        OnPropertyChanged(nameof(KpiCanalizacionText));
        OnPropertyChanged(nameof(KpiTablerosCount));
        OnPropertyChanged(nameof(KpiCircuitosCount));
        OnPropertyChanged(nameof(KpiCajasTerminalesText));
        OnPropertyChanged(nameof(KpiPotenciaText));
        OnPropertyChanged(nameof(KpiDesbalanceText));
        OnPropertyChanged(nameof(IsDesbalanceConformeSec));
    }

    private void BalancearFases()
    {
        if (selectedCuadro == null || !CanEdit) return;

        undo.PushUndoPoint(session, "Auto-balancear fases SEC");
        CuadroCargasSecEngine.AutoBalancearFases(selectedCuadro.Source);
        LoadCircuitos();
        NotifyKpiProperties();
        MarkDirty();

        var summary = CuadroCargasSecEngine.CalculateTableroSummary(selectedCuadro.Source);
        string status = summary.ConformeSec
            ? $"Fases auto-balanceadas: Desbalance {summary.PorcentajeDesbalance:N1}% (Conforme SEC RIC N°02 ≤ 10%)."
            : $"Fases auto-balanceadas: Desbalance {summary.PorcentajeDesbalance:N1}%.";

        StatusMessage = status;
        ShellNotificationService.Instance.ShowSuccess("Balanceo de Fases SEC", status);
    }

    private void ExportarCuadroSec() => exportCoordinator.ExportarCuadroSec(session);

    private void ExportPdf() => exportCoordinator.ExportPdf(session, IsPerspectiveDeptos, Config);
}
