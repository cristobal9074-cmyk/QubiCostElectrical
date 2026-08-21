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

public sealed partial class ApuWorkspaceVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    public ApuWorkspaceVm(IShellNavigationHost shell, WpfAppHost host)
    {
        this.shell = shell;
        this.host = host;
        session = host.Session;
        apuSvc = host.Services.Apu;
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

        Load();
    }

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
    private bool CanEdit => !session.IsReadOnly;

    public string PartidaFilter
    {
        get => partidaFilter;
        set
        {
            if (!SetProperty(ref partidaFilter, value)) return;
            ApplyFilter();
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

    // ─── IPageWithPendingChanges ─────────────────────────────────────────────
    bool IPageWithPendingChanges.HasPendingChanges => hasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() { MarkClean(); return true; }

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
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
        if (ShowEmptyState)
        {
            Partidas.Clear();
            ClearApu();
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

        var toSelect = string.IsNullOrWhiteSpace(prevSelectedId)
            ? null
            : Partidas.FirstOrDefault(p => string.Equals(p.ItemId, prevSelectedId, StringComparison.OrdinalIgnoreCase));

        SelectedPartida = toSelect ?? Partidas.FirstOrDefault(p => !p.EsTitulo);
        CommandManager.InvalidateRequerySuggested();
    }

    public void NavigateToItem(string itemId)
    {
        var vm = Partidas.FirstOrDefault(p => string.Equals(p.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
        if (vm != null) SelectedPartida = vm;
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
                var vm = new ApuRecursoVm(r);
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

        var gg = session.ProjectInfo.GastosGeneralesPct;
        var util = session.ProjectInfo.UtilidadPct;
        var markupMethod = Enum.TryParse<MarkupMethod>(session.ProjectInfo.MarkupMethod, true, out var mm) ? mm : MarkupMethod.SobreVenta;
        VentaUnit = ApuFromCubicacionService.CalculateSaleUnitUf(apu, gg, util, markupMethod);
        TotalPartida = apu.TotalPartidaUf;

        selectedPartida?.Refresh(apu);
        OnPropertyChanged(nameof(CostoMoTotal));
        OnPropertyChanged(nameof(CostoTransporteTotal));
    }

    private void ClearApu()
    {
        CostoMat = CostoMo = CostoEquipo = CostoSc = CostoHerr = 0m;
        CostoDirecto = Imprevistos = PrecioUnit = VentaUnit = TotalPartida = 0m;
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

        var apu = ApuFromCubicacionService.BuildFromCubicacion(item, cubicacion, apuSvc);
        var existing = session.Apus.FindIndex(a =>
            string.Equals(a.ItemId, selectedPartida.ItemId, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) session.Apus[existing] = apu;
        else session.Apus.Add(apu);

        session.MarkDirty("apu");
        MarkDirty();
        LoadApu(selectedPartida);
    }

    private void AddRecurso(string? tipoStr)
    {
        if (!CanEdit || currentApu == null) return;
        var tipo = ApuTipoRecurso.MAT;
        if (!string.IsNullOrEmpty(tipoStr) && Enum.TryParse<ApuTipoRecurso>(tipoStr, true, out var t))
            tipo = t;

        if (tipo == ApuTipoRecurso.MAT)
        {
            var dialog = new Templates.MaterialPickerDialog(session.Materiales);
            if (System.Windows.Application.Current.MainWindow != null)
            {
                dialog.Owner = System.Windows.Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true && dialog.SelectedMaterials.Count > 0)
            {
                foreach (var mat in dialog.SelectedMaterials)
                {
                    var recurso = new ApuRecursoRow
                    {
                        Tipo = ApuTipoRecurso.MAT,
                        CodigoMaterial = mat.Codigo,
                        Descripcion = mat.Descripcion,
                        Unidad = mat.Unidad,
                        Rendimiento = 1m,
                        PrecioUf = mat.MatUf,
                        MermaPct = 0m,
                        ImprevistosPct = 0m,
                        FuentePrecio = "Banco de precios",
                        FechaPrecio = AppClock.Now
                    };
                    currentApu.Recursos.Add(recurso);
                    var vm = new ApuRecursoVm(recurso);
                    Recursos.Add(vm);
                    RecursosMat.Add(vm);
                }
                OnRowEdited();
            }
            return;
        }

        // Para otros recursos no materiales, se mantiene el comportamiento de agregar una fila vacía
        var recursoVacio = new ApuRecursoRow { Tipo = tipo, Descripcion = "Nuevo recurso" };
        currentApu.Recursos.Add(recursoVacio);
        var vmVacio = new ApuRecursoVm(recursoVacio);
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

    private void DeleteRecurso(ApuRecursoVm? vm)
    {
        if (!CanEdit || vm == null || currentApu == null) return;
        if (!WpfDialogService.Confirm("Eliminar recurso",
            $"¿Eliminar '{vm.Descripcion}'? Esta acción no se puede deshacer.", isWarning: true))
            return;

        currentApu.Recursos.Remove(vm.Source);
        Recursos.Remove(vm);
        RecursosMat.Remove(vm); RecursosMo.Remove(vm); RecursosEquipo.Remove(vm);
        RecursosSc.Remove(vm); RecursosOtros.Remove(vm);
        OnRowEdited();
    }

    private void DuplicateRecurso(ApuRecursoVm? vm)
    {
        if (!CanEdit || vm == null || currentApu == null) return;
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
        var cloneVm = new ApuRecursoVm(clone);
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
        var materiales = session.Materiales;
        ApuFromCubicacionService.RefreshPricesFromCatalog(
            new List<ApuRow> { currentApu }, materiales, session.ProjectInfo.PreciosCongelados);
        foreach (var vm in Recursos) vm.RefreshCalculated();
        OnRowEdited();
    }

    internal void OnRowEdited()
    {
        RebuildGroupedCollections();
        RefreshTotals();
        BuildAlerts(currentApu!);
        MarkDirty();
        NotifyHeaderChanged();
        OnPropertyChanged(nameof(HasRecursos));
        OnPropertyChanged(nameof(RecursoCount));
        CommandManager.InvalidateRequerySuggested();
    }

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
        MarkClean();
    }

    private void MarkDirty()
    {
        hasPendingChanges = true;
        CommandManager.InvalidateRequerySuggested();
    }

    private void MarkClean()
    {
        hasPendingChanges = false;
        CommandManager.InvalidateRequerySuggested();
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

        var template = dialog.SelectedTemplate;
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
            return;
        }

        if (dialog.Reemplazar)
        {
            currentApu.Recursos.Clear();
            Recursos.Clear();
        }

        foreach (var r in recursos)
        {
            r.Id = Guid.NewGuid().ToString("N");
            currentApu.Recursos.Add(r);
            Recursos.Add(new ApuRecursoVm(r));
        }
        OnRowEdited();
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
        session.MarkDirty("conjuntos");
        WpfDialogService.Info("Conjunto creado",
            $"Conjunto '{conjunto.Nombre}' creado con {conjunto.Recursos.Count} recursos.");
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
        get => host.Session.GetFamilyMarkup("Conductores").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Conductores", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupCanalizacionText
    {
        get => host.Session.GetFamilyMarkup("Canalización").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Canalización", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupTablerosText
    {
        get => host.Session.GetFamilyMarkup("Tableros").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Tableros", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupProteccionesText
    {
        get => host.Session.GetFamilyMarkup("Protecciones").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Protecciones", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupIluminacionText
    {
        get => host.Session.GetFamilyMarkup("Iluminación").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Iluminación", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupEquiposText
    {
        get => host.Session.GetFamilyMarkup("Equipos").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Equipos", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupEnchufesText
    {
        get => host.Session.GetFamilyMarkup("Enchufes y tomacorrientes").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Enchufes y tomacorrientes", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupPuestaTierraText
    {
        get => host.Session.GetFamilyMarkup("Puesta a tierra").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Puesta a tierra", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    public string MarkupAccesoriosText
    {
        get => host.Session.GetFamilyMarkup("Accesorios").ToString("N2", CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                host.Session.SetFamilyMarkup("Accesorios", v);
                OnPropertyChanged();
                MarkDirty();
            }
        }
    }

    private void ResetMarkups()
    {
        host.Session.SetFamilyMarkup("Conductores", 1.00m);
        host.Session.SetFamilyMarkup("Canalización", 1.00m);
        host.Session.SetFamilyMarkup("Tableros", 1.00m);
        host.Session.SetFamilyMarkup("Protecciones", 1.00m);
        host.Session.SetFamilyMarkup("Iluminación", 1.00m);
        host.Session.SetFamilyMarkup("Equipos", 1.00m);
        host.Session.SetFamilyMarkup("Enchufes y tomacorrientes", 1.00m);
        host.Session.SetFamilyMarkup("Puesta a tierra", 1.00m);
        host.Session.SetFamilyMarkup("Accesorios", 1.00m);

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
}
