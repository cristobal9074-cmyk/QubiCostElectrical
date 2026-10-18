using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Apu;

public sealed partial class ApuWorkspaceVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges, IPageWithCommitChanges
{
    public ApuWorkspaceVm(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.UndoService, host.Services.Apu, host.PriceBankStore, host.Services.MaterialSearch) { }

    public ApuWorkspaceVm(
        IShellNavigationHost shell,
        BudgetProjectSession session,
        IProjectWorkspaceService workspace,
        WpfUndoService undo,
        IApuService apuSvc,
        IPriceBankStore? priceBankStore = null,
        IMaterialSearchService? materialSearch = null)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.undo = undo;
        this.apuSvc = apuSvc;
        this.autocompleteEngine = new ApuAutocompleteEngine(session, materialSearch ?? new MaterialSearchService(), priceBankStore);
        try
        {
            templateStore = new ApuTemplateStore();
        }
        catch (Exception ex)
        {
            templateStore = null;
            templateStoreStatusText = "La biblioteca de plantillas APU no está disponible. La edición seguirá funcionando sin plantillas.";
            WpfAppHost.Logger?.Warning("ApuWorkspace", "No se pudo inicializar la biblioteca de plantillas APU.", ex);
        }

        Partidas = new ObservableCollection<ApuPartidaVm>();
        Recursos = new ObservableCollection<ApuRecursoVm>();
        RecursosMat = new ObservableCollection<ApuRecursoVm>();
        RecursosMo = new ObservableCollection<ApuRecursoVm>();
        RecursosEquipo = new ObservableCollection<ApuRecursoVm>();
        RecursosSc = new ObservableCollection<ApuRecursoVm>();
        RecursosOtros = new ObservableCollection<ApuRecursoVm>();

