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
    public CostoViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Dashboard, host.Services.CurrencyConversion, host.Audit) { }

    public CostoViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, DashboardPresenter dashboard, ICurrencyConversionService currencySvc, ProjectAuditRecorder? audit = null) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.dashboard = dashboard;
        this.currencySvc = currencySvc;
        this.audit = audit;
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
        AddNegotiatedDiscountCommand = new RelayCommand(AddNegotiatedDiscount, () => CanEdit);
        RemoveNegotiatedDiscountCommand = new RelayCommand<NegotiatedDiscountVm>(RemoveNegotiatedDiscount, _ => CanEdit);
        AddStaffingRoleCommand = new RelayCommand(AddStaffingRole, () => CanEdit);
        RemoveStaffingRoleCommand = new RelayCommand<StaffingRoleVm>(RemoveStaffingRole, _ => CanEdit);
        ClosingPolicySection = new ClosingPolicySectionVm(CommitClosingPolicyType, CommitClosingPolicyParam);
        NegotiationSimulator = new CommercialNegotiationVm(ApplyNegotiatedTargetPrice, () => CanEdit);
        EnsureCoefFactors();
        Refresh();
    }

    // ─── Propiedades ──────────────────────────────────────────────────────────

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set => SetProperty(ref showEmptyState, value);
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetProperty(ref isLoading, value);
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
                AddNegotiatedDiscountCommand.RaiseCanExecuteChanged();
                RemoveNegotiatedDiscountCommand.RaiseCanExecuteChanged();
                AddStaffingRoleCommand.RaiseCanExecuteChanged();
                RemoveStaffingRoleCommand.RaiseCanExecuteChanged();
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
                session.ProjectInfo.GastosGeneralesPct = pct;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
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
                session.ProjectInfo.UtilidadPct = pct;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
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
                session.ProjectInfo.ValorAgregadoEquiposPct = pct;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
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
                session.ProjectInfo.PoliticaCostosExcel = value;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                UtilidadPolicyLabel = value ? "Utilidades (aditiva Excel)" : "Margen s/ venta";
                RefreshCostOnly();
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        }
    }

    /// <summary>
    /// C3: fuerza la venta línea a línea por factores de presentación por familia. La política
    /// Excel COSTOS ya lo implica; este switch permite activarlo también con markup global.
    /// </summary>
    public bool UsarFactoresPresentacionFamilia
    {
        get => usarFactoresPresentacionFamilia;
        set
        {
            if (!SetProperty(ref usarFactoresPresentacionFamilia, value) || !CanEdit)
                return;
            suppressCoefRefresh = true;
            try
            {
                session.ProjectInfo.UsarFactoresPresentacionFamilia = value;
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                RefreshCostOnly();
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        }
    }
    private bool usarFactoresPresentacionFamilia;

    public ObservableCollection<CostoRowVm> CostRows { get; } = new();
    public ObservableCollection<CostoOrigenVm> Origenes { get; } = new();
    public ObservableCollection<CostoFamiliaVm> Familias { get; } = new();
    public ObservableCollection<MoDirectaRowVm> MoDirectaRows { get; } = new();
    public ObservableCollection<MoIndirectaRowVm> MoIndirectaRows { get; } = new();
    public ObservableCollection<GastoOpCategoriaVm> GastosOperacionales { get; } = new();
    public ObservableCollection<GoComposeSegmentVm> GoComposeSegments { get; } = new();
    public ObservableCollection<GoComposeLegendVm> GoComposeLegend { get; } = new();
    public ObservableCollection<CoefFactorVm> CoefFactors { get; } = new();
    public ObservableCollection<NegotiatedDiscountVm> NegotiatedDiscounts { get; } = new();
    public ObservableCollection<StaffingRoleVm> StaffingPlan { get; } = new();

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
    public RelayCommand AddNegotiatedDiscountCommand { get; }
    public RelayCommand<NegotiatedDiscountVm> RemoveNegotiatedDiscountCommand { get; }
    public RelayCommand AddStaffingRoleCommand { get; }
    public RelayCommand<StaffingRoleVm> RemoveStaffingRoleCommand { get; }

    public string TotalStaffingPlanLabel
    {
        get => totalStaffingPlanLabel;
        private set => SetProperty(ref totalStaffingPlanLabel, value);
    }

    public string CostosPresetHint
    {
        get => costosPresetHint;
        private set => SetProperty(ref costosPresetHint, value);
    }

    public bool HasCostosPreset => workspace.HasOpenProject
        && session.ProjectInfo.CostosPreset is { Factores.Count: > 0 };

    private readonly RelayCommand goToTakeoffCommand;
    private readonly RelayCommand goToItemizedCommand;
    private readonly RelayCommand goToManoDeObraCommand;
    private readonly RelayCommand goToEppCommand;
    private readonly RelayCommand goToReviewCommand;

    // ─── Sesión ───────────────────────────────────────────────────────────────

    protected override void OnSessionChanged(object? sender, ProjectSessionChangedEventArgs e)
    {
        if (suppressGoRefresh || suppressCoefRefresh || suppressNegotiatedDiscountsRefresh || suppressStaffingPlanRefresh)
            return;
        // Edición local de GO / política de cierre / descuentos negociados / dotación: no
        // reconstruir toda la pantalla (perdería el foco de edición); RefreshCostOnly() ya se
        // llamó de forma síncrona en el propio commit.
        if (string.Equals(e.Scope, "gastos_operacionales", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Scope, "closing_policy", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Scope, "negotiated_discounts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.Scope, "staffing_plan", StringComparison.OrdinalIgnoreCase))
            return;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(Refresh);
    }

    public void Refresh()
    {
        IsLoading = true;
        try
        {
        ShowEmptyState = !workspace.HasOpenProject;
        CanEdit = !ShowEmptyState && !session.IsReadOnly;
        goToTakeoffCommand.RaiseCanExecuteChanged();
        goToItemizedCommand.RaiseCanExecuteChanged();
        goToManoDeObraCommand.RaiseCanExecuteChanged();
        goToEppCommand.RaiseCanExecuteChanged();
        goToReviewCommand.RaiseCanExecuteChanged();
        AddNegotiatedDiscountCommand.RaiseCanExecuteChanged();
        RemoveNegotiatedDiscountCommand.RaiseCanExecuteChanged();
        AddStaffingRoleCommand.RaiseCanExecuteChanged();
        RemoveStaffingRoleCommand.RaiseCanExecuteChanged();
        if (ShowEmptyState) return;

        var project = session.ProjectInfo;
        var data = session.Data;

        var cost = dashboard.BuildCost(data);
        var svc = currencySvc;
        var moneda = svc.NormalizeCurrency(project.MonedaPresupuesto);
        var valorUf = project.ValorUfClp > 0 ? project.ValorUfClp : session.ValorUfClp;
        if (valorUf <= 0) valorUf = PriceConversion.DefaultValorUfClp;
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
                TotalLabel = CurrencyFormat.Clp(totalClp),
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

        // ─── Descuentos negociados y dotación indirecta ─────────────────────
        RefreshNegotiatedDiscountsSection(data);
        RefreshStaffingPlanSection(data);

        UpdateSummaryKpis(cost, project, valorUf);

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
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string DescribeOrigen(string origen) => CostoOrigenDescriptor.Describe(origen);

    private void RefreshMoSection(ProjectFileData data, decimal valorUf)
    {
        MoDirectaRows.Clear();
        MoIndirectaRows.Clear();

        var projection = CostoMoLaborProjectionBuilder.Build(data.MoDirecta, data.MoIndirecta);
        foreach (var line in projection.Directa)
        {
            MoDirectaRows.Add(new MoDirectaRowVm
            {
                Cargo = line.Cargo,
                Dotacion = line.Dotacion,
                SueldoMensualClp = line.SueldoMensualClp,
                Pct = line.Pct
            });
        }

        foreach (var line in projection.Indirecta)
        {
            MoIndirectaRows.Add(new MoIndirectaRowVm
            {
                Cargo = line.Cargo,
                Dotacion = line.Dotacion,
                SueldoMensualClp = line.SueldoMensualClp,
                Pct = line.Pct
            });
        }

        var totals = projection.Totals;
        TotalMoDirectaLabel = CurrencyFormat.Clp(totals.DirectaClp);
        TotalMoIndirectaLabel = CurrencyFormat.Clp(totals.IndirectaClp);
        TotalMoLabel = CurrencyFormat.Clp(totals.TotalClp);
        TotalMoSubLabel = valorUf > 0 ? $"{totals.TotalClp / valorUf:N2} UF" : "";
        MoDirectaPct = totals.DirectaPct;
        MoIndirectaPct = totals.IndirectaPct;
        TotalMoDirectaDotacion = totals.DirectaDotacion;
        TotalMoDotacion = totals.TotalDotacion;
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
            GastosOperacionales.Add(new GastoOpCategoriaVm(r, valorUf, OnGoChanged, expanded: first, canEdit: () => CanEdit));
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
            cat = new GastoOpCategoriaVm(row, valorUf, OnGoChanged, canEdit: () => CanEdit);
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
            GastoOperacionalCategoryMaintenance.IsMovilizacionCategory(c.Codigo, c.Concepto));
        if (cat == null)
            return false;

        if (GastoOperacionalCategoryMaintenance.HasTrasladoBusItem(cat.Items.Select(i => i.Concepto)))
            return false;

        cat.AddItemSilent(
            GastoOperacionalCategoryMaintenance.TrasladoBusConcepto,
            GastoOperacionalCategoryMaintenance.TrasladoBusUnidad);
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

        TotalGoLabel = CurrencyFormat.Clp(totalGoClp);
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

        var shares = GastosOperacionales
            .Select(c => new CostoGoCategoryShare(c.Codigo, c.MontoClp, c.Pct))
            .ToList();
        var projection = CostoGoComposeProjectionBuilder.Build(shares, totalGoClp);

        foreach (var seg in projection.Segments)
        {
            GoComposeSegments.Add(new GoComposeSegmentVm
            {
                Fill = ParseBrush(GoPalette[seg.PaletteIndex % GoPalette.Length], seg.FillOpacity),
                WidthUnits = seg.WidthUnits,
            });
        }

        foreach (var item in projection.Legend)
        {
            GoComposeLegend.Add(new GoComposeLegendVm
            {
                Fill = ParseBrush(GoPalette[item.PaletteIndex % GoPalette.Length]),
                Codigo = item.Codigo,
                PctLabel = item.PctLabel,
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

        session.Data.GastosOperacionales = GastosOperacionales.Select(c => c.ToRow()).ToList();
        if (!markDirty)
            return;

        suppressGoRefresh = true;
        try
        {
            session.MarkDirty(ProjectDirtyScope.GastosOperacionales);
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

        var data = session.Data;
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
            var d = session.Data;
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
        var vm = new GastoOpCategoriaVm(row, currentValorUf, OnGoChanged, expanded: true, canEdit: () => CanEdit);
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
            GastosOperacionales.Add(new GastoOpCategoriaVm(r, currentValorUf, OnGoChanged, expanded: first, canEdit: () => CanEdit));
            first = false;
        }
        OnGoChanged();
    }

    private void AddRow(ObservableCollection<CostoRowVm> rows, string label, decimal clp, string explanation,
        string moneda, decimal valorUf, decimal tipoCambio, string kind = "regular")
    {
        var svc = currencySvc;
        decimal uf = PriceConversion.ClpToUf(clp, valorUf);
        rows.Add(new CostoRowVm
        {
            Concepto = label,
            ValorUf = uf,
            ValorMoneda = clp,
            Explicacion = explanation,
            Kind = kind,
            ValorMonedaLabel = clp != 0m
                ? svc.FormatMoneyFromClp(clp, moneda, valorUf, tipoCambio)
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
            var m = session.GetFamilyMarkup(f.FamilyKey);
            var d = session.GetFamilyDiscountPct(f.FamilyKey);
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
            f.SetValueSilent(session.GetFamilyMarkup(f.FamilyKey));
            f.SetDiscountSilent(session.GetFamilyDiscountPct(f.FamilyKey));
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

        usarFactoresPresentacionFamilia = project.UsarFactoresPresentacionFamilia;
        OnPropertyChanged(nameof(UsarFactoresPresentacionFamilia));

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
            var v = session.GetFamilyPresentationFactor(f.FamilyKey);
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
            session.SetFamilyMarkup(familyKey, value);
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
            session.SetFamilyDiscountPct(familyKey, pct);
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    // ─── Descuentos negociados por equipo/material específico ──────────────────

    private void RefreshNegotiatedDiscountsSection(ProjectFileData data)
    {
        NegotiatedDiscounts.Clear();
        foreach (var row in data.NegotiatedDiscounts ?? new List<NegotiatedDiscountRow>())
            NegotiatedDiscounts.Add(new NegotiatedDiscountVm(row, OnNegotiatedDiscountsChanged, () => CanEdit));
    }

    private void OnNegotiatedDiscountsChanged() => PersistNegotiatedDiscounts(markDirty: true);

    private void PersistNegotiatedDiscounts(bool markDirty)
    {
        if (!CanEdit || ShowEmptyState)
            return;

        session.Data.NegotiatedDiscounts = NegotiatedDiscounts.Select(x => x.ToRow()).ToList();
        if (!markDirty)
            return;

        suppressNegotiatedDiscountsRefresh = true;
        try
        {
            session.MarkDirty(ProjectDirtyScope.NegotiatedDiscounts);
            RefreshCostOnly();
        }
        finally
        {
            suppressNegotiatedDiscountsRefresh = false;
        }
    }

    private void AddNegotiatedDiscount()
    {
        if (!CanEdit)
            return;
        NegotiatedDiscounts.Add(new NegotiatedDiscountVm(new NegotiatedDiscountRow(), OnNegotiatedDiscountsChanged, () => CanEdit));
        PersistNegotiatedDiscounts(markDirty: true);
    }

    private void RemoveNegotiatedDiscount(NegotiatedDiscountVm? item)
    {
        if (!CanEdit || item == null)
            return;
        NegotiatedDiscounts.Remove(item);
        PersistNegotiatedDiscounts(markDirty: true);
    }

    // ─── Dotación indirecta por rol (se suma a Gastos Operacionales) ───────────

    private void RefreshStaffingPlanSection(ProjectFileData data)
    {
        StaffingPlan.Clear();
        foreach (var row in data.StaffingPlan ?? new List<StaffingRoleRow>())
            StaffingPlan.Add(new StaffingRoleVm(row, OnStaffingPlanChanged, () => CanEdit));
        UpdateStaffingPlanTotal();
    }

    private void UpdateStaffingPlanTotal()
    {
        TotalStaffingPlanLabel = CurrencyFormat.Clp(StaffingPlan.Sum(x => x.TotalClp));
    }

    private void OnStaffingPlanChanged()
    {
        UpdateStaffingPlanTotal();
        PersistStaffingPlan(markDirty: true);
    }

    private void PersistStaffingPlan(bool markDirty)
    {
        if (!CanEdit || ShowEmptyState)
            return;

        session.Data.StaffingPlan = StaffingPlan.Select(x => x.ToRow()).ToList();
        if (!markDirty)
            return;

        suppressStaffingPlanRefresh = true;
        try
        {
            session.MarkDirty(ProjectDirtyScope.StaffingPlan);
            RefreshCostOnly();
        }
        finally
        {
            suppressStaffingPlanRefresh = false;
        }
    }

    private void AddStaffingRole()
    {
        if (!CanEdit)
            return;
        StaffingPlan.Add(new StaffingRoleVm(new StaffingRoleRow { DedicacionPct = 100m, Meses = 1 }, OnStaffingPlanChanged, () => CanEdit));
        PersistStaffingPlan(markDirty: true);
    }

    private void RemoveStaffingRole(StaffingRoleVm? item)
    {
        if (!CanEdit || item == null)
            return;
        StaffingPlan.Remove(item);
        UpdateStaffingPlanTotal();
        PersistStaffingPlan(markDirty: true);
    }

    private void SaveCostosPreset()
    {
        if (!CanEdit)
            return;
        var project = session.ProjectInfo;
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
            preset.Factores[f.FamilyKey] = session.GetFamilyMarkup(f.FamilyKey);
            var d = session.GetFamilyDiscountPct(f.FamilyKey);
            if (d > 0m)
                preset.DescuentosFamiliaPct[f.FamilyKey] = d;
        }

        project.CostosPreset = preset;
        session.MarkDirty(ProjectDirtyScope.ProjectInfo);
        UpdateCostosPresetHint(project);
    }

    private void RestoreCostosPreset()
    {
        if (!CanEdit)
            return;
        var project = session.ProjectInfo;
        var preset = project.CostosPreset;
        if (preset is not { Factores.Count: > 0 })
            return;

        suppressCoefRefresh = true;
        try
        {
            foreach (var f in CoefFactors)
            {
                var markup = preset.Factores.TryGetValue(f.FamilyKey, out var m) ? m : 1m;
                session.SetFamilyMarkup(f.FamilyKey, markup);
                var dsct = preset.DescuentosFamiliaPct.TryGetValue(f.FamilyKey, out var d) ? d : 0m;
                session.SetFamilyDiscountPct(f.FamilyKey, dsct);
                f.SetValueSilent(markup);
                f.SetDiscountSilent(dsct);
            }

            project.GastosGeneralesPct = preset.GastosGeneralesPct;
            project.UtilidadPct = preset.UtilidadPct;
            project.ValorAgregadoEquiposPct = preset.ValorAgregadoEquiposPct;
            project.PoliticaCostosExcel = preset.PoliticaCostosExcel;
            project.MarkupMethod = preset.PoliticaCostosExcel ? "SobreCosto" : project.MarkupMethod;
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);

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

        var project = session.ProjectInfo;
        var data = session.Data;
        var cost = dashboard.BuildCost(data);
        var svc = currencySvc;
        var moneda = svc.NormalizeCurrency(project.MonedaPresupuesto);
        var valorUf = project.ValorUfClp > 0 ? project.ValorUfClp : session.ValorUfClp;
        if (valorUf <= 0) valorUf = PriceConversion.DefaultValorUfClp;
        var tipoCambio = project.TipoCambioUsdClp;

        UpdateSummaryKpis(cost, project, valorUf);
        FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
        RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);
    }

    private void UpdateSummaryKpis(CostBreakdown cost, ProjectInfoRow project, decimal valorUf)
    {
        decimal directoClp = cost.CostoDirectoClp;
        decimal directoUf = PriceConversion.ClpToUf(directoClp, valorUf);
        decimal ventaClp = cost.TotalVentaClp;
        decimal ventaUf = PriceConversion.ClpToUf(ventaClp, valorUf);
        decimal m2Clp = project.MetrosCuadrados > 0 ? ventaClp / project.MetrosCuadrados : 0m;

        CostoDirectoLabel = $"$ {directoClp:N0}";
        CostoDirectoSubLabel = $"{directoUf:N2} UF";
        TotalVentaLabel = $"$ {ventaClp:N0}";
        TotalVentaSubLabel = $"{ventaUf:N2} UF";
        MargenLabel = $"{cost.MargenRealPct:N1} %";
        UfM2Label = $"{PriceConversion.ClpToUf(m2Clp, valorUf):N2} UF/m²";
        UfM2SubLabel = project.MetrosCuadrados > 0 ? $"{project.MetrosCuadrados:N0} m² · $ {m2Clp:N0} / m²" : "Sin m² configurados";
        ResumenFooterText = $"Costo ejecución $ {directoClp:N0} ({directoUf:N2} UF) · Total venta $ {ventaClp:N0} ({ventaUf:N2} UF) · Margen {cost.MargenRealPct:N1}%";
        NegotiationSimulator.UpdateBase(directoClp, ventaClp, valorUf);
    }

    private void RefreshClosingPolicySection(CostBreakdown cost, string moneda, decimal valorUf, decimal tipoCambio)
    {
        var selection = session.Data.ClosingPolicy ?? ClosingPolicySelection.Default();
        ClosingPolicySection.SetSilent(selection);
        // El motor ya aplicó la política sobre el breakdown; leer su resultado en vez de recalcular.
        var result = cost.ClosingPolicyResult ?? ClosingPolicyEngine.Apply(cost, selection);
        var svc = currencySvc;
        ClosingPolicySection.SetResult(result, svc.FormatMoneyFromClp(result.VentaFinalClp, moneda, valorUf, tipoCambio));
        ClosingPolicySection.SetSafeguards(selection, cost.CostoDirectoClp);
    }

    private static readonly HashSet<string> AbsoluteAmountParams = new(StringComparer.Ordinal)
    {
        nameof(ClosingPolicySelection.TargetPriceClp), nameof(ClosingPolicySelection.FeeClp),
        nameof(ClosingPolicySelection.TargetCostClp), nameof(ClosingPolicySelection.GainshareCapClp)
    };

    private void CommitClosingPolicyType(ClosingPolicyType policy)
    {
        if (!CanEdit) return;
        var selection = session.Data.ClosingPolicy ??= ClosingPolicySelection.Default();
        if (selection.Policy == policy) return;
        audit?.RecordLifecycle("Política de cierre", $"{selection.Policy} → {policy}");
        selection.Policy = policy;
        session.MarkDirty(ProjectDirtyScope.ClosingPolicy);
        RefreshCostOnly();
    }

    private void CommitClosingPolicyParam(string paramName, decimal value)
    {
        if (!CanEdit) return;
        var selection = session.Data.ClosingPolicy ??= ClosingPolicySelection.Default();
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
        if (AbsoluteAmountParams.Contains(paramName))
        {
            selection.ParamsStampedAt = AppClock.Now;
            try { selection.ParamsStampedCostoEjecucionClp = dashboard.BuildCost(session.Data).CostoDirectoUf; }
            catch { /* breakdown no disponible: se sella sin costo de referencia */ }
        }
        session.MarkDirty(ProjectDirtyScope.ClosingPolicy);
        RefreshCostOnly();
    }

    private void ApplyNegotiatedTargetPrice(decimal targetPrice) { CommitClosingPolicyType(ClosingPolicyType.TargetPrice); CommitClosingPolicyParam(nameof(ClosingPolicySelection.TargetPriceClp), targetPrice); }

    private void FillCostRows(
        ObservableCollection<CostoRowVm> rows,
        CostBreakdown cost,
        ProjectInfoRow project,
        string moneda,
        decimal valorUf,
        decimal tipoCambio)
    {
        rows.Clear();
        foreach (var line in CostoBreakdownLineBuilder.BuildLines(cost, project))
            AddRow(rows, line.Label, line.AmountUf, line.Explanation, moneda, valorUf, tipoCambio, line.Kind);
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
                session.SetFamilyMarkup(f.FamilyKey, 1.00m);
                session.SetFamilyDiscountPct(f.FamilyKey, 0m);
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
                session.SetFamilyMarkup(f.FamilyKey, v);
                f.SetValueSilent(v);
            }

            ExcelCostosBridge.ApplyPoliticaExcelPel029(session.ProjectInfo);
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);

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
        if (CanEdit && !session.ProjectInfo.PoliticaCostosExcel)
        {
            session.ProjectInfo.PoliticaCostosExcel = true;
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
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
        pct = ClampPct(v);
        return true;
    }
}
