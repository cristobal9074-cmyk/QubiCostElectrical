using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed partial class CostoViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    public CostoViewModel(IShellNavigationHost shell, WpfAppHost host) : base(host)
    {
        this.shell = shell;
        this.host = host;
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        goToTakeoffCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Takeoff), () => !ShowEmptyState);
        goToItemizedCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized), () => !ShowEmptyState);
        goToManoDeObraCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.ManoDeObra), () => !ShowEmptyState);
        goToEppCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Epp), () => !ShowEmptyState);
        goToReviewCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Review), () => !ShowEmptyState);
        AddGoItemCommand = new RelayCommand<GastoOpCategoriaVm>(AddGoItem, _ => CanEdit);
        RemoveGoItemCommand = new RelayCommand<GastoOpItemVm>(RemoveGoItem, _ => CanEdit);
        AddGoCategoriaCommand = new RelayCommand(AddGoCategoria, () => CanEdit);
        RemoveGoCategoriaCommand = new RelayCommand<GastoOpCategoriaVm>(RemoveGoCategoria, _ => CanEdit);
        ResetGoPlantillaCommand = new RelayCommand(ResetGoPlantilla, () => CanEdit);
        ToggleGoCategoriaCommand = new RelayCommand<GastoOpCategoriaVm>(ToggleGoCategoria);
        OpenViaticosCommand = new RelayCommand<GastoOpCategoriaVm>(OpenViaticos, c => CanEdit && c is { IsViaticosCategory: true });
        ResetCoeficientesCommand = new RelayCommand(ResetCoeficientes, () => CanEdit);
        LoadPelCoeficientesCommand = new RelayCommand(LoadPel029Coeficientes, () => CanEdit);
        LoadTipicosCoeficientesCommand = new RelayCommand(LoadTipicosCoeficientes, () => CanEdit);
        SaveCostosPresetCommand = new RelayCommand(SaveCostosPreset, () => CanEdit);
        RestoreCostosPresetCommand = new RelayCommand(RestoreCostosPreset, () => CanEdit && HasCostosPreset);
        ApplyCoeficientesCommand = new RelayCommand(ApplyCoeficientes, () => CanEdit);
        ClosingPolicySection = new ClosingPolicySectionVm(CommitClosingPolicyType, CommitClosingPolicyParam);
        EnsureCoefFactors();
        Refresh();
    }

    // ─── Propiedades ──────────────────────────────────────────────────────────

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public bool ShowFamilias
    {
        get => showFamilias;
        private set => SetProperty(ref showFamilias, value);
    }

    public bool ShowMoSection
    {
        get => showMoSection;
        private set => SetProperty(ref showMoSection, value);
    }

    public bool ShowGoSection
    {
        get => showGoSection;
        private set => SetProperty(ref showGoSection, value);
    }

    public bool ShowOrigenes
    {
        get => showOrigenes;
        private set => SetProperty(ref showOrigenes, value);
    }

    public string CostoDirectoLabel { get => costDirectoLabel; private set => SetProperty(ref costDirectoLabel, value); }
    public string CostoDirectoSubLabel { get => costDirectoSubLabel; private set => SetProperty(ref costDirectoSubLabel, value); }
    public string TotalVentaLabel { get => totalVentaLabel; private set => SetProperty(ref totalVentaLabel, value); }
    public string TotalVentaSubLabel { get => totalVentaSubLabel; private set => SetProperty(ref totalVentaSubLabel, value); }
    public string MargenLabel { get => margenLabel; private set => SetProperty(ref margenLabel, value); }
    public string UfM2Label { get => ufM2Label; private set => SetProperty(ref ufM2Label, value); }
    public string UfM2SubLabel { get => ufM2SubLabel; private set => SetProperty(ref ufM2SubLabel, value); }
    public string ResumenFooterText { get => resumenFooterText; private set => SetProperty(ref resumenFooterText, value); }
    public string Currency { get => currency; private set => SetProperty(ref currency, value); }
    public string CurrencyColumnHeader { get => currencyColumnHeader; private set => SetProperty(ref currencyColumnHeader, value); }

    public string TotalMoDirectaLabel { get => totalMoDirectaLabel; private set => SetProperty(ref totalMoDirectaLabel, value); }
    public string TotalMoIndirectaLabel { get => totalMoIndirectaLabel; private set => SetProperty(ref totalMoIndirectaLabel, value); }
    public string TotalMoLabel { get => totalMoLabel; private set => SetProperty(ref totalMoLabel, value); }
    public string TotalMoSubLabel { get => totalMoSubLabel; private set => SetProperty(ref totalMoSubLabel, value); }
    public decimal MoDirectaPct { get => moDirectaPct; private set => SetProperty(ref moDirectaPct, value); }
    public decimal MoIndirectaPct { get => moIndirectaPct; private set => SetProperty(ref moIndirectaPct, value); }
    public int TotalMoDotacion { get => totalMoDotacion; private set => SetProperty(ref totalMoDotacion, value); }
    public int TotalMoDirectaDotacion { get => totalMoDirectaDotacion; private set => SetProperty(ref totalMoDirectaDotacion, value); }
    public string TotalMoDirectaDotacionLabel => TotalMoDirectaDotacion > 0 ? $"{TotalMoDirectaDotacion} contratos" : "";
    public string TotalGoLabel { get => totalGoLabel; private set => SetProperty(ref totalGoLabel, value); }
    public string TotalGoUfLabel { get => totalGoUfLabel; private set => SetProperty(ref totalGoUfLabel, value); }
    public string TotalGoUfSubLabel { get => totalGoUfSubLabel; private set => SetProperty(ref totalGoUfSubLabel, value); }
    public string GoComposeHint { get => goComposeHint; private set => SetProperty(ref goComposeHint, value); }
    public string GoFooterSubLabel { get => goFooterSubLabel; private set => SetProperty(ref goFooterSubLabel, value); }
    public string GoCategoriaCountLabel { get => goCategoriaCountLabel; private set => SetProperty(ref goCategoriaCountLabel, value); }
    public string GoItemCountLabel { get => goItemCountLabel; private set => SetProperty(ref goItemCountLabel, value); }
    public bool CanEdit
    {
        get => canEdit;
        private set
        {
            if (SetProperty(ref canEdit, value))
            {
                AddGoItemCommand.RaiseCanExecuteChanged();
                RemoveGoItemCommand.RaiseCanExecuteChanged();
                AddGoCategoriaCommand.RaiseCanExecuteChanged();
                RemoveGoCategoriaCommand.RaiseCanExecuteChanged();
                ResetGoPlantillaCommand.RaiseCanExecuteChanged();
                OpenViaticosCommand.RaiseCanExecuteChanged();
                ResetCoeficientesCommand.RaiseCanExecuteChanged();
                LoadPelCoeficientesCommand.RaiseCanExecuteChanged();
                LoadTipicosCoeficientesCommand.RaiseCanExecuteChanged();
                SaveCostosPresetCommand.RaiseCanExecuteChanged();
                RestoreCostosPresetCommand.RaiseCanExecuteChanged();
                ApplyCoeficientesCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string GastosGeneralesText
    {
        get => gastosGeneralesText;
        set
        {
            if (!SetProperty(ref gastosGeneralesText, value ?? ""))
                return;
            if (!CanEdit || !TryParsePct(value, out var pct))
                return;
            suppressCoefRefresh = true;
            try
            {
                host.Session.ProjectInfo.GastosGeneralesPct = pct;
                host.Session.MarkDirty("project_info");
                RefreshCostOnly();
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        }
    }

    public string UtilidadText
    {
        get => utilidadText;
        set
        {
            if (!SetProperty(ref utilidadText, value ?? ""))
                return;
            if (!CanEdit || !TryParsePct(value, out var pct))
                return;
            suppressCoefRefresh = true;
            try
            {
                host.Session.ProjectInfo.UtilidadPct = pct;
                host.Session.MarkDirty("project_info");
                RefreshCostOnly();
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        }
    }

    public string ValorAgregadoEquiposText
    {
        get => valorAgregadoEquiposText;
        set
        {
            if (!SetProperty(ref valorAgregadoEquiposText, value ?? ""))
                return;
            if (!CanEdit || !TryParsePct(value, out var pct))
                return;
            suppressCoefRefresh = true;
            try
            {
                host.Session.ProjectInfo.ValorAgregadoEquiposPct = pct;
                host.Session.MarkDirty("project_info");
                RefreshCostOnly();
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        }
    }

    public string UtilidadPolicyLabel
    {
        get => utilidadPolicyLabel;
        private set => SetProperty(ref utilidadPolicyLabel, value);
    }

    public string CoefFactorMedioLabel
    {
        get => coefFactorMedioLabel;
        private set => SetProperty(ref coefFactorMedioLabel, value);
    }

    public string CoefActivasLabel
    {
        get => coefActivasLabel;
        private set => SetProperty(ref coefActivasLabel, value);
    }

    public bool CoefAppliedFlash
    {
        get => coefAppliedFlash;
        private set => SetProperty(ref coefAppliedFlash, value);
    }

    public bool PoliticaCostosExcel
    {
        get => politicaCostosExcel;
        set
        {
            if (!SetProperty(ref politicaCostosExcel, value) || !CanEdit)
                return;
            suppressCoefRefresh = true;
            try
            {
                host.Session.ProjectInfo.PoliticaCostosExcel = value;
                host.Session.MarkDirty("project_info");
                UtilidadPolicyLabel = value ? "Utilidades (aditiva Excel)" : "Margen s/ venta";
                RefreshCostOnly();
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        }
    }

    public ObservableCollection<CostoRowVm> CostRows { get; } = new();
    public ObservableCollection<CostoOrigenVm> Origenes { get; } = new();
    public ObservableCollection<CostoFamiliaVm> Familias { get; } = new();
    public ObservableCollection<MoDirectaRowVm> MoDirectaRows { get; } = new();
    public ObservableCollection<MoIndirectaRowVm> MoIndirectaRows { get; } = new();
    public ObservableCollection<GastoOpCategoriaVm> GastosOperacionales { get; } = new();
    public ObservableCollection<GoComposeSegmentVm> GoComposeSegments { get; } = new();
    public ObservableCollection<GoComposeLegendVm> GoComposeLegend { get; } = new();
    public ObservableCollection<CoefFactorVm> CoefFactors { get; } = new();

    public ICommand OpenProjectCommand { get; }
    public ICommand GoToTakeoffCommand => goToTakeoffCommand;
    public ICommand GoToItemizedCommand => goToItemizedCommand;
    public ICommand GoToManoDeObraCommand => goToManoDeObraCommand;
    public ICommand GoToEppCommand => goToEppCommand;
    public ICommand GoToReviewCommand => goToReviewCommand;
    public RelayCommand<GastoOpCategoriaVm> AddGoItemCommand { get; }
    public RelayCommand<GastoOpItemVm> RemoveGoItemCommand { get; }
    public RelayCommand AddGoCategoriaCommand { get; }
    public RelayCommand<GastoOpCategoriaVm> RemoveGoCategoriaCommand { get; }
    public RelayCommand ResetGoPlantillaCommand { get; }
    public RelayCommand<GastoOpCategoriaVm> ToggleGoCategoriaCommand { get; }
    public RelayCommand<GastoOpCategoriaVm> OpenViaticosCommand { get; }
    public RelayCommand ResetCoeficientesCommand { get; }
    public RelayCommand LoadPelCoeficientesCommand { get; }
    public RelayCommand LoadTipicosCoeficientesCommand { get; }
    public RelayCommand SaveCostosPresetCommand { get; }
    public RelayCommand RestoreCostosPresetCommand { get; }
    public RelayCommand ApplyCoeficientesCommand { get; }

    public string CostosPresetHint
    {
        get => costosPresetHint;
        private set => SetProperty(ref costosPresetHint, value);
    }

    public bool HasCostosPreset => host.ProjectWorkspace.HasOpenProject
        && host.Session.ProjectInfo.CostosPreset is { Factores.Count: > 0 };

    private readonly RelayCommand goToTakeoffCommand;
    private readonly RelayCommand goToItemizedCommand;
    private readonly RelayCommand goToManoDeObraCommand;
    private readonly RelayCommand goToEppCommand;
    private readonly RelayCommand goToReviewCommand;

    // ─── Sesión ───────────────────────────────────────────────────────────────

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (suppressGoRefresh || suppressCoefRefresh)
            return;
        // Edición local de GO / política de cierre: no reconstruir toda la pantalla (perdería el
        // foco de edición); RefreshCostOnly() ya se llamó de forma síncrona en el propio commit.
        if (string.Equals(e.Scope, "gastos_operacionales", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Scope, "closing_policy", StringComparison.OrdinalIgnoreCase))
            return;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);
    }

    public void Refresh()
    {
        ShowEmptyState = !host.ProjectWorkspace.HasOpenProject;
        CanEdit = !ShowEmptyState && !host.Session.IsReadOnly;
        goToTakeoffCommand.RaiseCanExecuteChanged();
        goToItemizedCommand.RaiseCanExecuteChanged();
        goToManoDeObraCommand.RaiseCanExecuteChanged();
        goToEppCommand.RaiseCanExecuteChanged();
        goToReviewCommand.RaiseCanExecuteChanged();
        if (ShowEmptyState) return;

        var session = host.Session;
        var project = session.ProjectInfo;
        var data = session.Data;

        var cost = host.Dashboard.BuildCost(data);
        var svc = host.Services.CurrencyConversion;
        var moneda = svc.NormalizeCurrency(project.MonedaPresupuesto);
        var valorUf = project.ValorUfClp > 0 ? project.ValorUfClp : session.ValorUfClp;
        if (valorUf <= 0) valorUf = 37998m;
        currentValorUf = valorUf;
        var tipoCambio = project.TipoCambioUsdClp;

        Currency = moneda;
        CurrencyColumnHeader = $"Valor ({moneda})";

        // ─── Familias (desde catálogo de materiales) ────────────────────────
        var familias = Familias;
        familias.Clear();
        var cubicacion = data.Cubicacion ?? Enumerable.Empty<CubicacionRow>();
        var materiales = data.Materiales ?? Enumerable.Empty<MaterialRow>();
        var cubicacionByCode = cubicacion
            .Where(r => !string.IsNullOrWhiteSpace(r.Codigo))
            .ToLookup(r => r.Codigo, StringComparer.OrdinalIgnoreCase);
        var familiaGroups = materiales
            .Where(m => !string.IsNullOrWhiteSpace(m.Categoria) && !string.IsNullOrWhiteSpace(m.Codigo))
            .GroupBy(m => m.Categoria, StringComparer.OrdinalIgnoreCase);
        foreach (var grupo in familiaGroups)
        {
            var codesInFamily = grupo.Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cubRows = codesInFamily.SelectMany(c => cubicacionByCode[c]);
            var totalClp = cubRows.Sum(r => r.MatClp * r.Cantidad + r.MoClp * r.Cantidad);
            var totalUf = cubRows.Sum(r => r.UfTotal);
            var cantTotal = cubRows.Sum(r => Math.Abs(r.Cantidad));
            familias.Add(new CostoFamiliaVm
            {
                Familia = grupo.Key,
                MaterialesCount = codesInFamily.Count,
                TotalClp = totalClp,
                TotalUf = totalUf,
                TotalLabel = $"$ {totalClp:N0}",
                TotalMonedaLabel = $"{totalUf:N2} UF",
                CountLabel = $"{cantTotal:N2} {grupo.First().Unidad ?? ""}"
            });
        }
        ShowFamilias = familias.Count > 0;

        // ─── MO Dotación (desde datos del proyecto o seed) ──────────────────
        RefreshMoSection(data, valorUf);

        // ─── Gastos Operacionales (editable) ────────────────────────────────
        RefreshGoSection(data, valorUf);

        // ─── Coeficientes de sobreventa ─────────────────────────────────────
        RefreshCoeficientesSection(project);

        CostoDirectoLabel = $"{cost.CostoDirectoUf:N2} UF";
        CostoDirectoSubLabel = svc.FormatMoneyFromUf(cost.CostoDirectoUf, moneda, valorUf, tipoCambio);
        TotalVentaLabel = $"{cost.TotalVentaUf:N2} UF";
        TotalVentaSubLabel = svc.FormatMoneyFromUf(cost.TotalVentaUf, moneda, valorUf, tipoCambio);
        MargenLabel = $"{cost.MargenRealPct:N1} %";
        UfM2Label = $"{cost.UfM2:N2} UF";
        UfM2SubLabel = project.MetrosCuadrados > 0
            ? $"{project.MetrosCuadrados:N0} m² · {svc.FormatMoneyFromUf(cost.UfM2, moneda, valorUf, tipoCambio)} / m²"
            : "Sin m² configurados";

        FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
        RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);

        // Orígenes
        var origenes = Origenes;
        origenes.Clear();
        foreach (var o in cost.OrigenesResumen.OrderByDescending(o => o.TotalUf))
            origenes.Add(new CostoOrigenVm
            {
                Origen = o.Origen,
                Descripcion = DescribeOrigen(o.Origen),
                MaterialesUf = o.MaterialesUf,
                ManoObraUf = o.ManoObraUf,
                EquiposUf = o.EquiposUf,
                TotalUf = o.TotalUf
            });

        ShowOrigenes = origenes.Count > 0;

        ResumenFooterText = $"Costo ejecución {cost.CostoDirectoUf:N2} UF · Total venta {cost.TotalVentaUf:N2} UF · Margen {cost.MargenRealPct:N1}%";
    }

    private static string DescribeOrigen(string origen)
    {
        if (string.IsNullOrWhiteSpace(origen)) return "Origen del costo directo";
        if (origen.Contains("Cubic", StringComparison.OrdinalIgnoreCase)) return "Partidas valorizadas desde takeoff";
        if (origen.Contains("APU", StringComparison.OrdinalIgnoreCase)) return "Complementarios y ajustes de APU";
        if (origen.Contains("OC", StringComparison.OrdinalIgnoreCase) || origen.Contains("Orden", StringComparison.OrdinalIgnoreCase))
            return "Órdenes de cambio aprobadas";
        if (origen.Contains("Global", StringComparison.OrdinalIgnoreCase) || origen.Contains("Herramient", StringComparison.OrdinalIgnoreCase) || origen.Contains("Merma", StringComparison.OrdinalIgnoreCase))
            return "Recargos globales del motor";
        return origen;
    }

    private void RefreshMoSection(ProjectFileData data, decimal valorUf)
    {
        MoDirectaRows.Clear();
        MoIndirectaRows.Clear();

        if (data.MoDirecta != null && data.MoDirecta.Count > 0)
        {
            var clp = 0m;
            foreach (var r in data.MoDirecta)
            {
                var itemTotal = r.Dotacion * r.SueldoMensualClp;
                clp += itemTotal;
            }
            foreach (var r in data.MoDirecta)
            {
                var itemTotal = r.Dotacion * r.SueldoMensualClp;
                MoDirectaRows.Add(new MoDirectaRowVm
                {
                    Cargo = r.Cargo,
                    Dotacion = r.Dotacion,
                    SueldoMensualClp = r.SueldoMensualClp,
                    Pct = clp > 0 ? itemTotal / clp * 100m : 0
                });
            }
        }
        else
        {
            // Sin datos: estructura de cargos con dotación 0 (no inventar personas).
            MoDirectaRows.Add(new MoDirectaRowVm { Cargo = "Maestro eléctrico (M1/M2)", Dotacion = 0, SueldoMensualClp = 750000m, Pct = 0 });
            MoDirectaRows.Add(new MoDirectaRowVm { Cargo = "Ayudante eléctrico (AA/AB)", Dotacion = 0, SueldoMensualClp = 650000m, Pct = 0 });
        }

        if (data.MoIndirecta != null && data.MoIndirecta.Count > 0)
        {
            var clpInd = 0m;
            foreach (var r in data.MoIndirecta)
            {
                var itemTotal = r.Dotacion * r.SueldoMensualClp;
                clpInd += itemTotal;
            }
            foreach (var r in data.MoIndirecta)
            {
                var itemTotal = r.Dotacion * r.SueldoMensualClp;
                MoIndirectaRows.Add(new MoIndirectaRowVm
                {
                    Cargo = r.Cargo,
                    Dotacion = r.Dotacion,
                    SueldoMensualClp = r.SueldoMensualClp,
                    Pct = clpInd > 0 ? itemTotal / clpInd * 100m : 0
                });
            }
        }
        else
        {
            MoIndirectaRows.Add(new MoIndirectaRowVm { Cargo = "Administrador de obra", Dotacion = 0, SueldoMensualClp = 1237500m, Pct = 0 });
            MoIndirectaRows.Add(new MoIndirectaRowVm { Cargo = "Supervisor de obra", Dotacion = 0, SueldoMensualClp = 1466667m, Pct = 0 });
            MoIndirectaRows.Add(new MoIndirectaRowVm { Cargo = "Capataz de obra", Dotacion = 0, SueldoMensualClp = 916667m, Pct = 0 });
            MoIndirectaRows.Add(new MoIndirectaRowVm { Cargo = "Oficina técnica", Dotacion = 0, SueldoMensualClp = 779167m, Pct = 0 });
            MoIndirectaRows.Add(new MoIndirectaRowVm { Cargo = "Encargado de bodega", Dotacion = 0, SueldoMensualClp = 595833m, Pct = 0 });
            MoIndirectaRows.Add(new MoIndirectaRowVm { Cargo = "Prevencionista de riesgos", Dotacion = 0, SueldoMensualClp = 641667m, Pct = 0 });
        }

        var totalDir = MoDirectaRows.Sum(r => r.TotalAnualClp);
        var totalInd = MoIndirectaRows.Sum(r => r.TotalAnualClp);
        var totalMo = totalDir + totalInd;

        TotalMoDirectaLabel = $"$ {totalDir:N0}";
        TotalMoIndirectaLabel = $"$ {totalInd:N0}";
        TotalMoLabel = $"$ {totalMo:N0}";
        TotalMoSubLabel = valorUf > 0 ? $"{totalMo / valorUf:N2} UF" : "";
        MoDirectaPct = totalMo > 0 ? totalDir / totalMo * 100m : 0;
        MoIndirectaPct = totalMo > 0 ? totalInd / totalMo * 100m : 0;
        TotalMoDirectaDotacion = MoDirectaRows.Sum(r => r.Dotacion);
        TotalMoDotacion = TotalMoDirectaDotacion + MoIndirectaRows.Sum(r => r.Dotacion);
        OnPropertyChanged(nameof(TotalMoDirectaDotacionLabel));

        ShowMoSection = true; // Siempre visible: lectura desde MO / plantilla
    }

    private void RefreshGoSection(ProjectFileData data, decimal valorUf)
    {
        GastosOperacionales.Clear();

        // Repara proyectos donde el sync EPP dejó solo 3.5 (u otras categorías faltantes).
        var repaired = GastoOperacionalPlantilla.EnsureComplete(data);

        var source = data.GastosOperacionales;
        var seededFresh = source == null || source.Count == 0;
        var rows = seededFresh ? GastoOperacionalPlantilla.CreateClean() : source!;

        var first = true;
        foreach (var r in rows)
        {
            GastosOperacionales.Add(new GastoOpCategoriaVm(r, valorUf, OnGoChanged, expanded: first));
            first = false;
        }

        var ensuredBus = EnsureTrasladoBusItem();
        var syncedEpp = SyncGoEppFromBudget(data, valorUf);
        UpdateGoTotals();
        ShowGoSection = true;

        if ((seededFresh || repaired || ensuredBus || syncedEpp) && CanEdit)
            PersistGo(markDirty: true);
    }

    /// <summary>Empuja el total EPP del módulo a la categoría GO 3.5.</summary>
    private bool SyncGoEppFromBudget(ProjectFileData data, decimal valorUf)
    {
        var epp = data.Epp;
        if (epp == null || !epp.HasContent)
            return false;

        // Asegura que las cantidades EPP reflejen la MO actual antes de pintar GO.
        var (dotDir, dotInd) = EppBudgetData.ReadMoDotacion(data);
        epp.ApplyMoDotacion(dotDir, dotInd);
        data.Epp = epp;
        EppBudgetData.PushToGoCategory(data, epp);

        var cat = GastosOperacionales.FirstOrDefault(c =>
            string.Equals(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase)
            || c.Concepto.Contains("EPP", StringComparison.OrdinalIgnoreCase));

        if (cat == null)
        {
            var row = data.GastosOperacionales?.FirstOrDefault(c =>
                string.Equals(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase));
            if (row == null)
                return false;
            cat = new GastoOpCategoriaVm(row, valorUf, OnGoChanged);
            // Insertar cerca del orden natural (después de 3.4 si existe).
            var idx = GastosOperacionales.ToList().FindIndex(c =>
                string.Compare(c.Codigo, "3.5", StringComparison.OrdinalIgnoreCase) > 0);
            if (idx < 0) GastosOperacionales.Add(cat);
            else GastosOperacionales.Insert(idx, cat);
            return true;
        }

        var before = cat.MontoClp;
        var goItems = epp.ToGoCategoryItems();
        cat.ReplaceItems(goItems);
        return Math.Abs(cat.MontoClp - before) > 0.5m || cat.Items.Count != goItems.Count;
    }

    /// <summary>
    /// Asegura el ítem de arriendo de bus en 3.3 (traslado colectivo de trabajadores).
    /// </summary>
    private bool EnsureTrasladoBusItem()
    {
        var cat = GastosOperacionales.FirstOrDefault(c =>
            string.Equals(c.Codigo, "3.3", StringComparison.OrdinalIgnoreCase)
            || c.Concepto.Contains("Movilización", StringComparison.OrdinalIgnoreCase));
        if (cat == null)
            return false;

        var exists = cat.Items.Any(i =>
            i.Concepto.Contains("bus", StringComparison.OrdinalIgnoreCase)
            || i.Concepto.Contains("Arriendo / traslado", StringComparison.OrdinalIgnoreCase));
        if (exists)
            return false;

        cat.AddItemSilent("Arriendo / traslado en bus", "viaje");
        return true;
    }

    private void OnGoChanged()
    {
        UpdateGoTotals();
        PersistGo(markDirty: true);
    }

    private void UpdateGoTotals()
    {
        var totalGoClp = GastosOperacionales.Sum(r => r.MontoClp);
        var totalGoUf = GastosOperacionales.Sum(r => r.Uf);
        foreach (var cat in GastosOperacionales)
            cat.Pct = totalGoClp > 0 ? cat.MontoClp / totalGoClp * 100m : 0m;

        TotalGoLabel = $"$ {totalGoClp:N0}";
        TotalGoUfLabel = $"{totalGoUf:N2} UF";
        TotalGoUfSubLabel = $"{totalGoUf:N2} UF · UF $ {currentValorUf:N0}";
        GoCategoriaCountLabel = $"{GastosOperacionales.Count} categorías";
        GoItemCountLabel = GastosOperacionales.Sum(c => c.Items.Count).ToString();
        GoComposeHint = totalGoClp > 0 ? "por categoría" : "sin montos aún";
        GoFooterSubLabel = totalGoClp > 0
            ? "Incluye todas las categorías"
            : "Listo para cargar números reales";

        RebuildComposeBar(totalGoClp);
    }

    private void RebuildComposeBar(decimal totalGoClp)
    {
        GoComposeSegments.Clear();
        GoComposeLegend.Clear();

        if (GastosOperacionales.Count == 0)
            return;

        if (totalGoClp <= 0)
        {
            GoComposeSegments.Add(new GoComposeSegmentVm
            {
                Fill = ParseBrush(GoPalette[0], 0.22),
                WidthUnits = 1000,
            });
            for (var i = 0; i < GastosOperacionales.Count; i++)
            {
                GoComposeLegend.Add(new GoComposeLegendVm
                {
                    Fill = ParseBrush(GoPalette[i % GoPalette.Length]),
                    Codigo = GastosOperacionales[i].Codigo,
                    PctLabel = "—",
                });
            }
            return;
        }

        for (var i = 0; i < GastosOperacionales.Count; i++)
        {
            var cat = GastosOperacionales[i];
            var share = (double)(cat.MontoClp / totalGoClp);
            var units = Math.Max(share * 1000, cat.MontoClp > 0 ? 18 : 0.01);
            var fill = ParseBrush(GoPalette[i % GoPalette.Length]);
            GoComposeSegments.Add(new GoComposeSegmentVm { Fill = fill, WidthUnits = units });
            GoComposeLegend.Add(new GoComposeLegendVm
            {
                Fill = fill,
                Codigo = cat.Codigo,
                PctLabel = $"{cat.Pct:N0}%",
            });
        }
    }

    private static System.Windows.Media.Brush ParseBrush(string hex, double opacity = 1)
    {
        var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        if (opacity < 1)
            c.A = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        var brush = new System.Windows.Media.SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

    private void PersistGo(bool markDirty)
    {
        if (!CanEdit || ShowEmptyState)
            return;

        host.Session.Data.GastosOperacionales = GastosOperacionales.Select(c => c.ToRow()).ToList();
        if (!markDirty)
            return;

        suppressGoRefresh = true;
        try
        {
            host.Session.MarkDirty("gastos_operacionales");
        }
        finally
        {
            suppressGoRefresh = false;
        }
    }

    private void ToggleGoCategoria(GastoOpCategoriaVm? cat) => cat?.ToggleExpanded();

    private void OpenViaticos(GastoOpCategoriaVm? cat)
    {
        if (!CanEdit || cat is not { IsViaticosCategory: true })
            return;

        var data = host.Session.Data;
        var picoDir = data.MoDirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? TotalMoDirectaDotacion;
        var rolesInd = data.MoIndirecta?.Count(r => r.Dotacion > 0) ?? MoIndirectaRows.Count(r => r.Dotacion > 0);
        if (rolesInd <= 0)
            rolesInd = data.MoIndirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? MoIndirectaRows.Sum(r => r.Dotacion);
        var mesesObra = data.MoDirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        if (mesesObra <= 0)
            mesesObra = data.MoIndirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 0;
        if (mesesObra <= 0)
            mesesObra = 12;

        var vm = new ViaticosProfesionalViewModel(currentValorUf, picoDir, rolesInd, mesesObra);
        vm.SyncFromMoRequested = () =>
        {
            var d = host.Session.Data;
            var p = d.MoDirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
            var ind = d.MoIndirecta?.Count(r => r.Dotacion > 0) ?? 0;
            if (ind <= 0)
                ind = d.MoIndirecta?.Sum(r => Math.Max(0, r.Dotacion)) ?? 0;
            var m = d.MoDirecta?.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max() ?? 12;
            if (m <= 0) m = 12;
            vm.ApplyMoSnapshot(p, ind, m, currentValorUf);
        };

        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new ViaticosProfesionalDialog(vm);
        if (owner != null)
            dialog.Owner = owner;

        if (dialog.ShowDialog() != true)
            return;

        var bus = cat.Items.FirstOrDefault(i =>
            i.Concepto.Contains("bus", StringComparison.OrdinalIgnoreCase)
            || i.Concepto.Contains("Arriendo / traslado", StringComparison.OrdinalIgnoreCase));
        var preserveBus = bus?.ToRow();
        cat.ReplaceItems(vm.ResultItems, preserveBus);
    }

    private void AddGoItem(GastoOpCategoriaVm? cat)
    {
        if (!CanEdit || cat == null)
            return;
        cat.AddBlankItem();
    }

    private void RemoveGoItem(GastoOpItemVm? item)
    {
        if (!CanEdit || item == null)
            return;
        var cat = GastosOperacionales.FirstOrDefault(c => c.Items.Contains(item));
        cat?.RemoveItem(item);
    }

    private void AddGoCategoria()
    {
        if (!CanEdit)
            return;
        var n = GastosOperacionales.Count + 1;
        var row = new GastoOperacionalRow
        {
            Codigo = $"3.{n}",
            Concepto = "Nueva categoría",
            Items =
            [
                new GastoOperacionalItemRow { Concepto = "Nuevo ítem", Unidad = "un", Cantidad = 0, PrecioUnitarioClp = 0 }
            ],
        };
        var vm = new GastoOpCategoriaVm(row, currentValorUf, OnGoChanged, expanded: true);
        GastosOperacionales.Add(vm);
        OnGoChanged();
    }

    private void RemoveGoCategoria(GastoOpCategoriaVm? cat)
    {
        if (!CanEdit || cat == null)
            return;
        if (!GastosOperacionales.Remove(cat))
            return;
        OnGoChanged();
    }

    private void ResetGoPlantilla()
    {
        if (!CanEdit)
            return;
        GastosOperacionales.Clear();
        var first = true;
        foreach (var r in GastoOperacionalPlantilla.CreateClean())
        {
            GastosOperacionales.Add(new GastoOpCategoriaVm(r, currentValorUf, OnGoChanged, expanded: first));
            first = false;
        }
        OnGoChanged();
    }

    private void AddRow(ObservableCollection<CostoRowVm> rows, string label, decimal uf, string explanation,
        string moneda, decimal valorUf, decimal tipoCambio, string kind = "regular")
    {
        var svc = host.Services.CurrencyConversion;
        rows.Add(new CostoRowVm
        {
            Concepto = label,
            ValorUf = uf,
            ValorMoneda = uf,
            Explicacion = explanation,
            Kind = kind,
            ValorMonedaLabel = uf > 0
                ? svc.FormatMoneyFromUf(uf, moneda, valorUf, tipoCambio)
                : "$ 0"
        });
    }

    private void EnsureCoefFactors()
    {
        if (CoefFactors.Count > 0)
            return;

        CoefFactors.Add(new CoefFactorVm("Conductores", "Materiales", "→ Conductores", "Excel 1,50×", 1.50m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Canalización", "PVC / EPC", "→ Canalización", "Excel 1,44×", 1.44m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Tableros", "Tableros", "→ Tableros", "Excel 1,10×", 1.10m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Iluminación", "Luminarias", "→ Iluminación", "Excel 1,17×", 1.17m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Equipos", "Equipos", "→ Equipos", "Excel 1,12×", 1.12m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Accesorios", "Varios", "→ Accesorios", "Excel 1,14×", 1.14m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Enchufes y tomacorrientes", "Artefactos", "→ Enchufes", "Excel 1,05×", 1.05m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Protecciones", "Protecciones", "→ Protecciones", "App 1,12×", 1.12m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Puesta a tierra", "Puesta a tierra", "→ PAT", "App 1,20×", 1.20m, CommitFamilyMarkup, CommitFamilyDiscount));
    }

    private void RefreshCoefEffectiveLabels()
    {
        foreach (var f in CoefFactors)
        {
            var m = host.Session.GetFamilyMarkup(f.FamilyKey);
            var d = host.Session.GetFamilyDiscountPct(f.FamilyKey);
            f.SetEffectiveSilent(m, d);
        }
    }

    private void UpdateCostosPresetHint(ProjectInfoRow project)
    {
        var preset = project.CostosPreset;
        if (preset is not { Factores.Count: > 0 })
        {
            CostosPresetHint = "Sin preset guardado en este proyecto";
            OnPropertyChanged(nameof(HasCostosPreset));
            RestoreCostosPresetCommand.RaiseCanExecuteChanged();
            return;
        }

        var when = preset.GuardadoEn > DateTime.MinValue
            ? preset.GuardadoEn.ToString("d", CultureInfo.CurrentCulture)
            : "—";
        CostosPresetHint = $"{preset.Etiqueta} · guardado {when}";
        OnPropertyChanged(nameof(HasCostosPreset));
        RestoreCostosPresetCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCoeficientesSection(ProjectInfoRow project)
    {
        EnsureCoefFactors();
        foreach (var f in CoefFactors)
        {
            f.SetValueSilent(host.Session.GetFamilyMarkup(f.FamilyKey));
            f.SetDiscountSilent(host.Session.GetFamilyDiscountPct(f.FamilyKey));
        }
        RefreshCoefEffectiveLabels();
        UpdateCostosPresetHint(project);

        gastosGeneralesText = project.GastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(GastosGeneralesText));
        utilidadText = project.UtilidadPct.ToString("N1", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(UtilidadText));
        valorAgregadoEquiposText = project.ValorAgregadoEquiposPct.ToString("N1", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(ValorAgregadoEquiposText));

        UtilidadPolicyLabel = string.Equals(project.MarkupMethod, "SobreVenta", StringComparison.OrdinalIgnoreCase)
            && !project.PoliticaCostosExcel
            ? "Margen s/ venta"
            : project.PoliticaCostosExcel
                ? "Utilidades (aditiva Excel)"
                : "Utilidades";

        politicaCostosExcel = project.PoliticaCostosExcel;
        OnPropertyChanged(nameof(PoliticaCostosExcel));

        UpdateCoefChips();
    }

    private void UpdateCoefChips()
    {
        if (CoefFactors.Count == 0)
        {
            CoefFactorMedioLabel = "1,00×";
            CoefActivasLabel = "0/0";
            return;
        }

        decimal sum = 0m;
        int active = 0;
        foreach (var f in CoefFactors)
        {
            var v = host.Session.GetFamilyPresentationFactor(f.FamilyKey);
            sum += v;
            if (v > 1m) active++;
        }

        var avg = sum / CoefFactors.Count;
        CoefFactorMedioLabel = avg.ToString("N2", CultureInfo.CurrentCulture) + "×";
        CoefActivasLabel = $"{active}/{CoefFactors.Count}";
    }

    private void CommitFamilyMarkup(string familyKey, decimal value)
    {
        if (!CanEdit)
            return;
        suppressCoefRefresh = true;
        try
        {
            host.Session.SetFamilyMarkup(familyKey, value);
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void CommitFamilyDiscount(string familyKey, decimal pct)
    {
        if (!CanEdit)
            return;
        suppressCoefRefresh = true;
        try
        {
            host.Session.SetFamilyDiscountPct(familyKey, pct);
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void SaveCostosPreset()
    {
        if (!CanEdit)
            return;
        var project = host.Session.ProjectInfo;
        var preset = new CostosCoeficientesPresetRow
        {
            Etiqueta = string.IsNullOrWhiteSpace(project.Nombre) ? "Preset del proyecto" : $"Preset · {project.Nombre}",
            GuardadoEn = AppClock.Now,
            GastosGeneralesPct = project.GastosGeneralesPct,
            UtilidadPct = project.UtilidadPct,
            ValorAgregadoEquiposPct = project.ValorAgregadoEquiposPct,
            PoliticaCostosExcel = project.PoliticaCostosExcel,
        };
        foreach (var f in CoefFactors)
        {
            preset.Factores[f.FamilyKey] = host.Session.GetFamilyMarkup(f.FamilyKey);
            var d = host.Session.GetFamilyDiscountPct(f.FamilyKey);
            if (d > 0m)
                preset.DescuentosFamiliaPct[f.FamilyKey] = d;
        }

        project.CostosPreset = preset;
        host.Session.MarkDirty("project_info");
        UpdateCostosPresetHint(project);
    }

    private void RestoreCostosPreset()
    {
        if (!CanEdit)
            return;
        var project = host.Session.ProjectInfo;
        var preset = project.CostosPreset;
        if (preset is not { Factores.Count: > 0 })
            return;

        suppressCoefRefresh = true;
        try
        {
            foreach (var f in CoefFactors)
            {
                var markup = preset.Factores.TryGetValue(f.FamilyKey, out var m) ? m : 1m;
                host.Session.SetFamilyMarkup(f.FamilyKey, markup);
                var dsct = preset.DescuentosFamiliaPct.TryGetValue(f.FamilyKey, out var d) ? d : 0m;
                host.Session.SetFamilyDiscountPct(f.FamilyKey, dsct);
                f.SetValueSilent(markup);
                f.SetDiscountSilent(dsct);
            }

            project.GastosGeneralesPct = preset.GastosGeneralesPct;
            project.UtilidadPct = preset.UtilidadPct;
            project.ValorAgregadoEquiposPct = preset.ValorAgregadoEquiposPct;
            project.PoliticaCostosExcel = preset.PoliticaCostosExcel;
            project.MarkupMethod = preset.PoliticaCostosExcel ? "SobreCosto" : project.MarkupMethod;
            host.Session.MarkDirty("project_info");

            gastosGeneralesText = preset.GastosGeneralesPct.ToString("N1", CultureInfo.CurrentCulture);
            OnPropertyChanged(nameof(GastosGeneralesText));
            utilidadText = preset.UtilidadPct.ToString("N1", CultureInfo.CurrentCulture);
            OnPropertyChanged(nameof(UtilidadText));
            valorAgregadoEquiposText = preset.ValorAgregadoEquiposPct.ToString("N1", CultureInfo.CurrentCulture);
            OnPropertyChanged(nameof(ValorAgregadoEquiposText));
            politicaCostosExcel = preset.PoliticaCostosExcel;
            OnPropertyChanged(nameof(PoliticaCostosExcel));
            UtilidadPolicyLabel = preset.PoliticaCostosExcel ? "Utilidades (aditiva Excel)" : "Utilidades";

            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void RefreshCostOnly()
    {
        if (ShowEmptyState)
            return;

        var session = host.Session;
        var project = session.ProjectInfo;
        var data = session.Data;
        var cost = host.Dashboard.BuildCost(data);
        var svc = host.Services.CurrencyConversion;
        var moneda = svc.NormalizeCurrency(project.MonedaPresupuesto);
        var valorUf = project.ValorUfClp > 0 ? project.ValorUfClp : session.ValorUfClp;
        if (valorUf <= 0) valorUf = 37998m;
        var tipoCambio = project.TipoCambioUsdClp;

        CostoDirectoLabel = $"{cost.CostoDirectoUf:N2} UF";
        CostoDirectoSubLabel = svc.FormatMoneyFromUf(cost.CostoDirectoUf, moneda, valorUf, tipoCambio);
        TotalVentaLabel = $"{cost.TotalVentaUf:N2} UF";
        TotalVentaSubLabel = svc.FormatMoneyFromUf(cost.TotalVentaUf, moneda, valorUf, tipoCambio);
        MargenLabel = $"{cost.MargenRealPct:N1} %";
        UfM2Label = $"{cost.UfM2:N2} UF";
        UfM2SubLabel = project.MetrosCuadrados > 0
            ? $"{project.MetrosCuadrados:N0} m² · {svc.FormatMoneyFromUf(cost.UfM2, moneda, valorUf, tipoCambio)} / m²"
            : "Sin m² configurados";

        FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
        RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);
        ResumenFooterText = $"Costo ejecución {cost.CostoDirectoUf:N2} UF · Total venta {cost.TotalVentaUf:N2} UF · Margen {cost.MargenRealPct:N1}%";
    }

    private void RefreshClosingPolicySection(CostBreakdown cost, string moneda, decimal valorUf, decimal tipoCambio)
    {
        var selection = host.Session.Data.ClosingPolicy ?? ClosingPolicySelection.Default();
        ClosingPolicySection.SetSilent(selection);
        var result = ClosingPolicyEngine.Apply(cost, selection);
        var svc = host.Services.CurrencyConversion;
        ClosingPolicySection.SetResult(result, svc.FormatMoneyFromUf(result.VentaFinalClp, moneda, valorUf, tipoCambio));
    }

    private void CommitClosingPolicyType(ClosingPolicyType policy)
    {
        if (!CanEdit) return;
        var selection = host.Session.Data.ClosingPolicy ??= ClosingPolicySelection.Default();
        selection.Policy = policy;
        host.Session.MarkDirty("closing_policy");
        RefreshCostOnly();
    }

    private void CommitClosingPolicyParam(string paramName, decimal value)
    {
        if (!CanEdit) return;
        var selection = host.Session.Data.ClosingPolicy ??= ClosingPolicySelection.Default();
        switch (paramName)
        {
            case nameof(ClosingPolicySelection.TargetPriceClp): selection.TargetPriceClp = value; break;
            case nameof(ClosingPolicySelection.LumpSumMinRiskPct): selection.LumpSumMinRiskPct = value; break;
            case nameof(ClosingPolicySelection.FeeClp): selection.FeeClp = value; break;
            case nameof(ClosingPolicySelection.FeePct): selection.FeePct = value; break;
            case nameof(ClosingPolicySelection.TargetCostClp): selection.TargetCostClp = value; break;
            case nameof(ClosingPolicySelection.GainsharePct): selection.GainsharePct = value; break;
            case nameof(ClosingPolicySelection.GainshareCapClp): selection.GainshareCapClp = value; break;
        }
        host.Session.MarkDirty("closing_policy");
        RefreshCostOnly();
    }

    private void FillCostRows(
        ObservableCollection<CostoRowVm> rows,
        CostBreakdown cost,
        ProjectInfoRow project,
        string moneda,
        decimal valorUf,
        decimal tipoCambio)
    {
        rows.Clear();
        var excel = project.PoliticaCostosExcel;

        AddRow(rows, "Mano de obra", cost.ManoObraUf,
            excel ? "MO efectiva (módulo o cubicación/APU)" : "Suma de mano de obra valorizada desde APU",
            moneda, valorUf, tipoCambio);
        AddRow(rows, "Materiales", cost.MaterialesUf, "Suma de materiales valorizados desde cubicación", moneda, valorUf, tipoCambio);
        AddRow(rows, "Equipos", cost.EquiposUf, "Equipos desde APU / costos complementarios", moneda, valorUf, tipoCambio);
        if (excel || cost.GastosOperacionalesUf > 0m)
            AddRow(rows, "Gastos operacionales", cost.GastosOperacionalesUf, "GO 3.1–3.6 del módulo Costos", moneda, valorUf, tipoCambio);
        AddRow(rows, "Subcontratos", cost.SubcontratosUf, "Subcontratos desde APU", moneda, valorUf, tipoCambio);
        AddRow(rows, "Herramientas", cost.HerramientasUf, "Herramientas cuando aplica", moneda, valorUf, tipoCambio);
        AddRow(rows, "Mermas", cost.MermasUf, "Mermas cuando aplica", moneda, valorUf, tipoCambio);
        AddRow(rows, "Transporte", cost.TransporteUf, "Transporte desde APU", moneda, valorUf, tipoCambio);
        AddRow(rows, "Órdenes de cambio", cost.OrdenesCambioUf, "OC aprobadas — separadas de subcontratos", moneda, valorUf, tipoCambio);

        AddRow(rows, excel ? "Costo de ejecución" : "Costo directo", cost.CostoDirectoUf,
            excel ? "Mat + MO + Equipos + GO + complementarios" : "Costo base del presupuesto",
            moneda, valorUf, tipoCambio, "total");

        AddRow(rows, "Riesgo", cost.RiesgoUf, "Imprevistos / riesgo comercial", moneda, valorUf, tipoCambio);

        if (excel)
        {
            AddRow(rows, $"Gastos generales ({project.GastosGeneralesPct:N1}%)", cost.GastosGeneralesUf,
                "GG sobre Mat + MO + GO (sin equipos) · Excel COSTOS", moneda, valorUf, tipoCambio, "coef");
            AddRow(rows, $"Utilidades ({project.UtilidadPct:N1}%)", cost.UtilidadAditivaUf,
                "Utilidad aditiva sobre Mat + MO + GO · Excel COSTOS", moneda, valorUf, tipoCambio, "coef");
            AddRow(rows, $"VA sobre equipos ({project.ValorAgregadoEquiposPct:N1}%)", cost.ValorAgregadoEquiposUf,
                "Valor agregado solo sobre equipos · Excel COSTOS", moneda, valorUf, tipoCambio, "coef");
            AddRow(rows, "Costo total proyecto", cost.CostoProyectoUf,
                "Ejecución + GG + UT + VA", moneda, valorUf, tipoCambio, "subtotal");
            AddRow(rows, "Diferencia por factores", cost.DiferenciaFactoresUf,
                "Venta − costo proyecto (aporte de los × de presentación)", moneda, valorUf, tipoCambio, "coef");
        }
        else
        {
            AddRow(rows, $"Gastos generales ({project.GastosGeneralesPct:N1}%)", cost.GastosGeneralesUf,
                "GG sobre costo directo según política", moneda, valorUf, tipoCambio, "coef");
            if (cost.ValorAgregadoEquiposUf > 0m || project.ValorAgregadoEquiposPct > 0m)
                AddRow(rows, $"VA sobre equipos ({project.ValorAgregadoEquiposPct:N1}%)", cost.ValorAgregadoEquiposUf,
                    "Valor agregado solo sobre equipos", moneda, valorUf, tipoCambio, "coef");
            AddRow(rows, "Subtotal", cost.SubtotalUf, "Costo directo + riesgo + GG + VA", moneda, valorUf, tipoCambio, "subtotal");
            var utilidadLabel = string.Equals(project.MarkupMethod, "SobreVenta", StringComparison.OrdinalIgnoreCase)
                ? $"Margen s/ venta ({project.UtilidadPct:N1}%)"
                : $"Utilidad ({project.UtilidadPct:N1}%)";
            AddRow(rows, utilidadLabel, cost.UtilidadUf, "Utilidad / margen según política comercial", moneda, valorUf, tipoCambio, "coef");
        }

        AddRow(rows, "Descuento", cost.DescuentoUf, "Descuento comercial", moneda, valorUf, tipoCambio);
        AddRow(rows, "Total venta neto", cost.TotalVentaNetoUf, "Total antes de IVA", moneda, valorUf, tipoCambio, "subtotal");
        AddRow(rows, "IVA", cost.IvaUf, "IVA incluido en total", moneda, valorUf, tipoCambio);
        AddRow(rows, "TOTAL VENTA", cost.TotalVentaUf, "Neto + IVA", moneda, valorUf, tipoCambio, "grand");
        if (project.MetrosCuadrados > 0)
            AddRow(rows, $"UF / m² ({project.MetrosCuadrados:N0} m²)", cost.UfM2, "Total venta / m²", moneda, valorUf, tipoCambio);
    }

    private void ResetCoeficientes()
    {
        if (!CanEdit)
            return;
        suppressCoefRefresh = true;
        try
        {
            foreach (var f in CoefFactors)
            {
                host.Session.SetFamilyMarkup(f.FamilyKey, 1.00m);
                host.Session.SetFamilyDiscountPct(f.FamilyKey, 0m);
                f.SetValueSilent(1.00m);
                f.SetDiscountSilent(0m);
            }
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void LoadPel029Coeficientes() =>
        ApplyCoefPreset(ExcelCostosBridge.PresetFactoresPel029, fallback: 1.30m);

    private void LoadTipicosCoeficientes() =>
        ApplyCoefPreset(ExcelCostosBridge.PresetFactoresTipicos, fallback: 1.14m);

    private void ApplyCoefPreset(IReadOnlyDictionary<string, decimal> preset, decimal fallback)
    {
        if (!CanEdit)
            return;
        suppressCoefRefresh = true;
        try
        {
            foreach (var f in CoefFactors)
            {
                var v = preset.TryGetValue(f.FamilyKey, out var x) ? x : fallback;
                host.Session.SetFamilyMarkup(f.FamilyKey, v);
                f.SetValueSilent(v);
            }

            ExcelCostosBridge.ApplyPoliticaExcelPel029(host.Session.ProjectInfo);
            host.Session.MarkDirty("project_info");

            gastosGeneralesText = "5,0";
            OnPropertyChanged(nameof(GastosGeneralesText));
            utilidadText = "8,0";
            OnPropertyChanged(nameof(UtilidadText));
            valorAgregadoEquiposText = "3,0";
            OnPropertyChanged(nameof(ValorAgregadoEquiposText));
            politicaCostosExcel = true;
            OnPropertyChanged(nameof(PoliticaCostosExcel));
            UtilidadPolicyLabel = "Utilidades (aditiva Excel)";
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void ApplyCoeficientes()
    {
        if (CanEdit && !host.Session.ProjectInfo.PoliticaCostosExcel)
        {
            host.Session.ProjectInfo.PoliticaCostosExcel = true;
            host.Session.MarkDirty("project_info");
            politicaCostosExcel = true;
            OnPropertyChanged(nameof(PoliticaCostosExcel));
            UtilidadPolicyLabel = "Utilidades (aditiva Excel)";
        }

        RefreshCostOnly();
        CoefAppliedFlash = true;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(700)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CoefAppliedFlash = false;
        };
        timer.Start();
    }

    private static bool TryParsePct(string? text, out decimal pct)
    {
        pct = 0m;
        var normalized = (text ?? "").Replace(",", ".");
        if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
            return false;
        pct = Math.Clamp(v, 0m, 100m);
        return true;
    }
}
