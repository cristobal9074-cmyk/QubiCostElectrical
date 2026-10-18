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
    private readonly IPriceBankStore? priceBank;

    public CostoViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Dashboard, host.Services.CurrencyConversion, host.Audit, host.PriceBankStore, host.UndoService) { }

    public CostoViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService workspace, DashboardPresenter dashboard, ICurrencyConversionService currencySvc, ProjectAuditRecorder? audit = null, IPriceBankStore? priceBank = null, WpfUndoService? undo = null) : base(session)
    {
        this.shell = shell;
        this.session = session;
        this.workspace = workspace;
        this.dashboard = dashboard;
        this.currencySvc = currencySvc;
        this.audit = audit;
        this.priceBank = priceBank;
        this.undo = undo;
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
        ToggleCostGroupCommand = new RelayCommand<CostoGrupoVm>(ToggleCostGroup);
        ToggleAnalisisCommand = new RelayCommand(ToggleAnalisis);
        ToggleFerreteriaDetalleCommand = new RelayCommand(ToggleFerreteriaDetalle);
        AbrirCatalogoFamiliasCommand = new RelayCommand<CoefFactorVm>(AbrirCatalogoFamilias);
        ElegirFamiliaPrecioCommand = new RelayCommand<PrecioFamiliaPickVm>(ElegirFamiliaPrecio);
        AbrirGestionFamiliasCommand = new RelayCommand(AbrirGestionFamilias, () => CanEdit);
        OpenViaticosCommand = new RelayCommand<GastoOpCategoriaVm>(OpenViaticos, c => CanEdit && c is { IsViaticosCategory: true });
        OpenViaticosBarCommand = new RelayCommand(
            () => OpenViaticos(GastosOperacionales.FirstOrDefault(c => c.IsViaticosCategory)),
            () => CanEdit);
        ResetCoeficientesCommand = new RelayCommand(ResetCoeficientes, () => CanEdit);
        LoadCoeficientesEstandarCommand = new RelayCommand(LoadCoeficientesEstandar, () => CanEdit);
        LoadTipicosCoeficientesCommand = new RelayCommand(LoadTipicosCoeficientes, () => CanEdit);
        SaveCostosPresetCommand = new RelayCommand(SaveCostosPreset, () => CanEdit);
        RestoreCostosPresetCommand = new RelayCommand(RestoreCostosPreset, () => CanEdit && HasCostosPreset);
        ApplyCoeficientesCommand = new RelayCommand(ApplyCoeficientes, () => CanEdit);
        AddNegotiatedDiscountCommand = new RelayCommand(AddNegotiatedDiscount, () => CanEdit);
        RemoveNegotiatedDiscountCommand = new RelayCommand<NegotiatedDiscountVm>(RemoveNegotiatedDiscount, _ => CanEdit);
        AddStaffingRoleCommand = new RelayCommand(AddStaffingRole, () => CanEdit);
        RemoveStaffingRoleCommand = new RelayCommand<StaffingRoleVm>(RemoveStaffingRole, _ => CanEdit);
        MoInstalacion = new(session, () => CanEdit, () => MoDirectaRows, RunSuppressed, RefreshCostOnly, priceBank, undo);
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
    public string MargenSubLabel { get => margenSubLabel; private set => SetProperty(ref margenSubLabel, value); }
    public string UfM2Label { get => ufM2Label; private set => SetProperty(ref ufM2Label, value); }
    public string UfM2SubLabel { get => ufM2SubLabel; private set => SetProperty(ref ufM2SubLabel, value); }
    public string ResumenFooterText { get => resumenFooterText; private set => SetProperty(ref resumenFooterText, value); }
    public string ValorUfDiaLabel { get => valorUfDiaLabel; private set => SetProperty(ref valorUfDiaLabel, value); }
    public string FamCostoLabel { get => famCostoLabel; private set => SetProperty(ref famCostoLabel, value); }
    public string FamVentaLabel { get => famVentaLabel; private set => SetProperty(ref famVentaLabel, value); }
    public string FamDeltaLabel { get => famDeltaLabel; private set => SetProperty(ref famDeltaLabel, value); }
    public string ChainIvaLabel { get => chainIvaLabel; private set => SetProperty(ref chainIvaLabel, value); }
    public string ChainTotalLabel { get => chainTotalLabel; private set => SetProperty(ref chainTotalLabel, value); }
    public string Currency
    {
        get => currency;
        private set
        {
            if (SetProperty(ref currency, value))
            {
                OnPropertyChanged(nameof(ShowCurrencyColumn));
                OnPropertyChanged(nameof(CoefCostoHeader));
                OnPropertyChanged(nameof(CoefVentaHeader));
            }
        }
    }

    /// <summary>Columna de moneda extranjera del análisis: se oculta cuando el proyecto está en CLP.</summary>
    public bool ShowCurrencyColumn => ShowsUfFigures;

    /// <summary>Encabezado de la columna de costo por familia, en la moneda del proyecto.</summary>
    public string CoefCostoHeader => $"COSTO {Currency}";

    /// <summary>Encabezado de la columna de venta por familia, en la moneda del proyecto.</summary>
    public string CoefVentaHeader => $"VENTA {Currency}";
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
                (OpenViaticosBarCommand as RelayCommand)?.RaiseCanExecuteChanged();
                ResetCoeficientesCommand.RaiseCanExecuteChanged();
                LoadCoeficientesEstandarCommand.RaiseCanExecuteChanged();
                LoadTipicosCoeficientesCommand.RaiseCanExecuteChanged();
                AbrirGestionFamiliasCommand.RaiseCanExecuteChanged();
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

    public bool FerreteriaActiva
    {
        get => ferreteriaActiva;
        set
        {
            if (!SetProperty(ref ferreteriaActiva, value))
                return;
            if (!CanEdit)
                return;
            session.ProjectInfo.FerreteriaActiva = value;
            if (value && session.ProjectInfo.FerreteriaPct <= 0m)
            {
                session.ProjectInfo.FerreteriaPct = 5m;
                ferreteriaPctText = 5m.ToString("N1", CultureInfo.CurrentCulture);
                OnPropertyChanged(nameof(FerreteriaPctText));
            }
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            RefreshCostOnly();
        }
    }

    public string FerreteriaPctText
    {
        get => ferreteriaPctText;
        set
        {
            if (!SetProperty(ref ferreteriaPctText, value ?? ""))
                return;
            if (!CanEdit || !TryParsePct(value, out var pct))
                return;
            session.ProjectInfo.FerreteriaPct = pct;
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            if (session.ProjectInfo.FerreteriaActiva)
                RefreshCostOnly();
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
                UtilidadPolicyLabel = value ? "Utilidades (aditiva)" : "Margen s/ venta";
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
    /// La política nativa ya lo implica; este switch permite activarlo también con markup global.
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

    /// <summary>Seccion M.O. de instalacion (modo rendimiento / tarifa D-H). Logica en MoInstalacionSectionVm.</summary>
    public MoInstalacionSectionVm MoInstalacion { get; }

    private void RunSuppressed(Action action)
    {
        suppressCoefRefresh = true;
        try { action(); }
        finally { suppressCoefRefresh = false; }
    }

    public ObservableCollection<CostoRowVm> CostRows { get; } = new();
    public ObservableCollection<CostoRowVm> OrigenesDirectos { get; } = new();
    public ObservableCollection<CostoGrupoVm> CostGroups { get; } = new();
    public CostoRowVm? VentaCierre
    {
        get => ventaCierre;
        private set
        {
            if (SetProperty(ref ventaCierre, value))
                OnPropertyChanged(nameof(HasVentaCierre));
        }
    }
    public bool HasVentaCierre => VentaCierre != null;
    public CostoRowVm? UfMetroCierre
    {
        get => ufMetroCierre;
        private set
        {
            if (SetProperty(ref ufMetroCierre, value))
                OnPropertyChanged(nameof(HasUfMetroCierre));
        }
    }
    public bool HasUfMetroCierre => UfMetroCierre != null;
    public ObservableCollection<CostoRowVm> CierreAjustes { get; } = new();
    public RelayCommand<CostoGrupoVm> ToggleCostGroupCommand { get; }
    public RelayCommand ToggleAnalisisCommand { get; }
    public RelayCommand ToggleFerreteriaDetalleCommand { get; }
    public RelayCommand<CoefFactorVm> AbrirCatalogoFamiliasCommand { get; }
    public RelayCommand<PrecioFamiliaPickVm> ElegirFamiliaPrecioCommand { get; }
    public RelayCommand AbrirGestionFamiliasCommand { get; }
    private bool analisisAbierto;
    public bool AnalisisAbierto
    {
        get => analisisAbierto;
        private set
        {
            if (SetProperty(ref analisisAbierto, value))
                OnPropertyChanged(nameof(AnalisisChevron));
        }
    }
    public string AnalisisChevron => analisisAbierto ? "\u25BE" : "\u25B8";
    public ObservableCollection<CostoOrigenVm> Origenes { get; } = new();
    public ObservableCollection<CostoFamiliaVm> Familias { get; } = new();
    public ObservableCollection<MoDirectaRowVm> MoDirectaRows { get; } = new();
    public ObservableCollection<MoIndirectaRowVm> MoIndirectaRows { get; } = new();
    public ObservableCollection<GastoOpCategoriaVm> GastosOperacionales { get; } = new();
    public ObservableCollection<GoComposeSegmentVm> GoComposeSegments { get; } = new();
    public ObservableCollection<GoComposeLegendVm> GoComposeLegend { get; } = new();
    public ObservableCollection<CoefFactorVm> CoefFactors { get; } = new();
    public ObservableCollection<PrecioFamiliaChipVm> PrecioFamiliasLibres { get; } = new();
    public bool HasPrecioFamiliasLibres => PrecioFamiliasLibres.Count > 0;
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
    public RelayCommand OpenViaticosBarCommand { get; }
    public RelayCommand ResetCoeficientesCommand { get; }
    public RelayCommand LoadCoeficientesEstandarCommand { get; }
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
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (CoefFactors.Any(f => f.SuspendPush))
                return;
            Refresh();
        });
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
            CurrencyColumnHeader = moneda;

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
            var familiaBuilt = new List<(string Key, int Count, decimal Clp, decimal Uf, decimal Cant, string Unidad)>();
            foreach (var grupo in familiaGroups)
            {
                var codesInFamily = grupo.Select(m => m.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var cubRows = codesInFamily.SelectMany(c => cubicacionByCode[c]);
                familiaBuilt.Add((
                    grupo.Key,
                    codesInFamily.Count,
                    cubRows.Sum(r => r.MatClp * r.Cantidad + r.MoClp * r.Cantidad),
                    cubRows.Sum(r => r.UfTotal),
                    cubRows.Sum(r => Math.Abs(r.Cantidad)),
                    grupo.First().Unidad ?? ""));
            }

            familiaBuilt.Sort((a, b) => b.Clp.CompareTo(a.Clp));
            decimal familiaSum = 0m;
            foreach (var row in familiaBuilt)
                familiaSum += row.Clp;
            const double familiaTrack = 252d;
            foreach (var row in familiaBuilt)
            {
                var share = familiaSum > 0m ? row.Clp / familiaSum : 0m;
                var width = row.Clp > 0m && familiaSum > 0m
                    ? Math.Max(4d, (double)(share * (decimal)familiaTrack))
                    : 0d;
                familias.Add(new CostoFamiliaVm
                {
                    Familia = row.Key,
                    MaterialesCount = row.Count,
                    TotalClp = row.Clp,
                    TotalUf = row.Uf,
                    TotalLabel = MoneyPrimary(row.Clp),
                    TotalMonedaLabel = MoneyPair(row.Clp).Secondary,
                    CountLabel = $"{row.Cant:N2} {row.Unidad}".Trim(),
                    ShareLabel = (share * 100m).ToString("0", CultureInfo.CurrentCulture) + "%",
                    Share = (double)share,
                    BarWidth = width
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
            RefreshFerreteriaFamilias(cost, data, project, valorUf);

            FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
            RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);
            AttachCoefPresentation();

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
        _ = valorUf;
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
                FraccionObra = line.FraccionObra,
                Pct = line.Pct
            });
        }

        var totals = projection.Totals;
        TotalMoDirectaLabel = MoneyPrimary(totals.DirectaClp);
        TotalMoIndirectaLabel = MoneyPrimary(totals.IndirectaClp);
        TotalMoLabel = MoneyPrimary(totals.TotalClp);
        TotalMoSubLabel = MoneyPair(totals.TotalClp).Secondary;
        MoDirectaPct = totals.DirectaPct;
        MoIndirectaPct = totals.IndirectaPct;
        TotalMoDirectaDotacion = totals.DirectaDotacion;
        TotalMoDotacion = totals.TotalDotacion;
        OnPropertyChanged(nameof(TotalMoDirectaDotacionLabel));

        ShowMoSection = true; // Siempre visible: lectura desde MO / plantilla
    }

    private void RefreshGoSection(ProjectFileData data, decimal valorUf)
    {
        var open = GastosOperacionales
            .Where(c => c.IsExpanded)
            .Select(c => c.Codigo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        GastosOperacionales.Clear();

        // Repara proyectos donde el sync EPP dejó solo 3.5 (u otras categorías faltantes).
        var repaired = GastoOperacionalPlantilla.EnsureComplete(data);

        var source = data.GastosOperacionales;
        var seededFresh = source == null || source.Count == 0;
        var rows = seededFresh ? GastoOperacionalPlantilla.CreateClean() : source!;

        foreach (var r in rows)
        {
            var expanded = open.Contains(r.Codigo ?? "");
            GastosOperacionales.Add(new GastoOpCategoriaVm(r, valorUf, OnGoChanged, expanded: expanded, canEdit: () => CanEdit));
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
        var mo = EppBudgetData.ReadMoDrivers(data);
        epp.ApplyMoDotacion(mo.MesesObra, mo.PromedioDirecto, mo.DotacionIndirecta);
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
        foreach (var cat in GastosOperacionales)
            cat.Pct = totalGoClp > 0 ? cat.MontoClp / totalGoClp * 100m : 0m;

        TotalGoLabel = MoneyPrimary(totalGoClp);
        TotalGoUfLabel = MoneyPair(totalGoClp).Secondary;
        TotalGoUfSubLabel = MoneyPair(totalGoClp).Secondary;
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
                Fill = CostoUiBrushes.Parse(GoPalette[seg.PaletteIndex % GoPalette.Length], seg.FillOpacity),
                WidthUnits = seg.WidthUnits,
            });
        }

        foreach (var item in projection.Legend)
        {
            GoComposeLegend.Add(new GoComposeLegendVm
            {
                Fill = CostoUiBrushes.Parse(GoPalette[item.PaletteIndex % GoPalette.Length]),
                Codigo = item.Codigo,
                PctLabel = item.PctLabel,
            });
        }
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

        var vm = new ViaticosProfesionalViewModel(currentValorUf, picoDir, rolesInd, mesesObra, ocultarEquivalenciaUf: !ShowsUfFigures);
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
        var vm = new GastoOpCategoriaVm(row, currentValorUf, OnGoChanged, canEdit: () => CanEdit);
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
        foreach (var r in GastoOperacionalPlantilla.CreateClean())
            GastosOperacionales.Add(new GastoOpCategoriaVm(r, currentValorUf, OnGoChanged, canEdit: () => CanEdit));
        OnGoChanged();
    }

    private void ToggleAnalisis() => AnalisisAbierto = !AnalisisAbierto;

    private bool ferreteriaDetalleAbierto;

    public bool FerreteriaDetalleAbierto
    {
        get => ferreteriaDetalleAbierto;
        set
        {
            if (SetProperty(ref ferreteriaDetalleAbierto, value))
                OnPropertyChanged(nameof(FerreteriaDetalleMarca));
        }
    }

    public string FerreteriaDetalleMarca => FerreteriaDetalleAbierto ? "−" : "+";

    public ObservableCollection<FerreteriaFamiliaVm> FerreteriaFamilias { get; } = new();

    private string ferreteriaPopupTitulo = "Ferretería";
    public string FerreteriaPopupTitulo
    {
        get => ferreteriaPopupTitulo;
        private set => SetProperty(ref ferreteriaPopupTitulo, value);
    }

    private string ferreteriaPopupNota = "";
    public string FerreteriaPopupNota
    {
        get => ferreteriaPopupNota;
        private set => SetProperty(ref ferreteriaPopupNota, value);
    }

    private void ToggleFerreteriaDetalle() => FerreteriaDetalleAbierto = !FerreteriaDetalleAbierto;

    private void ToggleCostGroup(CostoGrupoVm? grupo)
    {
        if (grupo == null)
            return;
        grupo.IsExpanded = !grupo.IsExpanded;
    }

    private CostoRowVm CreateCostRow(string label, decimal clp, string explanation, decimal valorUf, string kind, string participacion = "", bool enInforme = true, bool tieneOrigen = false, string origenTexto = "", double share = 0d, string seccion = "directo")
    {
        decimal uf = PriceConversion.ClpToUf(clp, valorUf);
        return new CostoRowVm
        {
            Concepto = label,
            ValorUf = uf,
            CurrencyFigure = MoneyPrimary(clp),
            ShowCurrencyColumn = ShowsUfFigures,
            ValorMoneda = clp,
            Explicacion = explanation,
            Kind = kind,
            ParticipacionLabel = participacion,
            Share = share,
            Seccion = seccion,
            IsEnInforme = enInforme,
            TieneOrigen = tieneOrigen,
            OrigenTexto = origenTexto,
            ValorMonedaLabel = clp == 0m
                ? "$ 0"
                : $"$ {decimal.Round(clp, 0, MidpointRounding.AwayFromZero):N0}"
        };
    }

    private void AddRow(ObservableCollection<CostoRowVm> rows, string label, decimal clp, string explanation,
        string moneda, decimal valorUf, decimal tipoCambio, string kind = "regular")
    {
        _ = moneda;
        _ = tipoCambio;
        rows.Add(CreateCostRow(label, clp, explanation, valorUf, kind));
    }

    private void PushCostGroup(string titulo, CostoRowVm header, IReadOnlyList<CostoRowVm> children, bool firstLoad, HashSet<string> openTitles, bool expandOnFirst)
    {
        var grupo = new CostoGrupoVm
        {
            Titulo = titulo,
            ClpLabel = header.ValorMonedaLabel,
            UfLabel = header.CurrencyFigure,
            Explicacion = header.Explicacion,
            IsExpanded = firstLoad ? expandOnFirst : openTitles.Contains(titulo)
        };
        foreach (var child in children)
            grupo.Lineas.Add(child);
        CostGroups.Add(grupo);
    }

    private void EnsureCoefFactors()
    {
        if (CoefFactors.Count > 0)
            return;

        CoefFactors.Add(new CoefFactorVm("Conductores", "Materiales", "Conductores", 1.50m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Canalización", "PVC / EPC", "Canalización", 1.44m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Tableros", "Tableros", "Tableros", 1.10m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Iluminación", "Luminarias", "Iluminación", 1.17m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Equipos", "Equipos", "Equipos", 1.12m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Accesorios", "Varios", "Accesorios", 1.14m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Enchufes y tomacorrientes", "Artefactos", "Enchufes", 1.05m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Protecciones", "Protecciones", "Protecciones", 1.12m, CommitFamilyMarkup, CommitFamilyDiscount));
        CoefFactors.Add(new CoefFactorVm("Puesta a tierra", "Puesta a tierra", "PAT", 1.20m, CommitFamilyMarkup, CommitFamilyDiscount));
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
        ferreteriaActiva = project.FerreteriaActiva;
        OnPropertyChanged(nameof(FerreteriaActiva));
        ferreteriaPctText = project.FerreteriaPct.ToString("N1", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(FerreteriaPctText));

        UtilidadPolicyLabel = string.Equals(project.MarkupMethod, "SobreVenta", StringComparison.OrdinalIgnoreCase)
            && !project.PoliticaCostosExcel
            ? "Margen s/ venta"
            : project.PoliticaCostosExcel
                ? "Utilidades (aditiva)"
                : "Utilidades";

        politicaCostosExcel = project.PoliticaCostosExcel;
        OnPropertyChanged(nameof(PoliticaCostosExcel));

        usarFactoresPresentacionFamilia = project.UsarFactoresPresentacionFamilia;
        OnPropertyChanged(nameof(UsarFactoresPresentacionFamilia));

        MoInstalacion.Load(project);

        UpdateCoefChips();
        RebuildPrecioFamiliaChips();
    }

    public void MovePrecioFamilia(PrecioFamiliaChipVm? chip, string? targetFamilyKey)
    {
        if (!CanEdit || chip == null)
            return;

        var target = CoefFactors.FirstOrDefault(f =>
            string.Equals(f.FamilyKey, targetFamilyKey, StringComparison.OrdinalIgnoreCase));

        PrecioFamiliasLibres.Remove(chip);
        foreach (var row in CoefFactors)
            row.FamiliasPrecio.Remove(chip);

        if (target == null)
            PrecioFamiliasLibres.Add(chip);
        else
            target.FamiliasPrecio.Add(chip);

        OnPropertyChanged(nameof(HasPrecioFamiliasLibres));

        session.ProjectInfo.FamiliasPrecioAsignadas ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        session.ProjectInfo.FamiliasPrecioAsignadas[chip.Clave] = target?.FamilyKey ?? "";

        suppressCoefRefresh = true;
        try
        {
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            session.SyncCubicacionGgUtFactorsFromVariables();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void RebuildPrecioFamiliaChips()
    {
        foreach (var row in CoefFactors)
            row.FamiliasPrecio.Clear();
        PrecioFamiliasLibres.Clear();

        var map = session.ProjectInfo.FamiliasPrecioAsignadas;
        var familiasActivas = EnumerarFamiliasPrecio();
        var clavesActivas = new HashSet<string>(familiasActivas.Select(FamilyPresentationDefaults.Normalize), StringComparer.OrdinalIgnoreCase);

        // Limpiar asignaciones huérfanas de listas de precios anteriores que ya no existen
        if (map != null && map.Count > 0)
        {
            var huerfanas = map.Keys.Where(k => !clavesActivas.Contains(k)).ToList();
            if (huerfanas.Count > 0)
            {
                foreach (var h in huerfanas)
                    map.Remove(h);
                session.MarkDirty(ProjectDirtyScope.ProjectInfo);
            }
        }

        foreach (var nombre in familiasActivas)
        {
            var clave = FamilyPresentationDefaults.Normalize(nombre);
            var chip = new PrecioFamiliaChipVm(nombre, clave);
            var row = FilaDeFamiliaPrecio(clave, map);
            if (row == null)
                PrecioFamiliasLibres.Add(chip);
            else
                row.FamiliasPrecio.Add(chip);
        }

        OnPropertyChanged(nameof(HasPrecioFamiliasLibres));
        foreach (var row in CoefFactors)
            row.RefrescarResumenFamilias();
    }

    private void AbrirGestionFamilias()
    {
        if (!CanEdit)
            return;

        var vm = new GestionFamiliasViewModel(session, priceBank, () =>
        {
            suppressCoefRefresh = true;
            try
            {
                RefreshCostOnly();
                RebuildPrecioFamiliaChips();
                OnPropertyChanged(nameof(Familias));
            }
            finally
            {
                suppressCoefRefresh = false;
            }
        });

        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new GestionFamiliasDialog(vm);
        if (owner != null)
            dialog.Owner = owner;

        dialog.ShowDialog();

        RefreshCostOnly();
        RebuildPrecioFamiliaChips();
        OnPropertyChanged(nameof(Familias));
    }

    private void AbrirCatalogoFamilias(CoefFactorVm? fila)
    {
        if (fila == null || !IsFactoresFamiliaEditable)
            return;

        var abrir = !fila.CatalogoAbierto;
        foreach (var row in CoefFactors)
            row.CatalogoAbierto = false;
        if (!abrir)
            return;

        LlenarCatalogoFamilias(fila);
        fila.CatalogoAbierto = true;
    }

    private void ElegirFamiliaPrecio(PrecioFamiliaPickVm? pick)
    {
        if (pick == null || pick.Bloqueada || !IsFactoresFamiliaEditable)
            return;

        var fila = CoefFactors.FirstOrDefault(f => f.CatalogoAbierto);
        if (fila == null)
            return;

        var chip = PrecioFamiliasLibres.FirstOrDefault(c => MismaFamilia(c, pick))
            ?? CoefFactors.SelectMany(r => r.FamiliasPrecio).FirstOrDefault(c => MismaFamilia(c, pick))
            ?? new PrecioFamiliaChipVm(pick.Nombre, pick.Clave);
        MovePrecioFamilia(chip, pick.EnEstaFila ? null : fila.FamilyKey);
        if (fila.CatalogoAbierto)
            LlenarCatalogoFamilias(fila);
    }

    private void LlenarCatalogoFamilias(CoefFactorVm fila)
    {
        fila.Catalogo.Clear();
        foreach (var nombre in EnumerarFamiliasPrecio())
        {
            var clave = FamilyPresentationDefaults.Normalize(nombre);
            CoefFactorVm? duena = null;
            foreach (var row in CoefFactors)
            {
                if (row.FamiliasPrecio.Any(c => string.Equals(c.Clave, clave, StringComparison.OrdinalIgnoreCase)))
                {
                    duena = row;
                    break;
                }
            }

            var enEsta = ReferenceEquals(duena, fila);
            fila.Catalogo.Add(new PrecioFamiliaPickVm(
                nombre,
                clave,
                enEsta,
                duena != null && !enEsta,
                duena?.Name ?? ""));
        }
    }

    private static bool MismaFamilia(PrecioFamiliaChipVm chip, PrecioFamiliaPickVm pick)
        => string.Equals(chip.Clave, pick.Clave, StringComparison.OrdinalIgnoreCase);

    private CoefFactorVm? FilaDeFamiliaPrecio(string clave, Dictionary<string, string>? map)
    {
        if (map != null && map.TryGetValue(clave, out var assigned))
        {
            if (string.IsNullOrWhiteSpace(assigned))
                return null;
            return CoefFactors.FirstOrDefault(f =>
                string.Equals(f.FamilyKey, assigned, StringComparison.OrdinalIgnoreCase));
        }

        var direct = CoefFactors.FirstOrDefault(f =>
            string.Equals(FamilyPresentationDefaults.Normalize(f.FamilyKey), clave, StringComparison.Ordinal));
        if (direct != null)
            return direct;

        var naturalTarget = FamilyPresentationDefaults.NaturalCanonicalTarget(clave);
        return CoefFactors.FirstOrDefault(f =>
            string.Equals(FamilyPresentationDefaults.Normalize(f.FamilyKey), naturalTarget, StringComparison.Ordinal));
    }

    private List<string> EnumerarFamiliasPrecio()
    {
        var bankCategories = (priceBank?.GetAll().Select(e => e.Categoria) ?? Enumerable.Empty<string>()).ToList();
        return ElectricalFamilyCatalog.CombinarFamiliasConOcultas(
            session.ProjectInfo.FamiliasOcultas,
            session.ProjectInfo.FamiliasPersonalizadas,
            bankCategories).ToList();
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
        if (!CanEdit || session.GetFamilyMarkup(familyKey) == value)
            return;
        suppressCoefRefresh = true;
        try
        {
            session.SetFamilyMarkup(familyKey, value, syncTakeoff: false);
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            session.SyncCubicacionGgUtFactorsFromVariables();
            RefreshCostOnly();
        }
        finally
        {
            suppressCoefRefresh = false;
        }
    }

    private void CommitFamilyDiscount(string familyKey, decimal pct)
    {
        if (!CanEdit || session.GetFamilyDiscountPct(familyKey) == pct)
            return;
        suppressCoefRefresh = true;
        try
        {
            session.SetFamilyDiscountPct(familyKey, pct, syncTakeoff: false);
            RefreshCoefEffectiveLabels();
            UpdateCoefChips();
            session.SyncCubicacionGgUtFactorsFromVariables();
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

        if (!ConfirmAction("Restaurar preset",
            $"¿Desea restaurar los factores y márgenes guardados ({preset.Etiqueta})?\n\nEsta acción sobrescribirá los valores actuales de las familias."))
            return;

        undo?.PushUndoPoint(session, $"Restaurar preset de costos «{preset.Etiqueta}»");
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
            UtilidadPolicyLabel = preset.PoliticaCostosExcel ? "Utilidades (aditiva)" : "Utilidades";

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
        RefreshFerreteriaFamilias(cost, data, project, valorUf);
        FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
        RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);
        OnPropertyChanged(nameof(CurrentPolicy));
        OnPropertyChanged(nameof(IsGastosGeneralesEditable));
        OnPropertyChanged(nameof(IsUtilidadEditable));
        OnPropertyChanged(nameof(IsFactoresFamiliaEditable));
        OnPropertyChanged(nameof(GastosGeneralesPolicyHint));
        OnPropertyChanged(nameof(UtilidadPolicyHint));
        AttachCoefPresentation();
    }

    private void UpdateSummaryKpis(CostBreakdown cost, ProjectInfoRow project, decimal valorUf)
    {
        decimal directoClp = cost.CostoDirectoClp;
        decimal ventaClp = cost.TotalVentaClp;
        decimal m2Clp = project.MetrosCuadrados > 0 ? ventaClp / project.MetrosCuadrados : 0m;

        bool enClp = !ShowsUfFigures;
        var costoPair = MoneyPair(directoClp);
        var ventaPair = MoneyPair(ventaClp);
        CostoDirectoSubLabel = costoPair.Primary;
        CostoDirectoLabel = costoPair.Secondary;
        TotalVentaSubLabel = ventaPair.Primary;
        TotalVentaLabel = ventaPair.Secondary;
        MargenLabel = $"{cost.MargenRealPct:N1} %";
        decimal margenClp = Math.Max(0m, ventaClp - directoClp);
        var margenPair = MoneyPair(margenClp);
        MargenSubLabel = margenPair.Secondary.Length > 0
            ? $"{margenPair.Primary} ({margenPair.Secondary})"
            : margenPair.Primary;
        ValorUfDiaLabel = valorUf.ToString("N0", CultureInfo.CurrentCulture);
        UfM2Label = enClp
            ? $"$ {m2Clp:N0}/m²"
            : $"{PriceConversion.ClpToUf(m2Clp, valorUf):N3} UF/m²";
        UfM2SubLabel = project.MetrosCuadrados > 0
            ? (enClp ? "Total venta / m²" : $"$ {m2Clp:N0}/m²")
            : "Sin m²";
        ResumenFooterText = enClp
            ? $"Costo ejecución $ {directoClp:N0} · Total venta $ {ventaClp:N0} · Margen {cost.MargenRealPct:N1}%"
            : $"Costo ejecución {MoneyText(directoClp)} · Total venta {MoneyText(ventaClp)} · Margen {cost.MargenRealPct:N1}%";
        NegotiationSimulator.UpdateBase(directoClp, ventaClp, valorUf);
        Tabs.Update(cost, project, Familias, MoneyPrimary, clp => MoneyPair(clp).Secondary);
    }

    private void RefreshFerreteriaFamilias(CostBreakdown cost, ProjectFileData data, ProjectInfoRow project, decimal valorUf)
    {
        _ = valorUf;
        decimal pct = project.FerreteriaPct;
        decimal baseEngine = cost.MaterialesUf - cost.FerreteriaUf;
        var codeToFamily = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var material in data.Materiales ?? Enumerable.Empty<MaterialRow>())
        {
            if (string.IsNullOrWhiteSpace(material.Codigo) || codeToFamily.ContainsKey(material.Codigo))
                continue;
            codeToFamily[material.Codigo] = string.IsNullOrWhiteSpace(material.Categoria)
                ? "Sin familia"
                : material.Categoria.Trim();
        }

        var byFamily = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        decimal summed = 0m;
        foreach (var row in data.Cubicacion ?? Enumerable.Empty<CubicacionRow>())
        {
            decimal mat = row.Cantidad * row.MatClpConFallback;
            if (mat == 0m)
                continue;
            var familia = !string.IsNullOrWhiteSpace(row.Codigo) && codeToFamily.TryGetValue(row.Codigo, out var name)
                ? name
                : "Sin familia";
            byFamily[familia] = byFamily.GetValueOrDefault(familia) + mat;
            summed += mat;
        }

        decimal gap = baseEngine - summed;
        if (Math.Abs(gap) >= 1m)
            byFamily["Otros materiales"] = byFamily.GetValueOrDefault("Otros materiales") + gap;

        FerreteriaFamilias.Clear();
        decimal totalMat = 0m;
        decimal totalFerr = 0m;
        foreach (var pair in byFamily.Where(p => p.Value != 0m).OrderByDescending(p => p.Value))
        {
            decimal ferr = pair.Value * pct / 100m;
            totalMat += pair.Value;
            totalFerr += ferr;
            FerreteriaFamilias.Add(LineaFerreteria(pair.Key, pair.Value, ferr, total: false));
        }

        if (FerreteriaFamilias.Count > 0)
            FerreteriaFamilias.Add(LineaFerreteria("Total", totalMat, totalFerr, total: true));

        FerreteriaPopupTitulo = $"Ferretería {pct:N1} %";
        FerreteriaPopupNota = project.FerreteriaActiva
            ? "Porcentaje sobre los materiales de cada familia. Este monto ya está en el costo."
            : "Porcentaje sobre los materiales de cada familia. Está apagada: todavía no suma al costo.";
    }

    private FerreteriaFamiliaVm LineaFerreteria(string familia, decimal materiales, decimal ferreteria, bool total)
        => new()
        {
            Familia = familia,
            MaterialesLabel = MoneyPrimary(materiales),
            MaterialesUfLabel = MoneyPair(materiales).Secondary,
            FerreteriaLabel = MoneyPrimary(ferreteria),
            FerreteriaUfLabel = MoneyPair(ferreteria).Secondary,
            IsTotal = total
        };

    private void AttachCoefPresentation()
    {
        decimal costo = 0m;
        decimal venta = 0m;
        foreach (var factor in CoefFactors)
        {
            var hit = Familias.FirstOrDefault(f =>
                string.Equals(f.Familia, factor.FamilyKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Familia, factor.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(f.Familia, factor.Map, StringComparison.OrdinalIgnoreCase));
            var costoUf = hit?.TotalUf ?? 0m;
            var markup = ParseCoef(factor.ValueText, 1m);
            var discount = ParseCoef(factor.DiscountText, 0m);
            var ventaUf = costoUf * ExcelCostosBridge.PresentationFactor(markup, discount);
            factor.SetPresentation(
                costoUf,
                ventaUf,
                hit?.Share ?? 0d,
                costoUf > 0m ? MoneyPrimary(PriceConversion.UfToClp(costoUf, currentValorUf)) : null,
                costoUf > 0m ? MoneyPrimary(PriceConversion.UfToClp(ventaUf, currentValorUf)) : null);
            costo += costoUf;
            venta += ventaUf;
        }

        var costoClp = PriceConversion.UfToClp(costo, currentValorUf);
        var ventaClp = PriceConversion.UfToClp(venta, currentValorUf);
        FamCostoLabel = costo > 0m ? MoneyPrimary(costoClp) : "—";
        FamVentaLabel = venta > 0m ? MoneyPrimary(ventaClp) : "—";
        var delta = MoneyPair(ventaClp - costoClp);
        var deltaBase = ventaClp - costoClp >= 0m ? $"+{delta.Primary}" : delta.Primary;
        FamDeltaLabel = costo > 0m
            ? (delta.Secondary.Length == 0 ? deltaBase : $"{deltaBase} ({delta.Secondary})")
            : "—";
    }

    private static decimal ParseCoef(string? text, decimal fallback)
    {
        var s = (text ?? "").Trim().TrimEnd('%').Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
    }

    private void RefreshClosingPolicySection(CostBreakdown cost, string moneda, decimal valorUf, decimal tipoCambio)
    {
        var selection = session.Data.ClosingPolicy ?? ClosingPolicySelection.Default();
        ClosingPolicySection.SetSilent(selection);
        // El motor ya aplicó la política sobre el breakdown; leer su resultado en vez de recalcular.
        var result = cost.ClosingPolicyResult ?? ClosingPolicyEngine.Apply(cost, selection);
        var svc = currencySvc;
        var ventaPair = svc.FormatMoneyPairFromClp(result.VentaFinalClp, moneda, valorUf, tipoCambio);
        var margenPair = svc.FormatMoneyPairFromClp(result.MargenClp, moneda, valorUf, tipoCambio);
        var margenLabel = margenPair.Secondary.Length == 0
            ? $"{margenPair.Primary} · {result.MargenPct:N1}%"
            : $"{margenPair.Primary} ({margenPair.Secondary}) · {result.MargenPct:N1}%";
        ClosingPolicySection.SetResult(result, ventaPair.Primary, ventaPair.Secondary, margenLabel);
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
        var policyName = ClosingPolicyOptionVm.All.FirstOrDefault(o => o.Value == policy)?.DisplayName ?? policy.ToString();
        if (!ConfirmAction("Cambiar política de cierre",
            $"¿Desea cambiar la política de cierre comercial a «{policyName}»?\n\nEsto afectará la fórmula de cálculo de la venta final y los márgenes del proyecto."))
        {
            ClosingPolicySection.SetSilent(selection);
            return;
        }
        undo?.PushUndoPoint(session, $"Cambiar política de cierre a {policyName}");
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

    private void ApplyNegotiatedTargetPrice(decimal targetPrice)
    {
        undo?.PushUndoPoint(session, "Aplicar precio objetivo negociado");
        CommitClosingPolicyType(ClosingPolicyType.TargetPrice);
        CommitClosingPolicyParam(nameof(ClosingPolicySelection.TargetPriceClp), targetPrice);
    }

    private void FillCostRows(
        ObservableCollection<CostoRowVm> rows,
        CostBreakdown cost,
        ProjectInfoRow project,
        string moneda,
        decimal valorUf,
        decimal tipoCambio)
    {
        _ = moneda;
        _ = tipoCambio;
        rows.Clear();
        OrigenesDirectos.Clear();
        var openTitles = CostGroups.Where(g => g.IsExpanded).Select(g => g.Titulo).ToHashSet(StringComparer.Ordinal);
        var firstLoad = CostGroups.Count == 0;
        CostGroups.Clear();
        CierreAjustes.Clear();

        var pending = new List<CostoRowVm>();
        CostoRowVm? venta = null;
        CostoRowVm? metro = null;
        var sawCost = false;
        var sawRecargos = false;
        var openedComp = false;
        var openedCierre = false;
        var ventaBase = cost.TotalVentaUf;

        foreach (var line in CostoBreakdownLineBuilder.BuildLines(cost, project))
        {
            var enInforme = !CostoBreakdownLineBuilder.IsMetroLine(line.Label);
            if (!sawCost && !openedComp && line.Kind == "regular" && line.AmountUf != 0m)
            {
                rows.Add(InformeSection("Composición del costo de ejecución"));
                openedComp = true;
            }

            if (sawCost && !openedCierre && enInforme && line.Kind != "grand")
            {
                rows.Add(InformeSection("Recargos, margen y cierre"));
                openedCierre = true;
            }

            var share = enInforme && ventaBase != 0m ? $"{line.AmountUf / ventaBase * 100m:N1} %" : "";
            var esFerreteria = line.Label.StartsWith("Ferretería", StringComparison.Ordinal);
            var row = CreateCostRow(line.Label, line.AmountUf, line.Explanation, valorUf, line.Kind, share, enInforme, esFerreteria, "", enInforme && ventaBase != 0m ? (double)(line.AmountUf / ventaBase) : 0d, sawCost ? "cierre" : "directo");
            rows.Add(row);
            if (!sawCost && line.Kind == "regular" && !row.IsQuiet)
                OrigenesDirectos.Add(row);

            if (line.Kind == "grand")
            {
                venta = row;
                continue;
            }

            if (CostoBreakdownLineBuilder.IsMetroLine(line.Label))
            {
                metro = row;
                continue;
            }

            if (line.Kind == "total" && !sawCost)
            {
                PushCostGroup(line.Label, row, pending, firstLoad, openTitles, expandOnFirst: true);
                pending.Clear();
                sawCost = true;
                continue;
            }

            if (line.Kind == "subtotal" && sawCost && !sawRecargos)
            {
                PushCostGroup(line.Label, row, pending, firstLoad, openTitles, expandOnFirst: false);
                pending.Clear();
                sawRecargos = true;
                continue;
            }

            pending.Add(row);
        }

        if (pending.Count > 0)
        {
            var header = pending.LastOrDefault(r => r.Kind == "subtotal");
            if (header != null)
            {
                var index = pending.IndexOf(header);
                PushCostGroup(header.Concepto, header, pending.Take(index).ToList(), firstLoad, openTitles, expandOnFirst: false);
                foreach (var extra in pending.Skip(index + 1))
                    CierreAjustes.Add(extra);
            }
            else
            {
                foreach (var extra in pending)
                    CierreAjustes.Add(extra);
            }
        }

        VentaCierre = venta;
        UfMetroCierre = metro;
        ChainIvaLabel = rows.FirstOrDefault(r => r.Concepto == "IVA") is { } iva ? MoneyText(iva.ValorMoneda) : "—";
        ChainTotalLabel = venta?.ValorMonedaLabel is { Length: > 0 } clp ? clp : "—";
    }

    private static CostoRowVm InformeSection(string title) => new()
    {
        Concepto = title,
        Kind = "section",
        Explicacion = "",
        ValorMonedaLabel = ""
    };

    private void ResetCoeficientes()
    {
        if (!CanEdit)
            return;
        if (!ConfirmAction("Restablecer factores",
            "¿Desea restablecer todos los factores de familia a 1.00× y eliminar los descuentos aplicados? Se perderán las personalizaciones actuales."))
            return;
        undo?.PushUndoPoint(session, "Restablecer factores de familia");
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

    private void LoadCoeficientesEstandar() =>
        ApplyCoefPreset(ExcelCostosBridge.PresetFactoresEstandar, fallback: 1.30m, "Factores Estándar");

    private void LoadTipicosCoeficientes() =>
        ApplyCoefPreset(ExcelCostosBridge.PresetFactoresTipicos, fallback: 1.14m, "Factores Típicos");

    private void ApplyCoefPreset(IReadOnlyDictionary<string, decimal> preset, decimal fallback, string presetName)
    {
        if (!CanEdit)
            return;
        if (!ConfirmAction("Aplicar factores predefinidos",
            $"¿Desea aplicar los factores y márgenes «{presetName}»?\n\nEsta acción sobrescribirá los factores por familia y los porcentajes de gastos generales y utilidad."))
            return;
        undo?.PushUndoPoint(session, $"Aplicar preset «{presetName}»");
        suppressCoefRefresh = true;
        try
        {
            foreach (var f in CoefFactors)
            {
                var v = preset.TryGetValue(f.FamilyKey, out var x) ? x : fallback;
                session.SetFamilyMarkup(f.FamilyKey, v);
                f.SetValueSilent(v);
            }

            ExcelCostosBridge.ApplyPoliticaEstandar(session.ProjectInfo);
            session.MarkDirty(ProjectDirtyScope.ProjectInfo);

            gastosGeneralesText = "5,0";
            OnPropertyChanged(nameof(GastosGeneralesText));
            utilidadText = "8,0";
            OnPropertyChanged(nameof(UtilidadText));
            valorAgregadoEquiposText = "3,0";
            OnPropertyChanged(nameof(ValorAgregadoEquiposText));
            politicaCostosExcel = true;
            OnPropertyChanged(nameof(PoliticaCostosExcel));
            UtilidadPolicyLabel = "Utilidades (aditiva)";
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
            UtilidadPolicyLabel = "Utilidades (aditiva)";
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
