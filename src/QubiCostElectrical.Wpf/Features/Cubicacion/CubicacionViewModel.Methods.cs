using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
    private bool isLeftPanelCollapsed;
    private bool isRightPanelCollapsed;
    private bool isLoading;
    private bool hasPendingChanges;
    private string saveStatusText = "Sin cambios";
    private bool hasPendingUndoForEdit;
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

    public CubicacionViewModel(IShellNavigationHost shell, WpfAppHost host)
    {
        this.shell = shell;
        this.host = host;
        cubicacionPresenter = host.Cubicacion;
        session = host.Session;
        cubSvc = host.Services.Cubicacion;
        querySvc = host.Services.CubicacionTakeoffQuery;
        repo = host.Services.ProjectRepository;
        materialSearch = host.Services.MaterialSearch;
        priceBankStore = host.Services.PriceBankStore;
        conjuntoService = new ConjuntoElectricoService();
        totalsCalculator = new CubicacionTotalsCalculator(this, cubicacionPresenter);
        autocompleteService = new CubicacionAutocompleteService(this, session, materialSearch, priceBankStore);
        sessionCoordinator = new CubicacionSessionCoordinator(this, session);
        persistenceService = new CubicacionPersistenceService(this, session, host, shell, cubicacionPresenter, conjuntoService);
        loadingService = new CubicacionLoadingService(this, session, querySvc, cubSvc);
        sectorService = new CubicacionSectorService(this, session, host, querySvc, cubicacionPresenter);
        rowMutationService = new CubicacionRowMutationService(this, session, host, cubSvc, cubicacionPresenter, priceBankStore);
        clipboardCoordinator = new CubicacionClipboardCoordinator(this, session, host, cubicacionPresenter, querySvc);

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
        ClearPartidaFilterCommand = new RelayCommand(() => PartidaFilter = "");

        InitializeSectorMatrix();
        LoadPartidas();
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
                    SelectPartida(target);
                else
                    SelectFirstPartida();
                session.PendingTakeoffNavigation = null;
            }
            else
            {
                SelectFirstPartida();
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
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
            OnPropertyChanged(nameof(SelectedPartida));
            OnPropertyChanged(nameof(PartidaCodigo));
            OnPropertyChanged(nameof(PartidaDescripcion));
            OnPropertyChanged(nameof(PartidaUnidad));
            OnPropertyChanged(nameof(PartidaUnidadText));
            OnPropertyChanged(nameof(PartidaPositionText));
            OnPropertyChanged(nameof(PartidaCantidadText));
            OnPropertyChanged(nameof(PartidaObservacionesText));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasPartida));
            OnPropertyChanged(nameof(IsReadOnly));
            NotifyCubicacionSummaryChanged();
            LoadCubicacionRows();
            ((RelayCommand)AplicarConjuntoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)AddRowCommand).RaiseCanExecuteChanged();
            ((RelayCommand)InsertRowCommand).RaiseCanExecuteChanged();
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
                ApplyFilter();
        }
    }

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
    public bool IsReadOnly => session.IsReadOnly || SelectedPartida?.ExcluidoCubicacion == true;

    internal bool CanEdit => !session.IsReadOnly && SelectedPartida?.ExcluidoCubicacion != true;

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

    public string PartidaEstadoText => !HasPartida
        ? "Sin partida seleccionada"
        : SelectedPartida?.ExcluidoCubicacion == true
            ? "Excluida de cubicación"
            : (SelectedPartida?.TieneCubicacion == true ? "Partida cubicada" : "Partida vacía");

    public string PartidaResumenText => !HasPartida
        ? "Seleccione una partida para ver el detalle"
        : $"{Rows.Count(r => r.HasContent):N0} recursos · $ {TotalPartida:N0} CLP";

    public string PartidaCodigo => selectedPartida?.Codigo ?? "";
    public string PartidaDescripcion => selectedPartida?.Descripcion ?? "";
    public string PartidaUnidad => selectedPartida?.Unidad ?? "";

    // Unidad editable de la partida (modo Detalle) — cuando el itemizado no trae unidad, se
    // escribe manual aquí y esa es la que viaja al Resumen del presupuesto.
    public string PartidaUnidadText
    {
        get => PartidaUnidad;
        set
        {
            if (selectedPartida == null) return;
            string clean = (value ?? "").Trim();
            if (selectedPartida.Unidad == clean) return;

            host.UndoService.PushUndoPoint(session, $"Cambiar unidad partida {selectedPartida.Codigo}");
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
                    host.UndoService.PushUndoPoint(session, $"Cambiar cantidad partida {selectedPartida.Codigo}");

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
    {
        var dict = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Rows.Count; i++)
            dict[$"R{i + 1}"] = Rows[i].Cantidad;
        return dict;
    }

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
            if (selectedPartida == null) return;
            var item = session.Itemizado.FirstOrDefault(x => string.Equals(x.Id, selectedPartida.Id, StringComparison.OrdinalIgnoreCase));
            if (item == null) return;

            string clean = value ?? "";
            if (item.Observaciones == clean) return;

            host.UndoService.PushUndoPoint(session, $"Editar observación de {selectedPartida.Codigo}");
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

    // Comandos
    public ICommand AddRowCommand { get; }
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
    public ICommand ClearPartidaFilterCommand { get; }

    // IRefreshablePage
    public void Refresh()
    {
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
    }

    // IPageWithPendingChanges
    bool IPageWithPendingChanges.HasPendingChanges => hasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() { MarkClean(); return true; }
}
