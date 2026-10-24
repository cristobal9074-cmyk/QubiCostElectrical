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
    private readonly CostoGastosOperacionalesCoordinator _goCoordinator;
    private readonly CostoFamilyPricingCoordinator _familyPricingCoordinator;
    private readonly CostoClosingPolicyCoordinator _closingPolicyCoordinator;
    private readonly CostoCoeficientesCoordinator _coeficientesCoordinator;
    private readonly CostoBreakdownReportCoordinator _reportCoordinator;

    public CostoViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Dashboard, host.Currency, host.Audit, host.PriceBankStore, host.UndoService) { }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
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
        _goCoordinator = new CostoGastosOperacionalesCoordinator(this);
        _familyPricingCoordinator = new CostoFamilyPricingCoordinator(this);
        _closingPolicyCoordinator = new CostoClosingPolicyCoordinator(this);
        _coeficientesCoordinator = new CostoCoeficientesCoordinator(this);
        _reportCoordinator = new CostoBreakdownReportCoordinator(this);

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
        AgregarFamiliaPresentacionCommand = new RelayCommand(AgregarFamiliaPresentacion, () => CanEdit);
        OpenViaticosCommand = new RelayCommand<GastoOpCategoriaVm>(OpenViaticos, c => CanEdit && c is { IsViaticosCategory: true });
        OpenViaticosBarCommand = new RelayCommand(
            () => OpenViaticos(GastosOperacionales.FirstOrDefault(c => c.IsViaticosCategory)),
            () => CanEdit);
        ResetCoeficientesCommand = new RelayCommand(ResetCoeficientes, () => CanEdit);
        LoadCoeficientesEstandarCommand = new RelayCommand(LoadCoeficientesEstandar, () => CanEdit);
        LoadTipicosCoeficientesCommand = new RelayCommand(LoadTipicosCoeficientes, () => CanEdit);
        SaveCostosPresetCommand = new RelayCommand(SaveCostosPreset, () => CanEdit);
        RestoreCostosPresetCommand = new RelayCommand(RestoreCostosPreset, () => CanEdit && HasCostosPreset);
        LoadConfigFromOtherProjectCommand = new RelayCommand(LoadConfigFromOtherProject, () => CanEdit);
        FreezeCurrentFactorsCommand = new RelayCommand(FreezeCurrentFactors, () => CanEdit);
        ApplyCoeficientesCommand = new RelayCommand(ApplyCoeficientes, () => CanEdit);
        AddNegotiatedDiscountCommand = new RelayCommand(AddNegotiatedDiscount, () => CanEdit);
        RemoveNegotiatedDiscountCommand = new RelayCommand<NegotiatedDiscountVm>(RemoveNegotiatedDiscount, _ => CanEdit);
        AddStaffingRoleCommand = new RelayCommand(AddStaffingRole, () => CanEdit);
        RemoveStaffingRoleCommand = new RelayCommand<StaffingRoleVm>(RemoveStaffingRole, _ => CanEdit);
        MoInstalacion = new(session, () => CanEdit, () => MoDirectaRows, RunSuppressed, RefreshCostOnly, priceBank, undo);
        ClosingPolicySection = new ClosingPolicySectionVm(_closingPolicyCoordinator.CommitClosingPolicyType, _closingPolicyCoordinator.CommitClosingPolicyParam);
        NegotiationSimulator = new CommercialNegotiationVm(_closingPolicyCoordinator.ApplyNegotiatedTargetPrice, () => CanEdit);
        SyncCoefFactors();
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
        internal set => SetProperty(ref showGoSection, value);
    }

    public bool ShowOrigenes
    {
        get => showOrigenes;
        private set => SetProperty(ref showOrigenes, value);
    }

    public string CostoDirectoLabel { get => costDirectoLabel; internal set => SetProperty(ref costDirectoLabel, value); }
    public string CostoDirectoSubLabel { get => costDirectoSubLabel; internal set => SetProperty(ref costDirectoSubLabel, value); }
    public string TotalVentaLabel { get => totalVentaLabel; internal set => SetProperty(ref totalVentaLabel, value); }
    public string TotalVentaSubLabel { get => totalVentaSubLabel; internal set => SetProperty(ref totalVentaSubLabel, value); }
    public string MargenLabel { get => margenLabel; internal set => SetProperty(ref margenLabel, value); }
    public string MargenSubLabel { get => margenSubLabel; internal set => SetProperty(ref margenSubLabel, value); }
    public string UfM2Label { get => ufM2Label; internal set => SetProperty(ref ufM2Label, value); }
    public string UfM2SubLabel { get => ufM2SubLabel; internal set => SetProperty(ref ufM2SubLabel, value); }
    public string ResumenFooterText { get => resumenFooterText; internal set => SetProperty(ref resumenFooterText, value); }
    public string ValorUfDiaLabel { get => valorUfDiaLabel; internal set => SetProperty(ref valorUfDiaLabel, value); }
    public string FamCostoLabel { get => famCostoLabel; internal set => SetProperty(ref famCostoLabel, value); }
    public string FamVentaLabel { get => famVentaLabel; internal set => SetProperty(ref famVentaLabel, value); }
    public string FamDeltaLabel { get => famDeltaLabel; internal set => SetProperty(ref famDeltaLabel, value); }
    public string ChainIvaLabel { get => chainIvaLabel; internal set => SetProperty(ref chainIvaLabel, value); }
    public string ChainTotalLabel { get => chainTotalLabel; internal set => SetProperty(ref chainTotalLabel, value); }

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
    public string TotalGoLabel { get => totalGoLabel; internal set => SetProperty(ref totalGoLabel, value); }
    public string TotalGoUfLabel { get => totalGoUfLabel; internal set => SetProperty(ref totalGoUfLabel, value); }
    public string TotalGoUfSubLabel { get => totalGoUfSubLabel; internal set => SetProperty(ref totalGoUfSubLabel, value); }
    public string GoComposeHint { get => goComposeHint; internal set => SetProperty(ref goComposeHint, value); }
    public string GoFooterSubLabel { get => goFooterSubLabel; internal set => SetProperty(ref goFooterSubLabel, value); }
    public string GoCategoriaCountLabel { get => goCategoriaCountLabel; internal set => SetProperty(ref goCategoriaCountLabel, value); }
    public string GoItemCountLabel { get => goItemCountLabel; internal set => SetProperty(ref goItemCountLabel, value); }

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
                AgregarFamiliaPresentacionCommand.RaiseCanExecuteChanged();
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
        internal set => SetProperty(ref utilidadPolicyLabel, value);
    }

    public string CoefFactorMedioLabel
    {
        get => coefFactorMedioLabel;
        internal set => SetProperty(ref coefFactorMedioLabel, value);
    }

    public string CoefActivasLabel
    {
        get => coefActivasLabel;
        internal set => SetProperty(ref coefActivasLabel, value);
    }

    public bool CoefAppliedFlash
    {
        get => coefAppliedFlash;
        internal set => SetProperty(ref coefAppliedFlash, value);
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
        internal set
        {
            if (SetProperty(ref ventaCierre, value))
                OnPropertyChanged(nameof(HasVentaCierre));
        }
    }
    public bool HasVentaCierre => VentaCierre != null;
    public CostoRowVm? UfMetroCierre
    {
        get => ufMetroCierre;
        internal set
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
    public RelayCommand AgregarFamiliaPresentacionCommand { get; }
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
    internal InformePresentacionFamilias? InformeFamilias { get; set; }
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
    public RelayCommand LoadConfigFromOtherProjectCommand { get; }
    public RelayCommand FreezeCurrentFactorsCommand { get; }
    public RelayCommand ApplyCoeficientesCommand { get; }
    public RelayCommand AddNegotiatedDiscountCommand { get; }
    public RelayCommand<NegotiatedDiscountVm> RemoveNegotiatedDiscountCommand { get; }
    public RelayCommand AddStaffingRoleCommand { get; }
    public RelayCommand<StaffingRoleVm> RemoveStaffingRoleCommand { get; }

    public string TotalStaffingPlanLabel
    {
        get => totalStaffingPlanLabel;
        internal set => SetProperty(ref totalStaffingPlanLabel, value);
    }

    public string CostosPresetHint
    {
        get => costosPresetHint;
        internal set => SetProperty(ref costosPresetHint, value);
    }

    public bool HasCostosPreset => workspace.HasOpenProject
        && session.ProjectInfo.CostosPreset is { Factores.Count: > 0 };

    internal void NotifyCostosPresetStateChanged()
    {
        OnPropertyChanged(nameof(HasCostosPreset));
        RestoreCostosPresetCommand.RaiseCanExecuteChanged();
    }

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

            // ─── MO Dotación ────────────────────────────────────────────────────
            RefreshMoSection(data, valorUf);

            // ─── Gastos Operacionales ───────────────────────────────────────────
            RefreshGoSection(data, valorUf);

            // ─── Coeficientes de sobreventa ─────────────────────────────────────
            _coeficientesCoordinator.RefreshCoeficientesSection(project);

            // ─── Descuentos negociados y dotación indirecta ─────────────────────
            _closingPolicyCoordinator.RefreshNegotiatedDiscountsSection(data);
            _closingPolicyCoordinator.RefreshStaffingPlanSection(data);

            _reportCoordinator.UpdateSummaryKpis(cost, project, valorUf);
            _reportCoordinator.RefreshFerreteriaFamilias(cost, data, project, valorUf);

            _reportCoordinator.FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
            _closingPolicyCoordinator.RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);
            _coeficientesCoordinator.AttachCoefPresentation();

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

        ShowMoSection = true;
    }

    // ─── Gastos Operacionales (Delegados) ──────────────────────────────────────
    private void RefreshGoSection(ProjectFileData data, decimal valorUf) => _goCoordinator.RefreshGoSection(data, valorUf);
    private bool SyncGoEppFromBudget(ProjectFileData data, decimal valorUf) => _goCoordinator.SyncGoEppFromBudget(data, valorUf);
    private bool EnsureTrasladoBusItem() => _goCoordinator.EnsureTrasladoBusItem();
    private void OnGoChanged() => _goCoordinator.OnGoChanged();
    private void UpdateGoTotals() => _goCoordinator.UpdateGoTotals();
    private void RebuildComposeBar(decimal totalGoClp) => _goCoordinator.RebuildComposeBar(totalGoClp);
    private void PersistGo(bool markDirty) => _goCoordinator.PersistGo(markDirty);
    private void ToggleGoCategoria(GastoOpCategoriaVm? cat) => _goCoordinator.ToggleGoCategoria(cat);
    private void OpenViaticos(GastoOpCategoriaVm? cat) => _goCoordinator.OpenViaticos(cat);
    private void AddGoItem(GastoOpCategoriaVm? cat) => _goCoordinator.AddGoItem(cat);
    private void RemoveGoItem(GastoOpItemVm? item) => _goCoordinator.RemoveGoItem(item);
    private void AddGoCategoria() => _goCoordinator.AddGoCategoria();
    private void RemoveGoCategoria(GastoOpCategoriaVm? cat) => _goCoordinator.RemoveGoCategoria(cat);
    private void ResetGoPlantilla() => _goCoordinator.ResetGoPlantilla();

    // ─── Ferretería ───────────────────────────────────────────────────────────
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
        internal set => SetProperty(ref ferreteriaPopupTitulo, value);
    }

    private string ferreteriaPopupNota = "";
    public string FerreteriaPopupNota
    {
        get => ferreteriaPopupNota;
        internal set => SetProperty(ref ferreteriaPopupNota, value);
    }

    private void ToggleAnalisis() => AnalisisAbierto = !AnalisisAbierto;
    private void ToggleFerreteriaDetalle() => _reportCoordinator.ToggleFerreteriaDetalle();
    private void ToggleCostGroup(CostoGrupoVm? grupo) => _reportCoordinator.ToggleCostGroup(grupo);

    // ─── Familias de Precio (Delegados) ───────────────────────────────────────
    public void MovePrecioFamilia(PrecioFamiliaChipVm? chip, string? targetFamilyKey) => _familyPricingCoordinator.MovePrecioFamilia(chip, targetFamilyKey);
    internal void RebuildPrecioFamiliaChips() => _familyPricingCoordinator.RebuildPrecioFamiliaChips();
    private void AbrirGestionFamilias() => _familyPricingCoordinator.AbrirGestionFamilias();
    private void AgregarFamiliaPresentacion() => _familyPricingCoordinator.AgregarFamiliaPresentacion();
    private void AbrirCatalogoFamilias(CoefFactorVm? fila) => _familyPricingCoordinator.AbrirCatalogoFamilias(fila);
    private void ElegirFamiliaPrecio(PrecioFamiliaPickVm? pick) => _familyPricingCoordinator.ElegirFamiliaPrecio(pick);
    internal void UpdateCoefChips() => _familyPricingCoordinator.UpdateCoefChips();

    public void CommitFamilyMarkup(string familyKey, decimal value) => _coeficientesCoordinator.CommitFamilyMarkup(familyKey, value);
    public void CommitFamilyDiscount(string familyKey, decimal pct) => _coeficientesCoordinator.CommitFamilyDiscount(familyKey, pct);

    // ─── Descuentos Negociados y Dotación Indirecta ───────────────────────────
    private void AddNegotiatedDiscount() => _closingPolicyCoordinator.AddNegotiatedDiscount();
    private void RemoveNegotiatedDiscount(NegotiatedDiscountVm? item) => _closingPolicyCoordinator.RemoveNegotiatedDiscount(item);
    private void AddStaffingRole() => _closingPolicyCoordinator.AddStaffingRole();
    private void RemoveStaffingRole(StaffingRoleVm? item) => _closingPolicyCoordinator.RemoveStaffingRole(item);

    // ─── Presets y Coeficientes ───────────────────────────────────────────────
    internal void SyncCoefFactors() => _coeficientesCoordinator.SyncCoefFactors();
    private void SaveCostosPreset() => _coeficientesCoordinator.SaveCostosPreset();
    private void RestoreCostosPreset() => _coeficientesCoordinator.RestoreCostosPreset();
    private void LoadConfigFromOtherProject() => _coeficientesCoordinator.LoadConfigFromOtherProject();
    private void FreezeCurrentFactors() => _coeficientesCoordinator.FreezeCurrentFactors();
    private void ResetCoeficientes() => _coeficientesCoordinator.ResetCoeficientes();
    private void LoadCoeficientesEstandar() => _coeficientesCoordinator.LoadCoeficientesEstandar();
    private void LoadTipicosCoeficientes() => _coeficientesCoordinator.LoadTipicosCoeficientes();
    private void ApplyCoeficientes() => _coeficientesCoordinator.ApplyCoeficientes();

    internal void RefreshCostOnly()
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

        _reportCoordinator.UpdateSummaryKpis(cost, project, valorUf);
        _reportCoordinator.RefreshFerreteriaFamilias(cost, data, project, valorUf);
        _reportCoordinator.FillCostRows(CostRows, cost, project, moneda, valorUf, tipoCambio);
        _closingPolicyCoordinator.RefreshClosingPolicySection(cost, moneda, valorUf, tipoCambio);
        OnPropertyChanged(nameof(CurrentPolicy));
        OnPropertyChanged(nameof(IsGastosGeneralesEditable));
        OnPropertyChanged(nameof(IsUtilidadEditable));
        OnPropertyChanged(nameof(IsFactoresFamiliaEditable));
        OnPropertyChanged(nameof(GastosGeneralesPolicyHint));
        OnPropertyChanged(nameof(UtilidadPolicyHint));
        _coeficientesCoordinator.AttachCoefPresentation();
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
