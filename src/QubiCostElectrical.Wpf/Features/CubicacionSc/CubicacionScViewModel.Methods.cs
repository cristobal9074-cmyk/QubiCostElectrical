using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed partial class CubicacionScViewModel : SessionAwareViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    public CubicacionScViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        session = host.Session;

        AddCuadroCommand = new RelayCommand(AddCuadro, () => !showEmptyState && CanEdit);
        RemoveCuadroCommand = new RelayCommand(RemoveCuadro, () => selectedCuadro != null && CanEdit);
        AddCircuitoCommand = new RelayCommand(AddCircuito, () => selectedCuadro != null && CanEdit);
        RemoveCircuitoCommand = new RelayCommand(RemoveCircuito, () => selectedCircuito != null && CanEdit);
        CopyCircuitoCommand = new RelayCommand(CopyCircuito, () => selectedCircuito != null && CanEdit);
        PasteCircuitoCommand = new RelayCommand(PasteCircuito, () => clipboardCircuito != null && selectedCircuito != null && CanEdit);
        DuplicateCircuitoCommand = new RelayCommand(DuplicateCircuito, () => selectedCircuito != null && selectedCuadro != null && CanEdit);
        AddArtefactoCommand = new RelayCommand(AddArtefacto, () => selectedCircuito != null && CanEdit && !string.IsNullOrWhiteSpace(NuevoArtefactoNombre));
        RemoveArtefactoCommand = new RelayCommand<ArtefactoScRowVm>(RemoveArtefacto, a => a != null && CanEdit);
        ApplyCommand = new RelayCommand(ApplyChanges, () => HasPendingChanges && CanEdit);
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        OpenCatalogoCommand = new RelayCommand(AbrirCatalogo, () => !showEmptyState && CanEdit);
        OpenArtefactosCommand = new RelayCommand<CircuitoScRowVm>(AbrirArtefactos, c => c != null && CanEdit);
        OpenResumenCommand = new RelayCommand(AbrirResumen, () => !showEmptyState);
        ExportPdfCommand = new RelayCommand(ExportPdf, () => !showEmptyState);

        Refresh();
    }

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);

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
        // Abreviaturas solicitadas: Alumbrado (A), Enchufe (E), Clima (CL), Computación (C), Fuerza (F)
        ReplaceContents(tipos, new List<string> { "A", "E", "CL", "C", "F" });
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

    public CuadroScRowVm? SelectedCuadro
    {
        get => selectedCuadro;
        set
        {
            if (!SetProperty(ref selectedCuadro, value))
                return;
            LoadCircuitos();
            OnPropertyChanged(nameof(HasPendingChanges));
        }
    }

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

    // ── Filtro de Pisos ──────────────────────────────────────────────────────
    private const string TodosPisos = "Todos los pisos";
    private string pisoFiltro = TodosPisos;

    public ObservableCollection<string> PisosDisponibles { get; } = new();

    public string PisoFiltro
    {
        get => pisoFiltro;
        set
        {
            if (!SetProperty(ref pisoFiltro, value)) return;
            OnPropertyChanged(nameof(CircuitosFiltrados));
        }
    }

    /// <summary>
    /// Circuitos del tablero seleccionado, filtrados por piso cuando el usuario elige uno. Un
    /// piso puede ser una zona del proyecto (circuito cubicado por varios pisos) o el texto
    /// escrito a mano en Ubicación (circuito de un solo piso).
    /// </summary>
    public IEnumerable<CircuitoScRowVm> CircuitosFiltrados
    {
        get
        {
            if (pisoFiltro == TodosPisos || string.IsNullOrEmpty(pisoFiltro))
                return Circuitos;

            return Circuitos.Where(c => CircuitoPisos(c).Contains(pisoFiltro, StringComparer.OrdinalIgnoreCase));
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

    public void Refresh()
    {
        RefreshCatalogCollections();
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
        if (ShowEmptyState)
        {
            Cuadros.Clear();
            Circuitos.Clear();
            SelectedCuadro = null;
            StatusMessage = "";
            HasPendingChanges = false;
            return;
        }

        Cuadros.Clear();
        foreach (var cuadro in session.CubicacionSC)
            Cuadros.Add(new CuadroScRowVm(cuadro, MarkDirty));

        if (Cuadros.Count == 0)
            EnsureDefaultCuadro();

        SelectedCuadro = Cuadros.FirstOrDefault();
        StatusMessage = $"{Cuadros.Count} tablero(s) SC · {session.CubicacionSC.Sum(c => c.Circuitos.Count)} circuitos";
        HasPendingChanges = false;
        NotifyKpiProperties();
    }

    public bool TryDiscardChanges()
    {
        Refresh();
        return true;
    }

    public bool TryApplyChanges()
    {
        ApplyChanges();
        return !HasPendingChanges;
    }

    private void EnsureDefaultCuadro()
    {
        var cuadro = new CuadroCargaSCRow { Nombre = "TDA SC", Descripcion = "Tablero servicios comunes" };
        session.CubicacionSC.Add(cuadro);
        Cuadros.Add(new CuadroScRowVm(cuadro, MarkDirty));
        MarkDirty();
    }

    private void LoadCircuitos()
    {
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
            Circuitos.Add(new CircuitoScRowVm(circuito, Config, session.ZonasProyecto, selectedCuadro.NotifyCircuitosActivosChanged, OnCircuitoPisosChanged));

        RefreshPisosDisponibles();
    }

    private void AddCuadro()
    {
        var cuadro = new CuadroCargaSCRow { Nombre = $"TDA SC {Cuadros.Count + 1}" };
        session.CubicacionSC.Add(cuadro);
        var vm = new CuadroScRowVm(cuadro, MarkDirty);
        Cuadros.Add(vm);
        SelectedCuadro = vm;
        MarkDirty();
    }

    private void RemoveCuadro()
    {
        if (selectedCuadro == null)
            return;

        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        if (cuadro != null)
            session.CubicacionSC.Remove(cuadro);

        Cuadros.Remove(selectedCuadro);
        SelectedCuadro = Cuadros.FirstOrDefault();
        MarkDirty();
    }

    private void AddCircuito()
    {
        if (selectedCuadro == null)
            return;

        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        if (cuadro == null)
            return;

        // Hereda canalización del circuito activo (conductor/sección/ducto/fase) para cargar
        // muchos circuitos iguales sin retippear — solo cambia REC/ubicación después.
        var circuito = new CircuitoSCRow
        {
            Numero = NextCircuitoNumero(cuadro),
            Descripcion = "Circuito nuevo"
        };
        if (selectedCircuito != null)
            ApplyCanalizacionTemplate(selectedCircuito.Source, circuito);

        InsertCircuitoVm(cuadro, circuito);
    }

    private static void ApplyCanalizacionTemplate(CircuitoSCRow from, CircuitoSCRow to)
    {
        to.Tipo = from.Tipo;
        to.Sistema = from.Sistema;
        to.NumConductores = from.NumConductores;
        to.MermaPct = from.MermaPct;
        to.TipoRecorrido = from.TipoRecorrido;
        to.TipoTuberia = from.TipoTuberia;
        to.SeccionTuberia = from.SeccionTuberia;
        to.TipoBandeja = from.TipoBandeja;
        to.AnchoBandeja = from.AnchoBandeja;
        to.ConductorDistribucion = from.ConductorDistribucion;
        to.SeccionDistribucion = from.SeccionDistribucion;
        to.ConductorDerivacion = from.ConductorDerivacion;
        to.SeccionDerivacion = from.SeccionDerivacion;
    }

    /// <summary>Marca dirty tras pegar/rellenar celdas desde la grilla (Ctrl+V / Ctrl+D fill).</summary>
    public void NotifyGridCellsEdited()
    {
        MarkDirty();
        StatusMessage = "Celdas actualizadas · Ctrl+D duplica · Ctrl+C en N° copia circuito · Ctrl+V pega sobre el actual";
    }

    public void ClearCircuitoClipboard()
    {
        if (clipboardCircuito == null) return;
        clipboardCircuito = null;
        ((RelayCommand)PasteCircuitoCommand).RaiseCanExecuteChanged();
    }

    private void CopyCircuito()
    {
        if (selectedCircuito == null) return;
        clipboardCircuito = selectedCircuito.Source.Clone(selectedCircuito.Numero);
        ((RelayCommand)PasteCircuitoCommand).RaiseCanExecuteChanged();
        StatusMessage = $"Circuito {selectedCircuito.Numero} copiado · ve a otro circuito y Ctrl+V lo reemplaza (no agrega fila) · Ctrl+D duplica";
    }

    private void PasteCircuito()
    {
        if (clipboardCircuito == null || selectedCircuito == null) return;
        int destino = selectedCircuito.Numero;
        selectedCircuito.ApplyClipboardFrom(clipboardCircuito);
        selectedCuadro?.NotifyCircuitosActivosChanged();
        MarkDirty();
        StatusMessage = $"Circuito {destino} actualizado con la copia (mismo N°, sin fila nueva)";
    }

    private void DuplicateCircuito()
    {
        if (selectedCircuito == null || selectedCuadro == null) return;
        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        if (cuadro == null) return;

        int numero = NextCircuitoNumero(cuadro);
        var clone = selectedCircuito.Source.Clone(numero);
        InsertCircuitoVm(cuadro, clone);
        StatusMessage = $"Circuito {numero} duplicado · cambia REC/ubicación y sigue con Ctrl+D";
    }

    private static int NextCircuitoNumero(CuadroCargaSCRow cuadro) =>
        cuadro.Circuitos.Count == 0 ? 1 : cuadro.Circuitos.Max(c => c.Numero) + 1;

    private void InsertCircuitoVm(CuadroCargaSCRow cuadro, CircuitoSCRow circuito)
    {
        if (selectedCuadro == null) return;
        cuadro.Circuitos.Add(circuito);
        var vm = new CircuitoScRowVm(circuito, Config, session.ZonasProyecto, selectedCuadro.NotifyCircuitosActivosChanged, OnCircuitoPisosChanged);
        Circuitos.Add(vm);
        SelectedCircuito = vm;
        selectedCuadro.NotifyCircuitosActivosChanged();
        MarkDirty();
    }

    private void AddArtefacto()
    {
        if (selectedCircuito == null || string.IsNullOrWhiteSpace(NuevoArtefactoNombre))
            return;

        string nombre = NuevoArtefactoNombre.Trim();
        string categoria = ResolveCategoriaYRegistrar(nombre);
        selectedCircuito.AddArtefacto(nombre, categoria, NuevoArtefactoCantidad);

        NuevoArtefactoNombre = "";
        NuevoArtefactoCantidad = 1;
        MarkDirty();
    }

    private void RemoveArtefacto(ArtefactoScRowVm? artefacto)
    {
        if (selectedCircuito == null || artefacto == null)
            return;

        selectedCircuito.RemoveArtefacto(artefacto);
        MarkDirty();
    }

    private void RemoveCircuito()
    {
        if (selectedCuadro == null || selectedCircuito == null)
            return;

        var cuadro = session.CubicacionSC.FirstOrDefault(c => c.Id == selectedCuadro.Id);
        var circuito = cuadro?.Circuitos.FirstOrDefault(c => c.Id == selectedCircuito.Id);
        if (circuito != null)
            cuadro!.Circuitos.Remove(circuito);

        Circuitos.Remove(selectedCircuito);
        SelectedCircuito = Circuitos.FirstOrDefault();
        selectedCuadro.NotifyCircuitosActivosChanged();
        MarkDirty();
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
        session.MarkDirty("cubicacion_sc");
        RefreshCatalogCollections();
        // Recarga los circuitos para que su constructor re-sincronice N° conductores / merma con el
        // catálogo nuevo (los VM anteriores capturaban el config viejo).
        LoadCircuitos();
        MarkDirty();
        StatusMessage = "Catálogo actualizado. Use Guardar en la barra superior para persistir el archivo.";
    }

    // Botón "Artefactos" en cada fila de circuito: abre el diálogo de agregado rápido por zona.
    // Las zonas salen de las zonas del proyecto (ZonaProyecto, definidas en Cubicación); el
    // catálogo de artefactos y los "frecuentes" salen de CubicacionScConfig. El diálogo muta
    // los artefactos del circuito directamente; al aceptar se recarga la fila y se marca sucio.
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
        MarkDirty();
        StatusMessage = "Artefactos por zona actualizados. Use Guardar en la barra superior para persistir el archivo.";
    }

    // Invocado por cada CircuitoScRowVm (callback onPisosChanged) al confirmar el flyout de zonas
    // de la esquina de la celda "Ubicación / Pisos": refresca los pisos disponibles del filtro y
    // marca el proyecto como modificado.
    private void OnCircuitoPisosChanged()
    {
        RefreshPisosDisponibles();
        MarkDirty();
    }

    // Botón "Resumen" de la barra: abre el resumen del proyecto con sub-pestañas (total, por
    // tablero, por piso y artefactos por piso). Solo lectura.
    private void AbrirResumen()
    {
        if (showEmptyState) return;
        var owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>()
            .FirstOrDefault(w => w.IsActive);
        new CubicacionScResumenDialog(session.CubicacionSC, session.ZonasProyecto, Config) { Owner = owner }.ShowDialog();
    }

    private void ApplyChanges()
    {
        if (!CanEdit)
            return;

        session.MarkDirty("cubicacion_sc");
        HasPendingChanges = false;
        StatusMessage = "Cambios aplicados. Use Guardar en la barra superior para persistir el archivo.";
        shell.RefreshChrome();
    }

    private void MarkDirty()
    {
        HasPendingChanges = true;
        OnPropertyChanged(nameof(HasPendingChanges));
        NotifyKpiProperties();
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

    private void NotifyKpiProperties()
    {
        OnPropertyChanged(nameof(KpiCanalizacionText));
        OnPropertyChanged(nameof(KpiTablerosCount));
        OnPropertyChanged(nameof(KpiCircuitosCount));
        OnPropertyChanged(nameof(KpiCajasTerminalesText));
    }

    private void ExportPdf()
    {
        try
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Archivo PDF (*.pdf)|*.pdf",
                FileName = $"Cubicacion_SC_{host.Session.ProjectInfo.Nombre ?? "Proyecto"}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                CubicacionScPdfExporter.Export(host.Session.ProjectInfo, session.CubicacionSC, session.ZonasProyecto, Config, sfd.FileName);
                WpfDialogService.Info("Exportación Exitosa", $"Se ha exportado el registro de cubicación de Servicios Comunes a {sfd.FileName}");
            }
        }
        catch (Exception ex)
        {
            WpfDialogService.Error("Error al exportar", $"Ocurrió un error al generar el PDF: {ex.Message}");
        }
    }
}