        GenerarApuCommand = new RelayCommand(GenerarApu, () => CanEdit && SelectedPartida != null && !showEmptyState);
        SaveCommand = new RelayCommand(Save, () => hasPendingChanges && CanEdit);
        AddRecursoCommand = new RelayCommand<string>(AddRecurso, _ => CanEdit && currentApu != null);
        AddMaterialInlineCommand = new RelayCommand(() => AddMaterialInline(), () => CanEdit && currentApu != null);
        OpenMaterialPickerCommand = new RelayCommand(OpenMaterialPickerModal, () => CanEdit && currentApu != null);
        DeleteRecursoCommand = new RelayCommand<ApuRecursoVm>(DeleteRecurso, r => r != null && CanEdit && currentApu != null);
        DuplicateRecursoCommand = new RelayCommand<ApuRecursoVm>(DuplicateRecurso, r => r != null && CanEdit && currentApu != null);
        GoToCubicacionCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Takeoff));
        GoToPriceBankCommand = new RelayCommand(GoToPriceBank, () => selectedRecurso != null && !string.IsNullOrWhiteSpace(selectedRecurso.CodigoMaterial));
        RefrescarPreciosCommand = new RelayCommand(RefrescarPrecios, () => CanEdit && currentApu != null);
        GuardarComoPlantillaCommand = new RelayCommand(GuardarComoPlantilla, () => CanEdit && currentApu != null && templateStore != null);
        AplicarPlantillaCommand = new RelayCommand(AplicarPlantilla, () => CanEdit && currentApu != null && templateStore != null);
        CrearConjuntoCommand = new RelayCommand(CrearConjunto, () => CanEdit && currentApu != null && currentApu.Recursos.Count > 0);
        ToggleCoefficientsCommand = new RelayCommand(() => IsShowCoefficients = !IsShowCoefficients);
        ResetMarkupsCommand = new RelayCommand(ResetMarkups, () => CanEdit);
        OpenQuickJumpCommand = new RelayCommand(OpenQuickJumpPartida);

        Load();
    }

    public ICommand OpenQuickJumpCommand { get; }
    public ICommand AddMaterialInlineCommand { get; }
    public ICommand OpenMaterialPickerCommand { get; }

    // ─── Listas ──────────────────────────────────────────────────────────────
    public ObservableCollection<ApuPartidaVm> Partidas { get; }
    public ObservableCollection<ApuRecursoVm> Recursos { get; }
    public ObservableCollection<ApuRecursoVm> RecursosMat { get; }
    public ObservableCollection<ApuRecursoVm> RecursosMo { get; }
    public ObservableCollection<ApuRecursoVm> RecursosEquipo { get; }
    public ObservableCollection<ApuRecursoVm> RecursosSc { get; }
    public ObservableCollection<ApuRecursoVm> RecursosOtros { get; }
    public string TemplateStoreStatusText
    {
        get => templateStoreStatusText;
        private set
        {
            if (SetProperty(ref templateStoreStatusText, value))
                OnPropertyChanged(nameof(HasTemplateStoreStatus));
        }
    }
    public bool HasTemplateStoreStatus => !string.IsNullOrWhiteSpace(templateStoreStatusText);

    // ─── Selección ────────────────────────────────────────────────────────────
    public ApuPartidaVm? SelectedPartida
    {
        get => selectedPartida;
        set
        {
            if (!SetProperty(ref selectedPartida, value)) return;
            if (value != null)
            {
                IsShowCoefficients = false;
            }
            LoadApu(value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ApuRecursoVm? SelectedRecurso
    {
        get => selectedRecurso;
        set
        {
            if (!SetProperty(ref selectedRecurso, value)) return;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ─── Encabezado de la partida activa ─────────────────────────────────────
    public string ActiveItemCode => selectedPartida?.CodigoItem ?? "—";
    public string ActiveDescripcion => selectedPartida?.Descripcion ?? "Seleccione una partida";
    public string ActiveUnidad => selectedPartida?.Unidad ?? "—";
    public decimal ActiveCantidad => selectedPartida?.Cantidad ?? 0m;
    public string ActiveEstado => currentApu != null
        ? ApuFromCubicacionService.GetProfessionalStatus(currentApu)
        : (selectedPartida != null ? "Sin APU" : "—");

    // ─── Resumen de costos (calculados en ApuRow, no en VM) ──────────────────
    public decimal CostoMat { get => costoMat; private set => SetProperty(ref costoMat, value); }
    public decimal CostoMo { get => costoMo; private set => SetProperty(ref costoMo, value); }
    public decimal CostoEquipo { get => costoEquipo; private set => SetProperty(ref costoEquipo, value); }
    public decimal CostoSc { get => costoSc; private set => SetProperty(ref costoSc, value); }
    public decimal CostoHerr { get => costoHerr; private set => SetProperty(ref costoHerr, value); }
    public decimal CostoDirecto { get => costoDirecto; private set => SetProperty(ref costoDirecto, value); }
    public decimal Imprevistos { get => imprevistos; private set => SetProperty(ref imprevistos, value); }
    public decimal PrecioUnit { get => precioUnit; private set => SetProperty(ref precioUnit, value); }
    public decimal CostoMoTotal => currentApu != null ? (currentApu.CostoMoUf + currentApu.CostoHhUf) : 0m;
    public decimal CostoTransporteTotal => currentApu != null ? currentApu.CostoTransporteRecursoUf : 0m;
    public decimal VentaUnit { get => ventaUnit; private set => SetProperty(ref ventaUnit, value); }
    public decimal TotalPartida { get => totalPartida; private set => SetProperty(ref totalPartida, value); }

    // ─── Estado de la pantalla ────────────────────────────────────────────────
    public bool ShowEmptyState { get => showEmptyState; private set => SetProperty(ref showEmptyState, value); }
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public bool TieneApu => currentApu != null;
    public bool HasRecursos => currentApu?.Recursos?.Count > 0;
    public string AlertText { get => alertText; private set => SetProperty(ref alertText, value); }
    public bool HasAlerts { get => hasAlerts; private set => SetProperty(ref hasAlerts, value); }
    public int RecursoCount => currentApu?.Recursos?.Count ?? 0;
    public int KpiApuGenerados => Partidas.Count(p => !p.EsTitulo && p.TieneApu);
    public int KpiPrecioOk => Partidas.Count(p => !p.EsTitulo && p.Estado == "OK");
    public int KpiRequierenRevision => Partidas.Count(p => !p.EsTitulo && p.TieneApu && p.Estado != "OK");
    public bool IsReadOnly => session.IsReadOnly;
    public bool CanEdit => !session.IsReadOnly;

    public string PartidaFilter
    {
        get => partidaFilter;
        set
        {
            if (!SetProperty(ref partidaFilter, value)) return;
            ScheduleApplyFilter();
        }
    }

    // ─── Commands ─────────────────────────────────────────────────────────────
    public ICommand GenerarApuCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddRecursoCommand { get; }
    public ICommand DeleteRecursoCommand { get; }
    public ICommand DuplicateRecursoCommand { get; }
    public ICommand GoToCubicacionCommand { get; }
    public ICommand GoToPriceBankCommand { get; }
    public ICommand RefrescarPreciosCommand { get; }
    public ICommand GuardarComoPlantillaCommand { get; }
    public ICommand AplicarPlantillaCommand { get; }
    public ICommand CrearConjuntoCommand { get; }
    public ICommand ToggleCoefficientsCommand { get; }
    public ICommand ResetMarkupsCommand { get; }

    // ─── IRefreshablePage ────────────────────────────────────────────────────
    public void Refresh() => Load();

    // ─── IPageWithPendingChanges / IPageWithCommitChanges ────────────────────
    // Edición en vivo sobre session.Apus. Leave/Save persiste vía session.MarkDirty(ProjectDirtyScope.Apu).
    // Discard restaura baseline JSON capturado en Load/Refresh/Apply.
    bool IPageWithPendingChanges.HasPendingChanges => hasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() => TryDiscardChanges();
    bool IPageWithCommitChanges.TryApplyChanges() => TryApplyChanges();

    public bool TryDiscardChanges()
    {
        if (!hasPendingChanges)
            return true;

        RestoreBaseline();
        MarkClean();
        Load();
        return true;
    }

    public bool TryApplyChanges()
    {
        if (!CanEdit)
            return !hasPendingChanges;

        if (!session.IsDirty)
            session.MarkDirty(ProjectDirtyScope.Apu);

        CaptureBaseline();
        MarkClean();
        return true;
    }

    private void GoToPriceBank()
    {
        var recurso = selectedRecurso;
        if (recurso == null || string.IsNullOrWhiteSpace(recurso.CodigoMaterial))
            return;

        shell.RequestNavigate(ShellRoute.Prices, new PriceBankNavigationContext(recurso.CodigoMaterial));
    }

    // ─── Carga ───────────────────────────────────────────────────────────────
    public void Load()
    {
        ShowEmptyState = !workspace.HasOpenProject;
        if (ShowEmptyState)
        {
            Partidas.Clear();
            ClearApu();
            baselineApus = null;
            hasPendingChanges = false;
            return;
        }

        var prevSelectedId = selectedPartida?.ItemId;
        allPartidas.Clear();
        Partidas.Clear();

        var apuById = session.Apus
            .Where(a => !string.IsNullOrWhiteSpace(a.ItemId))
            .GroupBy(a => a.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in session.Itemizado)
        {
            apuById.TryGetValue(row.Id ?? "", out var apu);
            var vm = new ApuPartidaVm(row, apu);
            allPartidas.Add(vm);
            Partidas.Add(vm);
        }

        ApplyFilter();
        OnPropertyChanged(nameof(KpiApuGenerados));
        OnPropertyChanged(nameof(KpiPrecioOk));
        OnPropertyChanged(nameof(KpiRequierenRevision));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanEdit));

        var pendingId = session.PendingItemizadoSelectionId;
        if (!string.IsNullOrWhiteSpace(pendingId))
            session.PendingItemizadoSelectionId = null;

        var toSelect = !string.IsNullOrWhiteSpace(pendingId)
            ? Partidas.FirstOrDefault(p => string.Equals(p.ItemId, pendingId, StringComparison.OrdinalIgnoreCase))
            : (string.IsNullOrWhiteSpace(prevSelectedId)
                ? null
                : Partidas.FirstOrDefault(p => string.Equals(p.ItemId, prevSelectedId, StringComparison.OrdinalIgnoreCase)));

        SelectedPartida = toSelect ?? Partidas.FirstOrDefault(p => !p.EsTitulo);
        if (!hasPendingChanges)
            ScheduleCaptureBaseline();
        CommandManager.InvalidateRequerySuggested();
    }

    public void NavigateToItem(string itemId)
    {
        var vm = Partidas.FirstOrDefault(p => string.Equals(p.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
        if (vm != null) SelectedPartida = vm;
    }

    private void ScheduleApplyFilter()
    {
        filterDebounceTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        filterDebounceTimer.Stop();
        filterDebounceTimer.Tick -= OnFilterDebounceTick;
        filterDebounceTimer.Tick += OnFilterDebounceTick;
        filterDebounceTimer.Start();
    }

    private void OnFilterDebounceTick(object? sender, EventArgs e)
    {
        filterDebounceTimer?.Stop();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Partidas.Clear();
        var query = partidaFilter?.Trim().ToUpperInvariant() ?? "";
        foreach (var p in allPartidas)
        {
            if (string.IsNullOrWhiteSpace(query) ||
                p.DisplayLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Estado.Contains(query, StringComparison.OrdinalIgnoreCase))
                Partidas.Add(p);
        }
    }

    // ─── Carga del APU de la partida seleccionada ────────────────────────────
    private void LoadApu(ApuPartidaVm? partida)
    {
        IsLoading = true;
        try
        {
            currentApu = null;
            Recursos.Clear();
            RecursosMat.Clear(); RecursosMo.Clear(); RecursosEquipo.Clear();
            RecursosSc.Clear(); RecursosOtros.Clear();

            if (partida == null || partida.EsTitulo)
            {
                ClearApu();
                NotifyHeaderChanged();
                return;
            }

            var apu = session.Apus.FirstOrDefault(a =>
                string.Equals(a.ItemId, partida.ItemId, StringComparison.OrdinalIgnoreCase));

            currentApu = apu;

            if (apu != null)
            {
                foreach (var r in apu.Recursos)
                {
                    var vm = CreateRecursoVm(r);
                    Recursos.Add(vm);
                    switch (r.Tipo)
                    {
                        case ApuTipoRecurso.MAT: RecursosMat.Add(vm); break;
                        case ApuTipoRecurso.MO or ApuTipoRecurso.HH: RecursosMo.Add(vm); break;
                        case ApuTipoRecurso.EQ: RecursosEquipo.Add(vm); break;
                        case ApuTipoRecurso.SC: RecursosSc.Add(vm); break;
                        default: RecursosOtros.Add(vm); break;
                    }
                }
                RefreshTotals();
                BuildAlerts(apu);
            }
            else
            {
                ClearApu();
            }

            NotifyHeaderChanged();
            OnPropertyChanged(nameof(TieneApu));
            OnPropertyChanged(nameof(HasRecursos));
            OnPropertyChanged(nameof(RecursoCount));
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ─── Totales desde ApuRow (sin recalcular) ───────────────────────────────
    internal void RefreshTotals()
    {
        if (currentApu == null)
        {
            ClearApu();
            return;
        }

        var apu = currentApu;
        CostoMat = apu.CostoMaterialUf;
        CostoMo = apu.CostoMoUf;
        CostoEquipo = apu.CostoEquipoUf;
        CostoSc = apu.CostoScUf;
        CostoHerr = apu.HerramientasCalculadoUf;
        CostoDirecto = apu.CostoDirectoUf;
        Imprevistos = apu.ImprevistosUf;
        PrecioUnit = apu.PrecioUnitUf;

        var closingPolicy = session.Data.ClosingPolicy?.Policy ?? ClosingPolicyType.CostPlusMarkup;
        if (closingPolicy == ClosingPolicyType.CostPlusFee)
        {
            VentaUnit = PrecioUnit; // Costo directo comprobable: sin GG ni markup sobre el APU
        }
        else
        {
            var gg = session.ProjectInfo.GastosGeneralesPct;
            var util = session.ProjectInfo.UtilidadPct;
            var markupMethod = Enum.TryParse<MarkupMethod>(session.ProjectInfo.MarkupMethod, true, out var mm) ? mm : MarkupMethod.SobreVenta;
            VentaUnit = ApuFromCubicacionService.CalculateSaleUnitUf(apu, gg, util, markupMethod);
        }
        TotalPartida = apu.TotalPartidaUf;

        selectedPartida?.Refresh(apu);
        OnPropertyChanged(nameof(CostoMoTotal));
        OnPropertyChanged(nameof(CostoTransporteTotal));
        CostDistribution.Recalculate(CostoMat, CostoMoTotal, CostoEquipo, CostoSc, CostoTransporteTotal, CostoDirecto, session.ProjectInfo.MonedaPresupuesto, ValorUfVigente);
        NotifyMoneyDisplays();
    }

    // ─── Política de moneda ──────────────────────────────────────────────────
    // El motor guarda los montos del APU en pesos (ApuRow.*Uf es nombre legado: ver
    // BudgetEngine "apu.PrecioUnitUf ya está en CLP"). La UI, por regla de producto, muestra
    // pesos cuando el proyecto está en CLP y, cuando está en UF/USD, el valor en esa moneda
    // con su equivalente en pesos.
    private static readonly CurrencyConversionService CurrencySvc = new();

    /// <summary>Falso cuando el proyecto está en CLP: no se expone ninguna cifra en UF.</summary>
    public bool MuestraUf
        => !string.Equals(session.ProjectInfo.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);

    private decimal ValorUfVigente
        => session.ProjectInfo.ValorUfClp > 0m ? session.ProjectInfo.ValorUfClp : PriceConversion.DefaultValorUfClp;

    private string Money(decimal clp)
    {
        var info = session.ProjectInfo;
        var (primary, secondary) = CurrencySvc.FormatMoneyPairFromClp(clp, info.MonedaPresupuesto, ValorUfVigente, info.TipoCambioUsdClp);
        return secondary.Length == 0 ? primary : $"{primary} ({secondary})";
    }

    public string PrecioUnitDisplay => Money(PrecioUnit);
    public string TotalPartidaDisplay => Money(TotalPartida);
    public string CostoDirectoDisplay => Money(CostoDirecto);
    public string CostoMatDisplay => Money(CostoMat);
    public string CostoMoTotalDisplay => Money(CostoMoTotal);
    public string CostoEquipoDisplay => Money(CostoEquipo);
    public string CostoScDisplay => Money(CostoSc);
    public string CostoTransporteDisplay => Money(CostoTransporteTotal);
    public string CostoHerrDisplay => Money(CostoHerr);
    public string ImprevistosDisplay => Money(Imprevistos);
    public string VentaUnitDisplay => Money(VentaUnit);

    private void NotifyMoneyDisplays()
    {
        OnPropertyChanged(nameof(MuestraUf));
        OnPropertyChanged(nameof(PrecioUnitDisplay));
        OnPropertyChanged(nameof(TotalPartidaDisplay));
        OnPropertyChanged(nameof(CostoDirectoDisplay));
        OnPropertyChanged(nameof(CostoMatDisplay));
        OnPropertyChanged(nameof(CostoMoTotalDisplay));
        OnPropertyChanged(nameof(CostoEquipoDisplay));
        OnPropertyChanged(nameof(CostoScDisplay));
        OnPropertyChanged(nameof(CostoTransporteDisplay));
        OnPropertyChanged(nameof(CostoHerrDisplay));
        OnPropertyChanged(nameof(ImprevistosDisplay));
        OnPropertyChanged(nameof(VentaUnitDisplay));
    }

    private void ClearApu()
    {
        CostoMat = CostoMo = CostoEquipo = CostoSc = CostoHerr = 0m;
        CostoDirecto = Imprevistos = PrecioUnit = VentaUnit = TotalPartida = 0m;
        CostDistribution.Recalculate(0, 0, 0, 0, 0, 0, session.ProjectInfo.MonedaPresupuesto, ValorUfVigente);
        NotifyMoneyDisplays();
        AlertText = "";
        HasAlerts = false;
    }

    private void NotifyHeaderChanged()
    {
        OnPropertyChanged(nameof(ActiveItemCode));
        OnPropertyChanged(nameof(ActiveDescripcion));
        OnPropertyChanged(nameof(ActiveUnidad));
        OnPropertyChanged(nameof(ActiveCantidad));
        OnPropertyChanged(nameof(ActiveEstado));
    }

    private void BuildAlerts(ApuRow apu)
    {
        var alerts = ApuAlertRules.Build(apu, AppClock.Now);
        AlertText = alerts.Count > 0 ? string.Join("  •  ", alerts) : "";
        HasAlerts = alerts.Count > 0;
    }

    // ─── Generar APU desde cubicación ────────────────────────────────────────
    private void GenerarApu()
    {
        if (CanEdit == false || selectedPartida == null) return;
        var item = session.Itemizado.FirstOrDefault(i =>
            string.Equals(i.Id, selectedPartida.ItemId, StringComparison.OrdinalIgnoreCase));
        if (item == null) return;

        var cubicacion = session.Cubicacion
            .Where(c => string.Equals(c.ItemId, selectedPartida.ItemId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (cubicacion.Count == 0)
        {
            WpfDialogService.Info("Generar APU", "Esta partida no tiene cubicación valorizada.");
            return;
        }

        undo.PushUndoPoint(session, $"Generar APU {selectedPartida.CodigoItem}");
        pendingUndoForEdit = true;
        var apu = ApuFromCubicacionService.BuildFromCubicacion(item, cubicacion, apuSvc);
        var existing = session.Apus.FindIndex(a =>
            string.Equals(a.ItemId, selectedPartida.ItemId, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) session.Apus[existing] = apu;
        else session.Apus.Add(apu);

        session.MarkDirty(ProjectDirtyScope.Apu);
        MarkDirty();
        LoadApu(selectedPartida);
    }

    public ApuRecursoVm? AddMaterialInline()
    {
        if (!CanEdit || currentApu == null) return null;
        undo.PushUndoPoint(session, "Agregar material APU (inline)");
        pendingUndoForEdit = true;
        var recurso = new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            CodigoMaterial = "",
            Descripcion = "",
            Unidad = "un",
            Rendimiento = 1m,
            PrecioUf = 0m,
            MermaPct = 0m,
            ImprevistosPct = 0m,
            FuentePrecio = "Banco de precios",
            FechaPrecio = AppClock.Now
        };
        currentApu.Recursos.Add(recurso);
        var vm = CreateRecursoVm(recurso);
        Recursos.Add(vm);
        RecursosMat.Add(vm);
        SelectedRecurso = vm;
        OnRowEdited();
        return vm;
    }

    public void OpenMaterialPickerModal()
    {
        if (!CanEdit || currentApu == null) return;
        var dialog = new Templates.MaterialPickerDialog(session.Materiales);
        if (System.Windows.Application.Current.MainWindow != null)
        {
            dialog.Owner = System.Windows.Application.Current.MainWindow;
        }

        if (dialog.ShowDialog() == true && dialog.SelectedMaterials.Count > 0)
        {
            undo.PushUndoPoint(session, "Agregar material APU");
            pendingUndoForEdit = true;
            foreach (var mat in dialog.SelectedMaterials)
            {
                // ApuRecursoRow.PrecioUf es el precio unitario en CLP (nombre legado). El catálogo
                // de materiales guarda CLP y deriva UF; se usa el CLP para no inyectar UF donde el
                // motor espera pesos.
                decimal precioClp = mat.MatClpConFallback;
                if (precioClp <= 0m && mat.MatUf > 0m)
                    precioClp = Math.Round(mat.MatUf * ValorUfVigente, MidpointRounding.AwayFromZero);

                var recurso = new ApuRecursoRow
                {
                    Tipo = ApuTipoRecurso.MAT,
                    CodigoMaterial = mat.Codigo,
                    Descripcion = mat.Descripcion,
                    Unidad = mat.Unidad,
                    Rendimiento = 1m,
                    PrecioUf = precioClp,
                    MermaPct = 0m,
                    ImprevistosPct = 0m,
                    FuentePrecio = "Banco de precios",
                    FechaPrecio = AppClock.Now
                };
                currentApu.Recursos.Add(recurso);
                var vm = CreateRecursoVm(recurso);
                Recursos.Add(vm);
                RecursosMat.Add(vm);
            }
            OnRowEdited();
        }
    }

    private void AddRecurso(string? tipoStr)
    {
        if (!CanEdit || currentApu == null) return;
        var tipo = ApuTipoRecurso.MAT;
        if (!string.IsNullOrEmpty(tipoStr) && Enum.TryParse<ApuTipoRecurso>(tipoStr, true, out var t))
            tipo = t;

        if (tipo == ApuTipoRecurso.MAT)
        {
            AddMaterialInline();
            return;
        }

        // Para otros recursos no materiales, se mantiene el comportamiento de agregar una fila vacía
        undo.PushUndoPoint(session, $"Agregar recurso {tipo}");
        pendingUndoForEdit = true;
        var recursoVacio = new ApuRecursoRow { Tipo = tipo, Descripcion = "Nuevo recurso" };
        currentApu.Recursos.Add(recursoVacio);
        var vmVacio = CreateRecursoVm(recursoVacio);
        Recursos.Add(vmVacio);
        switch (tipo)
        {
            case ApuTipoRecurso.MO or ApuTipoRecurso.HH: RecursosMo.Add(vmVacio); break;
            case ApuTipoRecurso.EQ: RecursosEquipo.Add(vmVacio); break;
            case ApuTipoRecurso.SC: RecursosSc.Add(vmVacio); break;
            default: RecursosOtros.Add(vmVacio); break;
        }
        OnRowEdited();
    }

    // ─── Autocompletado e IntelliSense en Grilla APU ──────────────────────────
    public ObservableCollection<ApuAutocompleteSuggestionVm> AutocompleteResults
    {
        get => autocompleteResults;
        set => SetProperty(ref autocompleteResults, value);
    }

    public bool AutocompleteVisible
    {
        get => autocompleteVisible;
        set
        {
            if (SetProperty(ref autocompleteVisible, value) && !value)
            {
                autocompleteCts?.Cancel();
            }
        }
    }

    public ApuAutocompleteSuggestionVm? SelectedAutocompleteItem
    {
        get => selectedAutocompleteItem;
        set => SetProperty(ref selectedAutocompleteItem, value);
    }

    public string AutocompleteStatusText
    {
        get => autocompleteStatusText;
        set => SetProperty(ref autocompleteStatusText, value);
    }

    public ApuAutocompleteSuggestionVm? AutocompleteFirstItem =>
        AutocompleteResults.Count > 0 ? AutocompleteResults[0] : null;

    public async Task SearchAutocompleteAsync(string query)
    {
        autocompleteCts?.Cancel();
        autocompleteCts?.Dispose();
        autocompleteCts = new System.Threading.CancellationTokenSource();
        var ct = autocompleteCts.Token;

        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            AutocompleteVisible = false;
            AutocompleteResults = new ObservableCollection<ApuAutocompleteSuggestionVm>();
            SelectedAutocompleteItem = null;
            return;
        }

        try
        {
            var results = await autocompleteEngine.SearchAsync(query, ct);
            if (!ct.IsCancellationRequested)
            {
                AutocompleteResults = new ObservableCollection<ApuAutocompleteSuggestionVm>(results);
                SelectedAutocompleteItem = AutocompleteFirstItem;
                AutocompleteVisible = results.Count > 0;
                AutocompleteStatusText = results.Count > 0
                    ? $"{results.Count} sugerencias · ↑↓ navegar · Enter o Tab insertar"
                    : "Sin coincidencias";
                OnPropertyChanged(nameof(AutocompleteFirstItem));
            }
        }
        catch (OperationCanceledException) { }
    }

    public void ApplyAutocompleteSuggestion(ApuRecursoVm row, ApuAutocompleteSuggestionVm suggestion)
    {
        if (row == null || suggestion == null || !CanEdit) return;
        undo.PushUndoPoint(session, $"Aplicar insumo {suggestion.Codigo}");
        pendingUndoForEdit = true;

        row.CodigoMaterial = suggestion.Codigo;
        row.Descripcion = suggestion.Descripcion;
        row.Unidad = string.IsNullOrWhiteSpace(suggestion.Unidad) ? "un" : suggestion.Unidad;
        // El motor espera el precio del recurso en CLP (ver nota en MuestraUf); la sugerencia
        // trae MatClp ya resuelto desde el catálogo/banco.
        row.PrecioUf = suggestion.MatClp;
        row.Tipo = ApuTipoRecurso.MAT;
        row.FuentePrecio = suggestion.SourceBadge;
        if (!string.IsNullOrWhiteSpace(suggestion.Proveedor))
            row.Nota = suggestion.Proveedor;
        if (row.Rendimiento <= 0m)
            row.Rendimiento = 1m;

        row.RefreshCalculated();
        OnRowEdited();
    }

    private void DeleteRecurso(ApuRecursoVm? vm)
    {
        if (!CanEdit || vm == null || currentApu == null) return;
        if (!WpfDialogService.Confirm("Eliminar recurso",
            $"¿Eliminar '{vm.Descripcion}'?", isWarning: true))
            return;

        undo.PushUndoPoint(session, $"Eliminar recurso APU");
        pendingUndoForEdit = true;
        currentApu.Recursos.Remove(vm.Source);
        Recursos.Remove(vm);
        RecursosMat.Remove(vm); RecursosMo.Remove(vm); RecursosEquipo.Remove(vm);
        RecursosSc.Remove(vm); RecursosOtros.Remove(vm);
        OnRowEdited();
    }

    private void DuplicateRecurso(ApuRecursoVm? vm)
    {
        if (!CanEdit || vm == null || currentApu == null) return;
        undo.PushUndoPoint(session, "Duplicar recurso APU");
        pendingUndoForEdit = true;
        var clone = new ApuRecursoRow
        {
            Tipo = vm.Source.Tipo,
            CodigoMaterial = vm.Source.CodigoMaterial,
            Descripcion = vm.Source.Descripcion,
            Unidad = vm.Source.Unidad,
            Rendimiento = vm.Source.Rendimiento,
            PrecioUf = vm.Source.PrecioUf,
            ValorHHUf = vm.Source.ValorHHUf,
            MermaPct = vm.Source.MermaPct,
            ImprevistosPct = vm.Source.ImprevistosPct,
            FuentePrecio = vm.Source.FuentePrecio,
            FechaPrecio = vm.Source.FechaPrecio,
        };
        currentApu.Recursos.Add(clone);
        var cloneVm = CreateRecursoVm(clone);
        Recursos.Add(cloneVm);
        switch (clone.Tipo)
        {
            case ApuTipoRecurso.MAT: RecursosMat.Add(cloneVm); break;
            case ApuTipoRecurso.MO or ApuTipoRecurso.HH: RecursosMo.Add(cloneVm); break;
            case ApuTipoRecurso.EQ: RecursosEquipo.Add(cloneVm); break;
            case ApuTipoRecurso.SC: RecursosSc.Add(cloneVm); break;
            default: RecursosOtros.Add(cloneVm); break;
        }
        OnRowEdited();
    }

    private void RefrescarPrecios()
    {
        if (!CanEdit || currentApu == null) return;
        undo.PushUndoPoint(session, "Refrescar precios APU");
        pendingUndoForEdit = true;
        var materiales = session.Materiales;
        ApuFromCubicacionService.RefreshPricesFromCatalog(
            new List<ApuRow> { currentApu }, materiales, session.ProjectInfo.PreciosCongelados, session.ProjectInfo);
        foreach (var vm in Recursos) vm.RefreshCalculated();
        OnRowEdited();
    }

    public ProjectFileData? CaptureSnapshot() => CanEdit ? session.CreateSnapshot() : null;

    internal void OnRowEdited(ProjectFileData? preEditSnapshot = null)
    {
        if (CanEdit && currentApu != null)
        {
            if (preEditSnapshot != null)
                undo.PushSnapshot(preEditSnapshot, "Editar recurso APU");
            else if (!pendingUndoForEdit)
                undo.PushUndoPoint(session, "Editar recurso APU");
        }
        pendingUndoForEdit = false;
        RebuildGroupedCollections();
        RefreshTotals();
        BuildAlerts(currentApu!);
        MarkDirty();
        NotifyHeaderChanged();
        OnPropertyChanged(nameof(HasRecursos));
        OnPropertyChanged(nameof(RecursoCount));
        CommandManager.InvalidateRequerySuggested();
    }

    internal void ClearPendingUndoForEdit() => pendingUndoForEdit = false;

    private void RebuildGroupedCollections()
    {
        if (currentApu == null) return;
        RecursosMat.Clear(); RecursosMo.Clear(); RecursosEquipo.Clear();
        RecursosSc.Clear(); RecursosOtros.Clear();
        foreach (var vm in Recursos)
        {
            switch (vm.Tipo)
            {
                case ApuTipoRecurso.MAT: RecursosMat.Add(vm); break;
                case ApuTipoRecurso.MO or ApuTipoRecurso.HH: RecursosMo.Add(vm); break;
                case ApuTipoRecurso.EQ: RecursosEquipo.Add(vm); break;
                case ApuTipoRecurso.SC: RecursosSc.Add(vm); break;
                default: RecursosOtros.Add(vm); break;
            }
        }
    }

    // ─── Guardar ─────────────────────────────────────────────────────────────
    private void Save()
    {
        if (!CanEdit)
            return;
        if (currentApu != null)
        {
            var errors = apuSvc.Validar(currentApu);
            if (errors.Count > 0)
            {
                WpfDialogService.Info("Validación", string.Join("\n", errors));
                return;
            }
        }
        _ = shell.SaveProjectAsync();
        CaptureBaseline();
        MarkClean();
    }

    private void MarkDirty()
    {
        if (session.IsReadOnly)
            return;

        hasPendingChanges = true;
        session.MarkDirty(ProjectDirtyScope.Apu);
        CommandManager.InvalidateRequerySuggested();
    }

    private void MarkClean()
    {
        hasPendingChanges = false;
        pendingUndoForEdit = false;
        CommandManager.InvalidateRequerySuggested();
    }

    private ApuRecursoVm CreateRecursoVm(ApuRecursoRow row) => new(row, () => CanEdit);

    private void CaptureBaseline()
    {
        baselineApus = CloneJson(session.Apus.ToList());
    }

    private void ScheduleCaptureBaseline()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            if (session.Apus.Count <= 500)
                CaptureBaseline();
            return;
        }

        _ = dispatcher.InvokeAsync(() =>
        {
            if (!hasPendingChanges)
                CaptureBaseline();
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void RestoreBaseline()
    {
        if (baselineApus == null)
            return;

        var restored = CloneJson(baselineApus);
        session.Apus.Clear();
        foreach (var apu in restored)
            session.Apus.Add(apu);
    }

    private static T CloneJson<T>(T source)
    {
        var json = JsonSerializer.Serialize(source, BaselineJsonOptions);
        return JsonSerializer.Deserialize<T>(json, BaselineJsonOptions)
               ?? throw new InvalidOperationException($"No se pudo clonar {typeof(T).Name} para baseline APU.");
    }

    // ─── Guardar como plantilla ──────────────────────────────────────────────
    private void GuardarComoPlantilla()
    {
        if (!CanEdit || currentApu == null || templateStore == null) return;
        var nombre = WpfDialogService.PromptText("Guardar plantilla",
            "Nombre de la plantilla:",
            selectedPartida?.Descripcion ?? "Nueva plantilla");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var json = JsonSerializer.Serialize(currentApu.Recursos,
            new JsonSerializerOptions { WriteIndented = false });
        var row = new ApuTemplateRow
        {
            Nombre = nombre.Trim(),
            Categoria = "APU",
            Descripcion = selectedPartida?.Descripcion ?? "",
            Unidad = selectedPartida?.Unidad ?? "",
            JsonRecursos = json,
        };
        templateStore.Save(row);
        WpfDialogService.Info("Plantilla guardada", $"Plantilla '{row.Nombre}' guardada correctamente.");
    }

    // ─── Aplicar plantilla ───────────────────────────────────────────────────
    private void AplicarPlantilla()
    {
        if (!CanEdit || currentApu == null || templateStore == null) return;
        var dialog = new ApuTemplatePickerDialog(templateStore);
        if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null) return;

        ApplyTemplate(dialog.SelectedTemplate, dialog.Reemplazar);
    }

    internal bool ApplyTemplate(ApuTemplateRow template, bool reemplazar)
    {
        if (!CanEdit || currentApu == null) return false;

        List<ApuRecursoRow>? recursos = null;
        try
        {
            recursos = JsonSerializer.Deserialize<List<ApuRecursoRow>>(template.JsonRecursos ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            TemplateStoreStatusText = "La plantilla seleccionada no se pudo leer. Use otra plantilla o vuelva a exportarla.";
            WpfAppHost.Logger?.Warning("ApuWorkspace", "No se pudo deserializar la plantilla APU seleccionada.", ex);
        }

        if (recursos == null || recursos.Count == 0)
        {
            WpfDialogService.Info("Aplicar plantilla", "La plantilla no tiene recursos.");
            return false;
        }

        if (reemplazar && currentApu.Recursos.Count > 0)
        {
            var partidaDesc = selectedPartida?.Descripcion ?? currentApu.Descripcion ?? "la partida actual";
            var confirmMsg = $"El APU de '{partidaDesc}' ya contiene {currentApu.Recursos.Count} recurso(s) configurado(s).\n\n¿Está seguro de que desea reemplazarlos y eliminarlos definitivamente para aplicar la plantilla '{template.Nombre}'?";
            if (!ConfirmOverwriteTemplate(confirmMsg))
                return false;
        }

        undo.PushUndoPoint(session, $"Aplicar plantilla APU");
        pendingUndoForEdit = true;
        if (reemplazar)
        {
            currentApu.Recursos.Clear();
            Recursos.Clear();
        }

        foreach (var r in recursos)
        {
            r.Id = Guid.NewGuid().ToString("N");
            currentApu.Recursos.Add(r);
            Recursos.Add(CreateRecursoVm(r));
        }
        OnRowEdited();
        return true;
    }

    // ─── Crear conjunto eléctrico desde los recursos del APU actual ──────────
    private void CrearConjunto()
    {
        if (!CanEdit || currentApu == null || currentApu.Recursos.Count == 0) return;

        var nombre = WpfDialogService.PromptText("Crear conjunto",
            "Nombre del conjunto:",
            selectedPartida?.Descripcion ?? "Nuevo conjunto");
        if (string.IsNullOrWhiteSpace(nombre)) return;

        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = $"CE-{DateTime.Now:yyMMdd-HHmm}",
            Nombre = nombre.Trim(),
            Descripcion = selectedPartida?.Descripcion ?? "",
            Unidad = selectedPartida?.Unidad ?? "UN",
            TipoConjunto = ConjuntoTipo.General,
        };

        foreach (var r in currentApu.Recursos)
        {
            conjunto.Recursos.Add(new ConjuntoRecursoRow
            {
                Tipo = r.Tipo,
                Codigo = r.CodigoMaterial,
                Descripcion = r.Descripcion,
                Unidad = r.Unidad,
                Rendimiento = r.Rendimiento,
                Factor = 1m,
                PrecioUf = r.PrecioUf,
                ValorHHUf = r.ValorHHUf,
                MermaPct = r.MermaPct,
            });
        }

        session.ConjuntosElectricos.Add(conjunto);
        session.MarkDirty(ProjectDirtyScope.ConjuntosElectricos);
        bool guardadoGlobal = ConjuntoTemplateStore.TrySave(conjunto);
        WpfDialogService.Info("Conjunto creado",
            $"Conjunto '{conjunto.Nombre}' creado con {conjunto.Recursos.Count} recursos." +
            (guardadoGlobal
                ? "\nGuardado en la biblioteca global: visible en todos los proyectos."
                : "\nNo se pudo guardar en la biblioteca global (solo en este proyecto)."));
    }

    private bool isShowCoefficients;
    public bool IsShowCoefficients
    {
        get => isShowCoefficients;
        set
        {
            if (SetProperty(ref isShowCoefficients, value))
            {
                OnPropertyChanged(nameof(TieneApu));
                OnPropertyChanged(nameof(ShowEmptyState));
                if (value)
                {
                    selectedPartida = null;
                    OnPropertyChanged(nameof(SelectedPartida));
                }
            }
        }
    }

    public string MarkupConductoresText
    {
        get => session.GetFamilyMarkup("Conductores").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Conductores", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupCanalizacionText
    {
        get => session.GetFamilyMarkup("Canalización").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Canalización", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupTablerosText
    {
        get => session.GetFamilyMarkup("Tableros").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Tableros", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupProteccionesText
    {
        get => session.GetFamilyMarkup("Protecciones").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Protecciones", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupIluminacionText
    {
        get => session.GetFamilyMarkup("Iluminación").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Iluminación", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupEquiposText
    {
        get => session.GetFamilyMarkup("Equipos").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Equipos", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupEnchufesText
    {
        get => session.GetFamilyMarkup("Enchufes y tomacorrientes").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Enchufes y tomacorrientes", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupPuestaTierraText
    {
        get => session.GetFamilyMarkup("Puesta a tierra").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Puesta a tierra", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupAccesoriosText
    {
        get => session.GetFamilyMarkup("Accesorios").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (!CanEdit) return;
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                session.SetFamilyMarkup("Accesorios", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    private void ResetMarkups()
    {
        undo.PushUndoPoint(session, "Reset markups APU");
        session.SetFamilyMarkup("Conductores", 1.00m);
        session.SetFamilyMarkup("Canalización", 1.00m);
        session.SetFamilyMarkup("Tableros", 1.00m);
        session.SetFamilyMarkup("Protecciones", 1.00m);
        session.SetFamilyMarkup("Iluminación", 1.00m);
        session.SetFamilyMarkup("Equipos", 1.00m);
        session.SetFamilyMarkup("Enchufes y tomacorrientes", 1.00m);
        session.SetFamilyMarkup("Puesta a tierra", 1.00m);
        session.SetFamilyMarkup("Accesorios", 1.00m);

        OnPropertyChanged(nameof(MarkupConductoresText));
        OnPropertyChanged(nameof(MarkupCanalizacionText));
        OnPropertyChanged(nameof(MarkupTablerosText));
        OnPropertyChanged(nameof(MarkupProteccionesText));
        OnPropertyChanged(nameof(MarkupIluminacionText));
        OnPropertyChanged(nameof(MarkupEquiposText));
        OnPropertyChanged(nameof(MarkupEnchufesText));
        OnPropertyChanged(nameof(MarkupPuestaTierraText));
        OnPropertyChanged(nameof(MarkupAccesoriosText));

        MarkDirty();
    }

    public void OpenQuickJumpPartida()
    {
        if (Partidas.Count == 0) return;

        string currentCapitulo = "";
        var jumpItems = new List<QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem>(Partidas.Count);

        foreach (var p in Partidas)
        {
            if (p.EsTitulo)
            {
                currentCapitulo = p.Descripcion;
                jumpItems.Add(new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
                {
                    Id = p.ItemId,
                    Codigo = p.CodigoItem,
                    Descripcion = p.Descripcion,
                    EsTitulo = true
                });
            }
            else
            {
                jumpItems.Add(new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaItem
                {
                    Id = p.ItemId,
                    Codigo = p.CodigoItem,
                    Descripcion = p.Descripcion,
                    Unidad = p.Unidad,
                    Cantidad = p.Cantidad,
                    EsTitulo = false,
                    BadgeText = p.Estado,
                    Capitulo = currentCapitulo
                });
            }
        }

        var dialog = new QubiCostElectrical.Wpf.Controls.QuickJumpPartidaDialog(jumpItems, SelectedPartida?.ItemId)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.SelectedPartidaId))
        {
            var match = Partidas.FirstOrDefault(p => string.Equals(p.ItemId, dialog.SelectedPartidaId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                SelectedPartida = match;
            }
        }
    }
}
