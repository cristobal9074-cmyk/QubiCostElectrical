using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Data;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Features.Project;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public sealed partial class DashboardViewModel : ViewModelBase, IRefreshablePage, IDisposable
{
    public DashboardViewModel(IShellNavigationHost shell, WpfAppHost host)
        : this(shell, host.Session, host.ProjectWorkspace, host.Audit, host.Currency, host.Dashboard, host.Services.ProjectRepository)
    {
        ufUpdater = new DashboardUfUpdateService(session, host.UndoService);
    }

    public DashboardViewModel(IShellNavigationHost shell, BudgetProjectSession session, IProjectWorkspaceService projectWorkspace, ProjectAuditRecorder audit, ICurrencyConversionService currency, DashboardPresenter dashboard, IProjectRepository projectRepo)
    {
        this.shell = shell;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.audit = audit;
        this.currency = currency;
        this.dashboard = dashboard;
        this.projectRepo = projectRepo;
        recentProjectsService = new RecentProjectsService();
        ufUpdater = new DashboardUfUpdateService(session, undo: null);
        KpiCards = new ObservableCollection<DashboardKpiCardVm>();
        CostShares = new ObservableCollection<DashboardCostShareVm>();
        WorkflowSteps = new ObservableCollection<DashboardWorkflowStepVm>();
        Alerts = new ObservableCollection<DashboardAlertVm>();
        OfferReadinessItems = new ObservableCollection<DashboardOfferReadinessItemVm>();
        TopPartidas = new ObservableCollection<DashboardPartidaVm>();
        RecentProjects = new ObservableCollection<DashboardRecentProjectVm>();
        QuickSummaryCards = new ObservableCollection<DashboardMetricVm>();
        QuickTemplates = new ObservableCollection<DashboardTemplateCardVm>();
        RecentAuditEntries = new ObservableCollection<ProjectAuditEntryVm>();
        SensitivityCurve = new ObservableCollection<DashboardSensitivityPoint>();

        PopulateQuickTemplates();

        NewProjectCommand = new RelayCommand(() => _ = shell.NewProjectAsync());
        CreateProjectFromTemplateCommand = new RelayCommand<string>(templateId => _ = shell.NewProjectWithTemplateAsync(templateId ?? "residential_building"));
        OpenFolderRecentProjectCommand = new RelayCommand<string>(path => OpenProjectFolder(path));
        CopyRecentProjectPathCommand = new RelayCommand<string>(path => CopyProjectPathToClipboard(path));
        TogglePinRecentProjectCommand = new RelayCommand<string>(path => TogglePin(path));
        RemoveRecentProjectCommand = new RelayCommand<string>(path => RemoveRecent(path));
        OpenProjectCommand = new RelayCommand(() => _ = shell.OpenProjectAsync());
        ActualizarUfCommand = new RelayCommand(() => _ = ActualizarUfAsync(), () => PuedeActualizarUf);
        continueTakeoffCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Takeoff), () => !ShowEmptyState);
        primaryCtaCommand = new RelayCommand(() =>
        {
            if (ShowEmptyState)
                _ = shell.OpenProjectAsync();
            else
                shell.RequestNavigate(primaryCtaRoute);
        });
        goToProjectCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Project), () => !ShowEmptyState);
        goToReviewCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Review), () => !ShowEmptyState);
        goToOfferDocumentsCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.OfferDocuments), () => !ShowEmptyState);
        goToItemizadoCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized), () => !ShowEmptyState);
        toggleDetailCommand = new RelayCommand(() => IsDetailExpanded = !IsDetailExpanded);
        goToVersionsCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Versions), () => !ShowEmptyState);
        GoToPriceBankCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Prices));
        GoToTemplatesCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Templates));
        GoToPreferencesCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Preferences));
        OpenRecentProjectCommand = new RelayCommand<string>(path => _ = shell.OpenRecentProjectAsync(path ?? string.Empty), path => !string.IsNullOrWhiteSpace(path));
        ExitApplicationCommand = new RelayCommand(ExitApplication);
        GoToPartidaCommand = new RelayCommand<DashboardPartidaVm>(p =>
        {
            if (p != null && !string.IsNullOrWhiteSpace(p.ItemId))
            {
                session.PendingItemizadoSelectionId = p.ItemId;
                shell.RequestNavigate(ShellRoute.Itemized);
            }
        });
        GoToApuPartidaCommand = new RelayCommand<DashboardPartidaVm>(p =>
        {
            if (p != null && !string.IsNullOrWhiteSpace(p.ItemId))
                shell.RequestNavigate(ShellRoute.Apu, new QubiCostElectrical.Wpf.Features.Apu.ApuNavContext(p.ItemId, p.Item, p.Partida));
        });
        SetParetoFilterCommand = new RelayCommand<string?>(f =>
        {
            SelectedParetoFilter = SelectedParetoFilter == f ? null : f;
        });

        ToggleThemeCommand = new RelayCommand(() =>
        {
            QubiCostElectrical.Wpf.DesignSystem.ThemeManager.Toggle();
            OnPropertyChanged(nameof(IsDarkMode));
        });

        ToggleFilterCommand = new RelayCommand<string>(category =>
        {
            SelectedCategoryFilter = SelectedCategoryFilter == category ? null : category;
        });
    }

    private void ScheduleSessionRefresh()
    {
        sessionRefreshDebounceTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        sessionRefreshDebounceTimer.Stop();
        sessionRefreshDebounceTimer.Tick -= OnSessionRefreshDebounceTick;
        sessionRefreshDebounceTimer.Tick += OnSessionRefreshDebounceTick;
        sessionRefreshDebounceTimer.Start();
    }

    private void OnSessionRefreshDebounceTick(object? sender, EventArgs e)
    {
        sessionRefreshDebounceTimer?.Stop();
        Refresh();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (sessionRefreshDebounceTimer != null)
        {
            sessionRefreshDebounceTimer.Stop();
            sessionRefreshDebounceTimer.Tick -= OnSessionRefreshDebounceTick;
            sessionRefreshDebounceTimer = null;
        }
    }

    private static void ExitApplication() => Application.Current.Shutdown();

    public ObservableCollection<DashboardKpiCardVm> KpiCards { get; }
    public ObservableCollection<DashboardCostShareVm> CostShares { get; }
    public ObservableCollection<DashboardWorkflowStepVm> WorkflowSteps { get; }
    public ObservableCollection<DashboardAlertVm> Alerts { get; }
    public ObservableCollection<DashboardOfferReadinessItemVm> OfferReadinessItems { get; }
    public ObservableCollection<DashboardPartidaVm> TopPartidas { get; }
    public ObservableCollection<DashboardRecentProjectVm> RecentProjects { get; }
    public ObservableCollection<DashboardMetricVm> QuickSummaryCards { get; }
    public string ActiveProjectLabel => shell.ActiveProjectLabel;
    public bool HasOpenProject => projectWorkspace.HasOpenProject;

    public bool ShowEmptyState
    {
        get => showEmptyState;
        private set
        {
            if (!SetProperty(ref showEmptyState, value)) return;
            continueTakeoffCommand.RaiseCanExecuteChanged();
            primaryCtaCommand.RaiseCanExecuteChanged();
            goToProjectCommand.RaiseCanExecuteChanged();
            goToReviewCommand.RaiseCanExecuteChanged();
            goToOfferDocumentsCommand.RaiseCanExecuteChanged();
            goToItemizadoCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public string NextStepTitle { get => nextStepTitle; private set => SetProperty(ref nextStepTitle, value); }
    public string NextStepDetail { get => nextStepDetail; private set => SetProperty(ref nextStepDetail, value); }
    public string PrimaryCtaLabel { get => primaryCtaLabel; private set => SetProperty(ref primaryCtaLabel, value); }
    public int BlockingAlertCount { get => blockingAlertCount; private set => SetProperty(ref blockingAlertCount, value); }
    public string PartidasReadyLabel { get => partidasReadyLabel; private set => SetProperty(ref partidasReadyLabel, value); }
    public double PartidasReadyPercent { get => partidasReadyPercent; private set => SetProperty(ref partidasReadyPercent, value); }
    public bool IsDetailExpanded { get => isDetailExpanded; set => SetProperty(ref isDetailExpanded, value); }
    public string TopAlertTitle { get => topAlertTitle; private set => SetProperty(ref topAlertTitle, value); }
    public string TopAlertDetail { get => topAlertDetail; private set => SetProperty(ref topAlertDetail, value); }
    public bool HasTopAlert { get => hasTopAlert; private set => SetProperty(ref hasTopAlert, value); }
    public string SubtitleText { get => subtitleText; private set => SetProperty(ref subtitleText, value); }
    public string PriceValidityLabel { get => priceValidityLabel; private set => SetProperty(ref priceValidityLabel, value); }
    public string PriceValidityCaption { get => priceValidityCaption; private set => SetProperty(ref priceValidityCaption, value); }
    public double PriceValidityPercent { get => priceValidityPercent; private set => SetProperty(ref priceValidityPercent, value); }
    public string VersionComparisonLabel { get => versionComparisonLabel; private set => SetProperty(ref versionComparisonLabel, value); }
    public string VersionDeltaCaption { get => versionDeltaCaption; private set => SetProperty(ref versionDeltaCaption, value); }
    public double VersionDirectCostDeltaPercent { get => versionDirectCostDeltaPercent; private set => SetProperty(ref versionDirectCostDeltaPercent, value); }
    public bool HasVersionComparison { get => hasVersionComparison; private set => SetProperty(ref hasVersionComparison, value); }
    public double OfferReadinessRingPercent { get => offerReadinessRingPercent; private set => SetProperty(ref offerReadinessRingPercent, value); }
    public bool HasRecentProjects { get => hasRecentProjects; private set => SetProperty(ref hasRecentProjects, value); }
    public Task? LastRefreshTask { get; private set; }
    public string CostMaterials { get => costMaterials; private set => SetProperty(ref costMaterials, value); }
    public string CostLabor { get => costLabor; private set => SetProperty(ref costLabor, value); }
    public string CostOtherDirect { get => costOtherDirect; private set => SetProperty(ref costOtherDirect, value); }
    public string CostDirect { get => costDirect; private set => SetProperty(ref costDirect, value); }
    public string CostTotal { get => costTotal; private set => SetProperty(ref costTotal, value); }
    public string CostUfM2 { get => costUfM2; private set => SetProperty(ref costUfM2, value); }
    public GridLength MaterialShareWidth { get => materialShareWidth; private set => SetProperty(ref materialShareWidth, value); }
    public GridLength LaborShareWidth { get => laborShareWidth; private set => SetProperty(ref laborShareWidth, value); }
    public GridLength OtherShareWidth { get => otherShareWidth; private set => SetProperty(ref otherShareWidth, value); }

    public double GastosGeneralesSimulatedPct
    {
        get => gastosGeneralesSimulatedPct;
        set
        {
            if (SetProperty(ref gastosGeneralesSimulatedPct, value))
            {
                if (!isRefreshing && HasOpenProject && session.GastosGeneralesPct != (decimal)value)
                {
                    session.GastosGeneralesPct = (decimal)value;
                    session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                    ScheduleSessionRefresh();
                }
            }
        }
    }

    public double UtilidadSimulatedPct
    {
        get => utilidadSimulatedPct;
        set
        {
            if (SetProperty(ref utilidadSimulatedPct, value))
            {
                if (!isRefreshing && HasOpenProject && session.UtilidadPct != (decimal)value)
                {
                    session.UtilidadPct = (decimal)value;
                    session.MarkDirty(ProjectDirtyScope.ProjectInfo);
                    ScheduleSessionRefresh();
                }
            }
        }
    }

    public string? SelectedCategoryFilter
    {
        get => selectedCategoryFilter;
        set
        {
            if (SetProperty(ref selectedCategoryFilter, value))
            {
                OnPropertyChanged(nameof(FilteredTopPartidas));
                OnPropertyChanged(nameof(IsFilteringMateriales));
                OnPropertyChanged(nameof(IsFilteringLabor));
                OnPropertyChanged(nameof(IsFilteringOtros));
                OnPropertyChanged(nameof(IsAnyFilterActive));
            }
        }
    }

    public IEnumerable<DashboardPartidaVm> FilteredTopPartidas
    {
        get
        {
            var query = (IEnumerable<DashboardPartidaVm>)TopPartidas;
            if (!string.IsNullOrEmpty(SelectedCategoryFilter))
                query = query.Where(p => p.Category == SelectedCategoryFilter);
            if (!string.IsNullOrEmpty(SelectedParetoFilter))
                query = query.Where(p => p.ParetoClass == SelectedParetoFilter);
            return query;
        }
    }

    public bool IsFilteringMateriales => SelectedCategoryFilter == "Materiales";
    public bool IsFilteringLabor => SelectedCategoryFilter == "Mano de Obra";
    public bool IsFilteringOtros => SelectedCategoryFilter == "Otros";
    public bool IsAnyFilterActive => !string.IsNullOrEmpty(SelectedCategoryFilter);

    public ICommand OpenProjectCommand { get; }
    public ICommand ContinueTakeoffCommand => continueTakeoffCommand;
    public ICommand PrimaryCtaCommand => primaryCtaCommand;
    public ICommand ToggleDetailCommand => toggleDetailCommand;
    public ICommand GoToProjectCommand => goToProjectCommand;
    public ICommand GoToReviewCommand => goToReviewCommand;
    public ICommand GoToOfferDocumentsCommand => goToOfferDocumentsCommand;
    public ICommand GoToItemizadoCommand => goToItemizadoCommand;
    public ICommand GoToVersionsCommand => goToVersionsCommand;
    public ICommand GoToPriceBankCommand { get; }
    public ICommand GoToTemplatesCommand { get; }
    public ICommand GoToPreferencesCommand { get; }
    public ICommand OpenRecentProjectCommand { get; }
    public ICommand NewProjectCommand { get; }
    public ICommand CreateProjectFromTemplateCommand { get; }
    public ICommand OpenFolderRecentProjectCommand { get; }
    public ICommand CopyRecentProjectPathCommand { get; }
    public ICommand TogglePinRecentProjectCommand { get; }
    public ICommand RemoveRecentProjectCommand { get; }
    public ICommand ExitApplicationCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ToggleFilterCommand { get; }
    public ObservableCollection<DashboardTemplateCardVm> QuickTemplates { get; }
    public ObservableCollection<ProjectAuditEntryVm> RecentAuditEntries { get; }
    public ObservableCollection<DashboardSensitivityPoint> SensitivityCurve { get; }
    public string MaterialImpact10PctLabel { get => materialImpact10PctLabel; private set => SetProperty(ref materialImpact10PctLabel, value); }
    public double ParetoAxisMaxPct { get => paretoAxisMaxPct; private set => SetProperty(ref paretoAxisMaxPct, value); }
    public int MajorAlertCount => Alerts.Count(a => a.SeverityLabel is "Crítico" or "Mayor");
    public bool HasBlockingAlerts => BlockingAlertCount > 0;
    public bool HasMajorAlerts => MajorAlertCount > 0;
    public bool IsDarkMode => QubiCostElectrical.Wpf.DesignSystem.ThemeManager.IsDarkMode;
    public string VersionLabel => $"v{ProductVersionInfo.ProductVersion}";
    public string SyncFooterLabel =>
        $"Sincronización correcta · Base SQLite v{ProductVersionInfo.DatabaseSchemaVersion} · Auditoría activa";

    public bool HasAlerts => Alerts.Count > 0;
    public bool HasTopPartidas => TopPartidas.Count > 0;
    public bool HasRecentAudit => RecentAuditEntries.Count > 0;
    public bool IsOfferReady => OfferReadinessRingPercent >= 99.5;
    public string ReadinessBadgeLabel => IsOfferReady ? "Listo" : "En curso";

    public void Refresh() => Load();

    public void Load()
    {
        IsLoading = true;
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);

            OnPropertyChanged(nameof(ActiveProjectLabel));
            OnPropertyChanged(nameof(HasOpenProject));
            OnPropertyChanged(nameof(IsDarkMode));
            ShowEmptyState = !projectWorkspace.HasOpenProject;
            if (ShowEmptyState)
            {
                KpiCards.Clear();
                CostShares.Clear();
                WorkflowSteps.Clear();
                Alerts.Clear();
                OfferReadinessItems.Clear();
                TopPartidas.Clear();
                ParetoSummary = DashboardParetoSummary.Empty;
                RecentAuditEntries.Clear();
                LoadLandingState();
                CostMaterials = CostLabor = CostOtherDirect = CostDirect = CostTotal = CostUfM2 = CostGo = CostMargin = MargenRealPctLabel = "—";
                MaterialShareWidth = new GridLength(0);
                LaborShareWidth = new GridLength(0);
                OtherShareWidth = new GridLength(0);
                GoShareWidth = new GridLength(0);
                MarginShareWidth = new GridLength(0);
                NextStepTitle = "Abra un proyecto";
                NextStepDetail = "Cree o abra un archivo .qce para ver el resumen ejecutivo.";
                PrimaryCtaLabel = "Abrir proyecto";
                primaryCtaRoute = ShellRoute.Dashboard;
                PartidasReadyLabel = "—";
                BlockingAlertCount = 0;
                HasTopAlert = false;
                TopAlertTitle = "";
                TopAlertDetail = "";
                OnPropertyChanged(nameof(HasAlerts));
                OnPropertyChanged(nameof(HasTopPartidas));
                OnPropertyChanged(nameof(HasRecentAudit));
                OnPropertyChanged(nameof(IsOfferReady));
                OnPropertyChanged(nameof(ReadinessBadgeLabel));
                primaryCtaCommand.RaiseCanExecuteChanged();
                return;
            }

            isRefreshing = true;
            try
            {
                var state = dashboard.BuildPageState();
                var project = state.Project;
                decimal valorUf = project.ValorUfClp > 0m ? project.ValorUfClp : PriceConversion.DefaultValorUfClp;

                string FormatClp(decimal clp) => $"$ {clp:N0}";
                string FormatUf(decimal clp)
                {
                    decimal uf = PriceConversion.ClpToUf(clp, valorUf);
                    return $"{uf:N2} UF";
                }
                string FormatDual(decimal clp)
                {
                    decimal uf = PriceConversion.ClpToUf(clp, valorUf);
                    return $"$ {clp:N0} · {uf:N2} UF";
                }
                string FormatMoney(decimal clp) =>
                    currency.FormatMoneyFromClp(clp, project.MonedaPresupuesto, valorUf, project.TipoCambioUsdClp);

                gastosGeneralesSimulatedPct = (double)project.GastosGeneralesPct;
                utilidadSimulatedPct = (double)project.UtilidadPct;
                OnPropertyChanged(nameof(GastosGeneralesSimulatedPct));
                OnPropertyChanged(nameof(UtilidadSimulatedPct));

                decimal totalVentaClp = state.Cost.TotalVentaClp;
                decimal totalVentaUf = PriceConversion.ClpToUf(totalVentaClp, valorUf);

                BuildWorkflow(state.Alerts);
                BuildAlerts(state.Alerts);
                BuildOfferReadiness(state.Alerts);
                BuildPriceValidity();
                BuildVersionComparison();
                UpdateNextStep(state.Alerts);
                OnPropertyChanged(nameof(HasAlerts));
                OnPropertyChanged(nameof(IsOfferReady));
                OnPropertyChanged(nameof(ReadinessBadgeLabel));
                OnPropertyChanged(nameof(MajorAlertCount));
                OnPropertyChanged(nameof(HasBlockingAlerts));
                OnPropertyChanged(nameof(HasMajorAlerts));

                KpiCards.Clear();
                foreach (var kpi in state.Executive.Kpis)
                {
                    string val, cap;
                    if (kpi.Title == "TOTAL VENTA")
                    {
                        bool isClpPref = string.Equals(project.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);
                        val = isClpPref ? FormatClp(totalVentaClp) : FormatUf(totalVentaClp);
                        cap = isClpPref ? $"{FormatUf(totalVentaClp)} · Valor oficial" : $"{FormatClp(totalVentaClp)} · Valor oficial";
                    }
                    else if (kpi.Title.EndsWith("/ m²", StringComparison.OrdinalIgnoreCase) || kpi.Title.Contains("m²"))
                    {
                        if (project.MetrosCuadrados > 0)
                        {
                            decimal clpM2 = totalVentaClp / project.MetrosCuadrados;
                            decimal ufM2 = PriceConversion.ClpToUf(clpM2, valorUf);
                            bool isClpPref = string.Equals(project.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);
                            val = isClpPref ? $"$ {clpM2:N0} / m²" : $"{ufM2:N2} UF / m²";
                            cap = isClpPref ? $"{ufM2:N2} UF/m² · Superficie" : $"$ {clpM2:N0} CLP/m² · Superficie";
                        }
                        else
                        {
                            val = "Configurar m²";
                            cap = "Costo comercial por superficie";
                        }
                    }
                    else
                    {
                        val = kpi.ValueKind == DashboardExecutiveKpiValueKind.Money ? FormatMoney(kpi.MoneyValueUf ?? 0m) : (kpi.TextValue ?? "");
                        cap = kpi.Caption;
                    }

                    int pendientes = kpi.Title == "PARTIDAS LISTAS"
                        ? state.Alerts.EmptyPartidas + state.Alerts.MaterialsWithoutPrice
                        : 0;
                    string chip = kpi.Title switch
                    {
                        "TOTAL VENTA" => HasVersionComparison
                            ? $"{VersionDirectCostDeltaPercent:+0.0;-0.0;0.0}% vs v. anterior"
                            : "Valor oficial",
                        "PARTIDAS LISTAS" => pendientes > 0 ? $"{pendientes} pendientes" : "Todo listo",
                        _ when kpi.Title.Contains("m²") => project.MetrosCuadrados > 0 ? $"{project.MetrosCuadrados:N0} m² útiles" : "Configurar m²",
                        _ => kpi.Accent == DashboardExecutiveKpiAccent.RiskLow ? "Riesgo bajo" : (kpi.TextValue ?? "")
                    };
                    string chipKind = kpi.Accent switch
                    {
                        DashboardExecutiveKpiAccent.RiskHigh => "danger",
                        DashboardExecutiveKpiAccent.RiskMedium or DashboardExecutiveKpiAccent.Orange => "warn",
                        DashboardExecutiveKpiAccent.RiskLow => "up",
                        _ => "mute"
                    };
                    if (kpi.Title == "TOTAL VENTA" && HasVersionComparison)
                        chipKind = VersionDirectCostDeltaPercent >= 0 ? "up" : "danger";
                    double gauge = kpi.Title == "PARTIDAS LISTAS" ? PartidasReadyPercent : 0;
                    string iconKind = kpi.Title == "TOTAL VENTA" ? "total"
                        : kpi.Title == "PARTIDAS LISTAS" ? "listas"
                        : kpi.Title.Contains("m²") ? "m2"
                        : "riesgo";

                    KpiCards.Add(new DashboardKpiCardVm
                    {
                        Title = kpi.Title,
                        Value = val,
                        Caption = cap,
                        IsPrimary = kpi.IsPrimary,
                        Accent = kpi.Accent,
                        AccentBrush = DashboardKpiBrushes.ForAccent(kpi.Accent),
                        AccentSoftBrush = DashboardKpiBrushes.ForAccentSoft(kpi.Accent),
                        ChipLabel = chip,
                        ChipKind = chipKind,
                        IconKind = iconKind,
                        ShowUfUpdateButton = kpi.Title == "TOTAL VENTA",
                        GaugePercent = gauge,
                        GaugeFillWidth = new GridLength(Math.Max(0.001, gauge), GridUnitType.Star),
                        GaugeRemainWidth = new GridLength(Math.Max(0.001, 100 - gauge), GridUnitType.Star)
                    });
                }

                ProjectClient = string.IsNullOrWhiteSpace(project.Cliente) ? "Sin cliente" : project.Cliente;
                ProjectTitle = string.IsNullOrWhiteSpace(project.Nombre) ? "Proyecto sin nombre" : project.Nombre;
                var pol = session.Data.ClosingPolicy?.Policy ?? ClosingPolicyType.CostPlusMarkup;
                ClosingPolicyLabel = pol switch
                {
                    ClosingPolicyType.CostPlusMarkup => "Costo + Markup",
                    ClosingPolicyType.TargetPrice => "Precio Objetivo",
                    ClosingPolicyType.LumpSumEpc => "Suma Alzada / EPC",
                    ClosingPolicyType.CostPlusFee => "Costo + Fee",
                    ClosingPolicyType.UnitPricePure => "Precio Unitario Puro",
                    ClosingPolicyType.GainsharePainshare => "Gainshare / Painshare",
                    _ => "Costo + Markup"
                };

                CostMaterials = FormatDual(state.Cost.MaterialesUf);
                CostLabor = FormatDual(state.Cost.ManoObraUf);
                var costDirect = Math.Max(0m, state.Cost.CostoDirectoUf);
                var otherDirect = Math.Max(0m, costDirect - state.Cost.MaterialesUf - state.Cost.ManoObraUf);
                CostOtherDirect = FormatDual(otherDirect);
                CostDirect = FormatDual(costDirect);
                CostTotal = FormatDual(state.Cost.TotalVentaUf);
                CostGo = FormatDual(state.Cost.GastosOperacionalesUf);
                decimal marginClp = Math.Max(0m, state.Cost.TotalVentaUf - state.Cost.CostoProyectoUf);
                CostMargin = FormatDual(marginClp);
                MargenRealPctLabel = $"{state.Cost.MargenRealPct:N1}%";

                CostUfM2 = project.MetrosCuadrados > 0
                    ? $"{PriceConversion.ClpToUf(totalVentaClp / project.MetrosCuadrados, valorUf):N2} UF/m² · $ {(totalVentaClp / project.MetrosCuadrados):N0}/m²"
                    : "Configure m² en datos del proyecto";
                UpdateCostDistribution(state.Cost.TotalVentaUf, state.Cost.MaterialesUf, state.Cost.ManoObraUf, state.Cost.GastosOperacionalesUf, marginClp);

                TopPartidas.Clear();
                double runningAccum = 0;
                int rank = 1;
                foreach (var row in state.Executive.TopPartidas.Take(10))
                {
                    double sharePct = costDirect > 0m ? (double)(row.TotalUf / costDirect * 100m) : 0;
                    runningAccum += sharePct;
                    string category = GetCategoryForPartida(row.Item, row.Partida);
                    decimal rowClp = row.TotalUf;
                    decimal rowUf = PriceConversion.ClpToUf(rowClp, valorUf);
                    var itemRow = session.Itemizado.FirstOrDefault(x => string.Equals(x.Item, row.Item, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Descripcion, row.Partida, StringComparison.OrdinalIgnoreCase));
                    string itemId = itemRow?.Id ?? "";
                    TopPartidas.Add(new DashboardPartidaVm
                    {
                        Rank = rank++,
                        ItemId = itemId,
                        Item = row.Item,
                        Partida = row.Partida,
                        TotalLabel = $"$ {rowClp:N0} ({rowUf:N1} UF)",
                        TotalClpLabel = $"$ {rowClp:N0}",
                        TotalUfLabel = $"{rowUf:N2} UF",
                        SharePct = Math.Clamp(sharePct, 0, 100),
                        AccumPct = Math.Clamp(runningAccum, 0, 100),
                        Category = category
                    });
                }
                ParetoSummary = DashboardParetoSummary.Calculate(TopPartidas, costDirect, valorUf);
                double maxShare = TopPartidas.Count > 0 ? TopPartidas.Max(p => p.SharePct) : 10;
                ParetoAxisMaxPct = Math.Max(5, Math.Ceiling(maxShare / 5) * 5);
                OnPropertyChanged(nameof(FilteredTopPartidas));
                OnPropertyChanged(nameof(HasTopPartidas));

                SensitivityCurve.Clear();
                foreach (var pt in state.Executive.SensitivityCurve)
                    SensitivityCurve.Add(pt);
                MaterialImpact10PctLabel = $"+10% → {state.Executive.MaterialImpact10Pct:+0.0;-0.0;0.0}%";

                RecentAuditEntries.Clear();
                foreach (var entry in audit.GetRecentEntries(5))
                {
                    RecentAuditEntries.Add(new ProjectAuditEntryVm(entry));
                }
                OnPropertyChanged(nameof(HasRecentAudit));

                // Actualiza el badge del shell con el total ya calculado (sin CreateSnapshot extra).
                shell.RefreshChrome();
            }
            finally
            {
                isRefreshing = false;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string GetCategoryForPartida(string item, string name)
    {
        string text = (name ?? "").ToLowerInvariant();
        if (text.Contains("conductor") || text.Contains("cable") || text.Contains("tubería") || text.Contains("tuberia") ||
            text.Contains("pvc") || text.Contains("canaliz") || text.Contains("artefacto") || text.Contains("led") ||
            text.Contains("enchufe") || text.Contains("interruptor"))
        {
            return "Materiales";
        }
        else if (text.Contains("montaje") || text.Contains("conexionado") || text.Contains("instalac") || text.Contains("mano de obra"))
        {
            return "Mano de Obra";
        }
        else
        {
            return "Otros";
        }
    }

    private void LoadLandingState()
    {
        var recent = recentProjectsService.GetProjects();
        var items = recent
            .Select(project => BuildRecentProject(project, project.IsMissing
                ? DashboardProjectStateKind.Unknown
                : DashboardProjectStateKind.Draft))
            .ToList();

        RecentProjects.Clear();
        foreach (var item in items)
            RecentProjects.Add(item);
        HasRecentProjects = RecentProjects.Count > 0;

        UpdateLandingSummary(items);
        LastRefreshTask = RefreshRecentProjectStatesAsync(recent, ++landingLoadGeneration);
    }

    private void UpdateLandingSummary(IReadOnlyList<DashboardRecentProjectVm> items)
    {
        QuickSummaryCards.Clear();
        AddSummaryCard("Proyectos totales", items.Count.ToString("N0"), "Según la lista de recientes", DashboardKpiBrushes.Blue, DashboardKpiBrushes.BlueSoft);
        AddSummaryCard("Completos", items.Count(x => x.StateKind == DashboardProjectStateKind.Complete).ToString("N0"), "Listos para entregar", DashboardKpiBrushes.Green, DashboardKpiBrushes.GreenSoft);
        AddSummaryCard("En progreso", items.Count(x => x.StateKind == DashboardProjectStateKind.InProgress).ToString("N0"), "Trabajo activo", DashboardKpiBrushes.Amber, DashboardKpiBrushes.AmberSoft);
        AddSummaryCard("Borradores", items.Count(x => x.StateKind is DashboardProjectStateKind.Draft or DashboardProjectStateKind.Unknown).ToString("N0"), "Pendientes de revisar", DashboardKpiBrushes.BlueDark, DashboardKpiBrushes.BlueSoft);
    }

    private async Task RefreshRecentProjectStatesAsync(IReadOnlyList<RecentProjectInfo> projects, int generation)
    {
        var resolved = await Task.Run(() => projects
            .Select(project => (Project: project, State: DetermineProjectState(project)))
            .ToList());

        if (!ShowEmptyState || generation != landingLoadGeneration)
            return;

        var updatedItems = resolved
            .Select(item => BuildRecentProject(item.Project, item.State))
            .ToList();

        RecentProjects.Clear();
        foreach (var item in updatedItems)
            RecentProjects.Add(item);
        HasRecentProjects = RecentProjects.Count > 0;
        UpdateLandingSummary(updatedItems);
    }

    private void AddSummaryCard(string title, string value, string caption, Brush accent, Brush accentSoft)
    {
        QuickSummaryCards.Add(new DashboardMetricVm
        {
            Title = title,
            Value = value,
            Caption = caption,
            AccentBrush = accent,
            AccentSoftBrush = accentSoft
        });
    }

    private DashboardRecentProjectVm BuildRecentProject(RecentProjectInfo project, DashboardProjectStateKind state)
    {
        var accent = state switch
        {
            DashboardProjectStateKind.Complete => DashboardKpiBrushes.Green,
            DashboardProjectStateKind.InProgress => DashboardKpiBrushes.Amber,
            DashboardProjectStateKind.Draft => DashboardKpiBrushes.Blue,
            _ => DashboardKpiBrushes.BlueDark
        };

        var soft = state switch
        {
            DashboardProjectStateKind.Complete => DashboardKpiBrushes.GreenSoft,
            DashboardProjectStateKind.InProgress => DashboardKpiBrushes.AmberSoft,
            DashboardProjectStateKind.Draft => DashboardKpiBrushes.BlueSoft,
            _ => DashboardKpiBrushes.BlueSoft
        };

        var statusText = project.IsMissing
            ? "No encontrado"
            : state switch
            {
                DashboardProjectStateKind.Complete => "Completo",
                DashboardProjectStateKind.InProgress => "En progreso",
                DashboardProjectStateKind.Draft => "Borrador",
                _ => "Sin estado"
            };

        return new DashboardRecentProjectVm
        {
            Name = string.IsNullOrWhiteSpace(project.Name) ? Path.GetFileNameWithoutExtension(project.Path) : project.Name,
            Path = project.Path,
            StatusText = statusText,
            StatusCaption = project.IsMissing ? "Ruta no disponible" : state switch
            {
                DashboardProjectStateKind.Complete => "Listo para cerrar",
                DashboardProjectStateKind.InProgress => "En desarrollo",
                DashboardProjectStateKind.Draft => "Pendiente de trabajo",
                _ => "Estado no disponible"
            },
            DisplayDate = project.DisplayDate,
            IsMissing = project.IsMissing,
            IsPinned = project.IsPinned,
            StateKind = state,
            AccentBrush = accent,
            AccentSoftBrush = soft,
            StatusForeground = accent,
            OpenCommand = new RelayCommand(() => _ = shell.OpenRecentProjectAsync(project.Path)),
            OpenFolderCommand = new RelayCommand(() => OpenProjectFolder(project.Path)),
            CopyPathCommand = new RelayCommand(() => CopyProjectPathToClipboard(project.Path)),
            TogglePinCommand = new RelayCommand(() => TogglePin(project.Path)),
            RemoveRecentCommand = new RelayCommand(() => RemoveRecent(project.Path))
        };
    }

    private void PopulateQuickTemplates()
    {
        QuickTemplates.Clear();
        foreach (var t in DashboardLandingCoordinator.CreateQuickTemplates(shell))
            QuickTemplates.Add(t);
    }

    private void OpenProjectFolder(string? path) => DashboardLandingCoordinator.OpenProjectFolder(path);

    private void CopyProjectPathToClipboard(string? path) => DashboardLandingCoordinator.CopyProjectPathToClipboard(path);

    private void TogglePin(string? path) => DashboardLandingCoordinator.TogglePin(recentProjectsService, path, LoadLandingState);

    private void RemoveRecent(string? path) => DashboardLandingCoordinator.RemoveRecent(recentProjectsService, shell, path, LoadLandingState);

    private DashboardProjectStateKind DetermineProjectState(RecentProjectInfo project)
    {
        if (project.IsMissing)
            return DashboardProjectStateKind.Unknown;

        try
        {
            var format = formatDetector.Detect(project.Path);
            // Solo se lee el campo "estado" (SQLite) en vez del snapshot completo (itemizado,
            // cubicación, materiales, APUs) para no bloquear la UI al listar recientes.
            string? estadoRaw = format switch
            {
                ProjectFileFormat.Sqlite => projectRepo.PeekSQLiteEstadoPresupuesto(project.Path),
                ProjectFileFormat.LegacyJson => projectRepo
                    .LoadProjectFile(project.Path, ProjectJsonSerializer.Options).Proyecto?.EstadoPresupuesto,
                _ => null
            };

            var estado = estadoRaw?.Trim();
            if (string.IsNullOrWhiteSpace(estado))
                return DashboardProjectStateKind.Draft;

            if (estado.Contains("complet", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("aprob", StringComparison.OrdinalIgnoreCase))
                return DashboardProjectStateKind.Complete;

            if (estado.Contains("progres", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("curso", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("revision", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("revisión", StringComparison.OrdinalIgnoreCase))
            {
                return DashboardProjectStateKind.InProgress;
            }

            if (estado.Contains("borr", StringComparison.OrdinalIgnoreCase) ||
                estado.Contains("draft", StringComparison.OrdinalIgnoreCase))
            {
                return DashboardProjectStateKind.Draft;
            }

            return DashboardProjectStateKind.Draft;
        }
        catch
        {
            return DashboardProjectStateKind.Unknown;
        }
    }

    private void UpdateCostDistribution(decimal totalVenta, decimal materials, decimal labor, decimal go, decimal margin)
    {
        var safeTotal = totalVenta <= 0m ? 1m : totalVenta;
        var matPct = Math.Max(0m, materials / safeTotal * 100m);
        var moPct = Math.Max(0m, labor / safeTotal * 100m);
        var goPct = Math.Max(0m, go / safeTotal * 100m);
        var mgPct = Math.Max(0m, margin / safeTotal * 100m);

        MaterialShareWidth = new GridLength((double)Math.Max(matPct, 0.1m), GridUnitType.Star);
        LaborShareWidth = new GridLength((double)Math.Max(moPct, 0.1m), GridUnitType.Star);
        GoShareWidth = new GridLength((double)Math.Max(goPct, 0.1m), GridUnitType.Star);
        MarginShareWidth = new GridLength((double)Math.Max(mgPct, 0.1m), GridUnitType.Star);

        CostShares.Clear();
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Materiales",
            AmountLabel = CostMaterials,
            PercentageLabel = $"{matPct:N0}%",
            PercentageValue = (double)matPct,
            Accent = DashboardExecutiveKpiAccent.Blue,
            AccentBrush = DashboardKpiBrushes.Blue,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft
        });
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Mano de obra",
            AmountLabel = CostLabor,
            PercentageLabel = $"{moPct:N0}%",
            PercentageValue = (double)moPct,
            Accent = DashboardExecutiveKpiAccent.RiskLow,
            AccentBrush = DashboardKpiBrushes.Green,
            AccentSoftBrush = DashboardKpiBrushes.GreenSoft
        });
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Gastos Op. (Faena)",
            AmountLabel = CostGo,
            PercentageLabel = $"{goPct:N0}%",
            PercentageValue = (double)goPct,
            Accent = DashboardExecutiveKpiAccent.Orange,
            AccentBrush = DashboardKpiBrushes.Amber,
            AccentSoftBrush = DashboardKpiBrushes.AmberSoft
        });
        CostShares.Add(new DashboardCostShareVm
        {
            Label = "Margen Bruto",
            AmountLabel = CostMargin,
            PercentageLabel = $"{mgPct:N0}%",
            PercentageValue = (double)mgPct,
            Accent = DashboardExecutiveKpiAccent.Primary,
            AccentBrush = DashboardKpiBrushes.BlueDark,
            AccentSoftBrush = DashboardKpiBrushes.BlueSoft
        });
    }

    private void BuildWorkflow(FinalReviewSummary review)
    {
        var project = session.ProjectInfo;
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        bool hasName = !string.IsNullOrWhiteSpace(project.Nombre) && project.Nombre != "Proyecto sin nombre";
        bool s1 = hasName;
        bool s2 = totalPartidas > 0;
        bool s3 = review.EmptyPartidas == 0 && totalPartidas > 0;
        bool s4 = review.MaterialsWithoutPrice == 0 && totalPartidas > 0;
        // "Revisión" exige que el checklist quede realmente limpio (sin bloqueantes ni críticos);
        // "Exportar" solo depende del gate real de exportación (sin bloqueantes), igual que en
        // Revisión final / Exportar. Así ambos pasos pueden completarse en momentos distintos.
        bool s5 = review.BlockingCount == 0 && review.CriticalCount == 0;
        bool s6 = review.ReadyToExport;

        var steps = new[]
        {
            ("Proyecto",  project.Nombre,                                                     s1, !s1,       ShellRoute.Project),
            ("Itemizado", $"{totalPartidas:N0} partidas",                                     s2, s1 && !s2, ShellRoute.Itemized),
            ("Cubicación",$"{Math.Max(0, totalPartidas - review.EmptyPartidas):N0} cubiertas",s3, s2 && !s3, ShellRoute.Takeoff),
            ("Precios",   review.MaterialsWithoutPrice > 0 ? $"{review.MaterialsWithoutPrice:N0} sin precio" : "Precios vigentes", s4, s3 && !s4, ShellRoute.Prices),
            ("Revisión",  $"{review.BlockingCount} bloqueantes",                              s5, s4 && !s5, ShellRoute.Review),
            ("Exportar",  review.ReadyToExport ? "Listo" : "Bloqueado",                      s6, s5 && !s6, ShellRoute.OfferDocuments),
        };

        WorkflowSteps.Clear();
        for (int i = 0; i < steps.Length; i++)
        {
            var (title, detail, complete, current, route) = steps[i];
            var r = route;
            WorkflowSteps.Add(new DashboardWorkflowStepVm
            {
                Index = i + 1,
                Title = title,
                Detail = detail,
                IsComplete = complete,
                IsCurrent = current,
                IsLast = (i == steps.Length - 1),
                NavigateCommand = new RelayCommand(() => shell.RequestNavigate(r), () => !ShowEmptyState)
            });
        }
    }

    private void BuildAlerts(FinalReviewSummary review)
    {
        Alerts.Clear();
        foreach (var issue in review.Issues.OrderByDescending(x => x.Severity).Take(12))
        {
            var category = issue.Category;
            Alerts.Add(new DashboardAlertVm
            {
                Title = issue.Problem,
                Detail = issue.Recommendation,
                SeverityLabel = issue.Severity switch
                {
                    FinalReviewSeverity.Blocker => "Bloqueante",
                    FinalReviewSeverity.Critical => "Crítico",
                    FinalReviewSeverity.Warning => "Mayor",
                    _ => "Info"
                },
                Category = category,
                NavigateCommand = new RelayCommand(() =>
                {
                    var target = ShellRouteMapper.FromReviewTarget(FinalReviewRouteResolver.Resolve(category));
                    shell.RequestNavigate(target);
                })
            });
        }
    }

    private void UpdateNextStep(FinalReviewSummary review)
    {
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        int readyPartidas = Math.Max(0, totalPartidas - review.EmptyPartidas);
        PartidasReadyLabel = $"{readyPartidas:N0} de {totalPartidas:N0} partidas listas";
        PartidasReadyPercent = totalPartidas == 0 ? 0 : Math.Min(100, readyPartidas * 100d / totalPartidas);
        BlockingAlertCount = review.BlockingCount;

        var current = WorkflowSteps.FirstOrDefault(s => s.IsCurrent)
                      ?? WorkflowSteps.LastOrDefault(s => !s.IsComplete)
                      ?? WorkflowSteps.LastOrDefault();

        if (current == null)
        {
            NextStepTitle = "Siguiente paso";
            NextStepDetail = PartidasReadyLabel;
            PrimaryCtaLabel = "Continuar";
            primaryCtaRoute = ShellRoute.Takeoff;
            SubtitleText = "Revise el flujo de producción para avanzar.";
        }
        else
        {
            NextStepTitle = current.IsComplete && WorkflowSteps.All(s => s.IsComplete)
                ? "Proyecto listo para exportar"
                : $"Siguiente paso: {current.Title.ToLowerInvariant()}";
            NextStepDetail = $"{PartidasReadyLabel} · {review.BlockingCount:N0} alertas bloqueantes";
            SubtitleText = review.BlockingCount > 0
                ? "Hay decisiones pendientes que afectan la oferta."
                : review.ReadyToExport
                    ? "El proyecto está listo para cerrar y exportar."
                    : "El proyecto avanza; continúe con el paso actual del flujo.";
            PrimaryCtaLabel = current.Title switch
            {
                "Proyecto" => "Completar proyecto",
                "Itemizado" => "Ir al itemizado",
                "Cubicación" => "Continuar cubicación",
                "APU/Costos" => "Completar APU",
                "Revisión" => "Ir a revisión",
                "Exportar" => "Cerrar oferta",
                _ => $"Continuar {current.Title.ToLowerInvariant()}"
            };
            primaryCtaRoute = current.Title switch
            {
                "Proyecto" => ShellRoute.Project,
                "Itemizado" => ShellRoute.Itemized,
                "Cubicación" => ShellRoute.Takeoff,
                "APU/Costos" => ShellRoute.Apu,
                "Revisión" => ShellRoute.Review,
                "Exportar" => ShellRoute.OfferDocuments,
                _ => ShellRoute.Takeoff
            };
        }

        var top = review.Issues.OrderByDescending(x => x.Severity).FirstOrDefault();
        if (top != null)
        {
            HasTopAlert = true;
            TopAlertTitle = top.Problem;
            TopAlertDetail = top.Recommendation;
        }
        else
        {
            HasTopAlert = false;
            TopAlertTitle = "";
            TopAlertDetail = "";
        }

        primaryCtaCommand.RaiseCanExecuteChanged();
    }

    private void BuildOfferReadiness(FinalReviewSummary review)
    {
        var project = session.ProjectInfo;
        int totalPartidas = session.Itemizado.Count(x => !x.EsTitulo);
        bool hasProjectData = !string.IsNullOrWhiteSpace(project.Nombre) && project.Nombre != "Proyecto sin nombre";
        bool hasItemizado = totalPartidas > 0;
        bool cubicacionOk = hasItemizado && review.EmptyPartidas == 0;
        int stalePrices = CountStalePrices();

        OfferReadinessItems.Clear();
        OfferReadinessItems.Add(new DashboardOfferReadinessItemVm
        {
            Label = "Datos del proyecto",
            IsComplete = hasProjectData,
            StatusBrush = hasProjectData ? DashboardKpiBrushes.Green : DashboardKpiBrushes.AmberRisk,
            NavigateCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Project), () => !ShowEmptyState)
        });
        OfferReadinessItems.Add(new DashboardOfferReadinessItemVm
        {
            Label = "Itemizado importado",
            IsComplete = hasItemizado,
            StatusBrush = hasItemizado ? DashboardKpiBrushes.Green : DashboardKpiBrushes.AmberRisk,
            NavigateCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized), () => !ShowEmptyState)
        });
        OfferReadinessItems.Add(new DashboardOfferReadinessItemVm
        {
            Label = "Cubicación completada",
            IsComplete = cubicacionOk,
            StatusBrush = cubicacionOk ? DashboardKpiBrushes.Green : DashboardKpiBrushes.AmberRisk,
            NavigateCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Takeoff), () => !ShowEmptyState)
        });
        OfferReadinessItems.Add(new DashboardOfferReadinessItemVm
        {
            Label = stalePrices > 0 ? $"Precios por confirmar ({stalePrices:N0})" : "Precios vigentes",
            IsComplete = stalePrices == 0,
            IsWarning = stalePrices > 0,
            StatusBrush = stalePrices > 0 ? DashboardKpiBrushes.AmberRisk : DashboardKpiBrushes.Green,
            NavigateCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Prices), () => !ShowEmptyState)
        });

        int completeCount = OfferReadinessItems.Count(i => i.IsComplete);
        OfferReadinessRingPercent = OfferReadinessItems.Count == 0
            ? 0
            : Math.Min(100, completeCount * 100d / OfferReadinessItems.Count);
        OnPropertyChanged(nameof(IsOfferReady));
        OnPropertyChanged(nameof(ReadinessBadgeLabel));
    }

    private int CountStalePrices()
        => session.Materiales.Count(m => m.RequiereRevisionPrecio);

    private void BuildPriceValidity()
    {
        var priced = session.Materiales.Where(m => m.FechaPrecio != default).ToList();
        if (priced.Count == 0)
        {
            PriceValidityLabel = "Sin datos";
            PriceValidityCaption = "Asigne fechas en banco de precios";
            PriceValidityPercent = 0;
            return;
        }

        int fresh = priced.Count(m => !m.RequiereRevisionPrecio);
        PriceValidityPercent = Math.Min(100, fresh * 100d / priced.Count);
        PriceValidityLabel = $"{fresh:N0}/{priced.Count:N0} vigentes";
        PriceValidityCaption = fresh == priced.Count
            ? "Precios dentro del umbral de 6 meses"
            : $"{priced.Count - fresh:N0} requieren revisión";
    }

    private void BuildVersionComparison()
    {
        var versions = session.Versiones
            .OrderBy(v => v.FechaCreacion)
            .ThenBy(v => v.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (versions.Count < 2)
        {
            HasVersionComparison = false;
            VersionComparisonLabel = "Sin versiones comparables";
            VersionDeltaCaption = "Guarde al menos dos versiones del presupuesto";
            VersionDirectCostDeltaPercent = 0;
            return;
        }

        var previous = versions[^2];
        var current = versions[^1];
        HasVersionComparison = true;
        VersionComparisonLabel = $"{previous.Codigo} → {current.Codigo}";

        if (previous.MontoVentaUf <= 0m)
        {
            VersionDeltaCaption = "Versión anterior sin monto registrado";
            VersionDirectCostDeltaPercent = 0;
            return;
        }

        var deltaPct = (current.MontoVentaUf - previous.MontoVentaUf) / previous.MontoVentaUf * 100m;
        VersionDirectCostDeltaPercent = Math.Min(100, Math.Abs((double)deltaPct));
        VersionDeltaCaption = deltaPct switch
        {
            > 0.05m => $"+{deltaPct:N1}% en monto de venta",
            < -0.05m => $"{deltaPct:N1}% en monto de venta",
            _ => "Sin variación relevante entre versiones"
        };
    }
}



public interface IRefreshablePage
{
    void Refresh();
}
