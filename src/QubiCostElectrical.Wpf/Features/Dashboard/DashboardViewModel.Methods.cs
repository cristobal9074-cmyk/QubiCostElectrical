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
        : this(shell, host.Session, host.ProjectWorkspace, host.Audit, host.Currency, host.Dashboard, host.ProjectRepository)
    {
        ufUpdater = new DashboardUfUpdateService(session, host.UndoService);
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
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
        readinessCoordinator = new DashboardReadinessCoordinator(session, shell, currency);
        ufUpdater = new DashboardUfUpdateService(session, undo: null);
        KpiCards = new ObservableCollection<DashboardKpiCardVm>();
        KeyFigures = new ObservableCollection<DashboardKeyFigureVm>();
        CostShares = new ObservableCollection<DashboardCostShareVm>();
        WorkflowSteps = new ObservableCollection<DashboardWorkflowStepVm>();
        Alerts = new ObservableCollection<DashboardAlertVm>();
        OfferReadinessItems = new ObservableCollection<DashboardOfferReadinessItemVm>();
        TopPartidas = new ObservableCollection<DashboardPartidaVm>();
        RecentProjects = new ObservableCollection<DashboardRecentProjectVm>();
        BindingOperations.EnableCollectionSynchronization(RecentProjects, recentProjectsLock);
        QuickSummaryCards = new ObservableCollection<DashboardMetricVm>();
        QuickTemplates = new ObservableCollection<DashboardTemplateCardVm>();
        RecentAuditEntries = new ObservableCollection<ProjectAuditEntryVm>();
        SensitivityCurve = new ObservableCollection<DashboardSensitivityPoint>();

        PopulateQuickTemplates();

        NewProjectCommand = new RelayCommand(() => _ = shell.NewProjectAsync());
        CreateProjectFromTemplateCommand = new RelayCommand<string>(templateId => _ = shell.NewProjectWithTemplateAsync(templateId ?? "residential_building"));
        OpenFolderRecentProjectCommand = new RelayCommand<string>(path => DashboardLandingCoordinator.OpenProjectFolder(path));
        CopyRecentProjectPathCommand = new RelayCommand<string>(path => DashboardLandingCoordinator.CopyProjectPathToClipboard(path));
        TogglePinRecentProjectCommand = new RelayCommand<string>(path => DashboardLandingCoordinator.TogglePin(recentProjectsService, path, LoadLandingState));
        RemoveRecentProjectCommand = new RelayCommand<string>(path => DashboardLandingCoordinator.RemoveRecent(recentProjectsService, shell, path, LoadLandingState));
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
        ClearRecentSearchCommand = new RelayCommand(() => RecentSearchText = string.Empty);
        SetRecentFilterAllCommand = new RelayCommand(() => IsRecentFilterPinnedOnly = false);
        SetRecentFilterPinnedCommand = new RelayCommand(() => IsRecentFilterPinnedOnly = true);
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
    public ObservableCollection<DashboardKeyFigureVm> KeyFigures { get; }
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
    public ICommand ClearRecentSearchCommand { get; }
    public ICommand SetRecentFilterAllCommand { get; }
    public ICommand SetRecentFilterPinnedCommand { get; }

    private ICollectionView? filteredRecentProjects;
    public ICollectionView FilteredRecentProjects
    {
        get
        {
            if (filteredRecentProjects == null)
            {
                filteredRecentProjects = CollectionViewSource.GetDefaultView(RecentProjects);
                filteredRecentProjects.Filter = FilterRecentProjectsPredicate;
            }
            return filteredRecentProjects;
        }
    }

    private string recentSearchText = "";
    public string RecentSearchText
    {
        get => recentSearchText;
        set
        {
            if (SetProperty(ref recentSearchText, value))
            {
                FilteredRecentProjects?.Refresh();
                OnPropertyChanged(nameof(HasRecentSearchText));
                OnPropertyChanged(nameof(HasFilteredRecentProjects));
            }
        }
    }
    public bool HasRecentSearchText => !string.IsNullOrWhiteSpace(recentSearchText);

    private bool isRecentFilterPinnedOnly;
    public bool IsRecentFilterPinnedOnly
    {
        get => isRecentFilterPinnedOnly;
        set
        {
            if (SetProperty(ref isRecentFilterPinnedOnly, value))
            {
                FilteredRecentProjects?.Refresh();
                OnPropertyChanged(nameof(IsRecentFilterAll));
                OnPropertyChanged(nameof(HasFilteredRecentProjects));
            }
        }
    }
    public bool IsRecentFilterAll
    {
        get => !isRecentFilterPinnedOnly;
        set => IsRecentFilterPinnedOnly = !value;
    }

    public int PinnedProjectsCount => RecentProjects.Count(p => p.IsPinned);
    public int TotalRecentProjectsCount => RecentProjects.Count;
    public bool HasFilteredRecentProjects => FilteredRecentProjects != null && !FilteredRecentProjects.IsEmpty;

    private bool FilterRecentProjectsPredicate(object item)
    {
        if (item is not DashboardRecentProjectVm project) return false;

        if (isRecentFilterPinnedOnly && !project.IsPinned)
            return false;

        if (string.IsNullOrWhiteSpace(recentSearchText))
            return true;

        var search = recentSearchText.Trim();
        return (project.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (project.Path?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (project.StatusText?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

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
                KeyFigures.Clear();
                Composition = DashboardCompositionVm.Empty;
                CostShares.Clear();
                WorkflowSteps.Clear();
                Alerts.Clear();
                OfferReadinessItems.Clear();
                TopPartidas.Clear();
                ParetoSummary = DashboardParetoSummary.Empty;
                Waterfall = DashboardWaterfallVm.Empty;
                Readiness = DashboardReadinessContract.Empty;
                VerdictTitle = "Abra un proyecto";
                VerdictReason = "Cree o abra un archivo .qce para ver venta, margen y preparación de la oferta.";
                VerdictKind = "mute";
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
                string FormatDual(decimal clp)
                {
                    // Regla de moneda: en CLP solo pesos; en UF/USD el valor primario con su equivalente en pesos.
                    var pair = currency.FormatMoneyPairFromClp(clp, project.MonedaPresupuesto, valorUf, project.TipoCambioUsdClp);
                    return pair.Secondary.Length == 0 ? pair.Primary : $"{pair.Primary} ({pair.Secondary})";
                }
                string FormatMoney(decimal clp) =>
                    currency.FormatMoneyFromClp(clp, project.MonedaPresupuesto, valorUf, project.TipoCambioUsdClp);

                gastosGeneralesSimulatedPct = (double)project.GastosGeneralesPct;
                utilidadSimulatedPct = (double)project.UtilidadPct;
                OnPropertyChanged(nameof(GastosGeneralesSimulatedPct));
                OnPropertyChanged(nameof(UtilidadSimulatedPct));

                decimal totalVentaClp = state.Cost.TotalVentaClp;
                decimal totalVentaUf = PriceConversion.ClpToUf(totalVentaClp, valorUf);

                WorkflowSteps.Clear();
                foreach (var step in readinessCoordinator.BuildWorkflow(state.Alerts, ShowEmptyState))
                    WorkflowSteps.Add(step);

                Alerts.Clear();
                foreach (var alert in readinessCoordinator.BuildAlerts(state.Alerts))
                    Alerts.Add(alert);

                var readinessResult = readinessCoordinator.BuildReadiness(state.Alerts, ShowEmptyState);
                Readiness = readinessResult.Contract;
                PartidasReadyLabel = readinessResult.PartidasReadyLabel;
                PartidasReadyPercent = readinessResult.PartidasReadyPercent;
                BlockingAlertCount = readinessResult.BlockingAlertCount;
                OfferReadinessRingPercent = readinessResult.RingPercent;
                OfferReadinessItems.Clear();
                foreach (var item in readinessResult.Items)
                    OfferReadinessItems.Add(item);

                var priceValidity = readinessCoordinator.BuildPriceValidity();
                PriceValidityLabel = priceValidity.Label;
                PriceValidityCaption = priceValidity.Caption;
                PriceValidityPercent = priceValidity.Percent;

                var versionComparison = readinessCoordinator.BuildVersionComparison();
                HasVersionComparison = versionComparison.HasComparison;
                VersionComparisonLabel = versionComparison.Label;
                VersionDeltaCaption = versionComparison.Caption;
                VersionDirectCostDeltaPercent = versionComparison.DeltaPercent;

                var nextStep = readinessCoordinator.DetermineNextStep(state.Alerts, WorkflowSteps, PartidasReadyLabel);
                NextStepTitle = nextStep.NextStepTitle;
                NextStepDetail = nextStep.NextStepDetail;
                SubtitleText = nextStep.SubtitleText;
                PrimaryCtaLabel = nextStep.PrimaryCtaLabel;
                primaryCtaRoute = nextStep.PrimaryCtaRoute;
                HasTopAlert = nextStep.HasTopAlert;
                TopAlertTitle = nextStep.TopAlertTitle;
                TopAlertDetail = nextStep.TopAlertDetail;
                primaryCtaCommand.RaiseCanExecuteChanged();

                OnPropertyChanged(nameof(HasAlerts));
                OnPropertyChanged(nameof(IsOfferReady));
                OnPropertyChanged(nameof(ReadinessBadgeLabel));
                OnPropertyChanged(nameof(MajorAlertCount));
                OnPropertyChanged(nameof(HasBlockingAlerts));
                OnPropertyChanged(nameof(HasMajorAlerts));

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
                decimal goAndOther = otherDirect + state.Cost.GastosOperacionalesUf;
                CostGo = FormatDual(goAndOther);
                decimal marginClp = Math.Max(0m, state.Cost.TotalVentaNetoUf - (costDirect + state.Cost.GastosOperacionalesUf));
                CostMargin = FormatDual(marginClp);
                MargenRealPctLabel = $"{state.Cost.MargenRealPct:N1}%";

                var enClpResumen = string.Equals(project.MonedaPresupuesto, "CLP", StringComparison.OrdinalIgnoreCase);
                CostUfM2 = project.MetrosCuadrados > 0
                    ? enClpResumen
                        ? $"$ {totalVentaClp / project.MetrosCuadrados:N0}/m²"
                        : $"{PriceConversion.ClpToUf(totalVentaClp / project.MetrosCuadrados, valorUf):N3} UF/m² · $ {(totalVentaClp / project.MetrosCuadrados):N0}/m²"
                    : "Configure m² en datos del proyecto";

                var costDistribution = readinessCoordinator.BuildCostDistribution(
                    state.Cost.TotalVentaNetoUf, state.Cost.MaterialesUf, state.Cost.ManoObraUf, goAndOther, marginClp,
                    CostMaterials, CostLabor, CostGo, CostMargin);
                MaterialShareWidth = costDistribution.MaterialShareWidth;
                LaborShareWidth = costDistribution.LaborShareWidth;
                GoShareWidth = costDistribution.GoShareWidth;
                MarginShareWidth = costDistribution.MarginShareWidth;
                CostShares.Clear();
                foreach (var cs in costDistribution.Items)
                    CostShares.Add(cs);

                TopPartidas.Clear();
                double runningAccum = 0;
                int rank = 1;
                foreach (var row in state.Executive.TopPartidas.Take(10))
                {
                    double sharePct = costDirect > 0m ? (double)(row.TotalUf / costDirect * 100m) : 0;
                    runningAccum += sharePct;
                    string category = DashboardReadinessCoordinator.GetCategoryForPartida(row.Item, row.Partida);
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
                        TotalLabel = FormatDual(rowClp),
                        TotalShortLabel = FormatMoney(rowClp),
                        TotalClpLabel = FormatClp(rowClp),
                        TotalUfLabel = $"{rowUf:N3} UF",
                        SharePct = Math.Clamp(sharePct, 0, 100),
                        AccumPct = Math.Clamp(runningAccum, 0, 100),
                        Category = category
                    });
                }
                ParetoSummary = DashboardParetoSummary.Calculate(TopPartidas, costDirect, valorUf);
                OnPropertyChanged(nameof(FilteredCockpitPartidasCountLabel));
                OnPropertyChanged(nameof(FilteredCockpitPartidas));
                double maxShare = TopPartidas.Count > 0 ? TopPartidas.Max(p => p.SharePct) : 10;
                ParetoAxisMaxPct = Math.Max(5, Math.Ceiling(maxShare / 5) * 5);
                OnPropertyChanged(nameof(FilteredTopPartidas));
                OnPropertyChanged(nameof(HasTopPartidas));

                KpiCards.Clear();
                foreach (var card in readinessCoordinator.BuildExecutiveKpis(
                    state, valorUf, totalVentaClp, HasVersionComparison, VersionDirectCostDeltaPercent,
                    Readiness.Pendientes, PartidasReadyPercent, MargenRealPctLabel, CostMargin, ParetoSummary))
                {
                    KpiCards.Add(card);
                }


                // Cascada de precio: un único recorrido auditable del costo de ejecución al total.
                // Eje de la cascada: mismo formato compacto que los importes, en la moneda del
                // proyecto (las barras y el eje deben hablar del mismo número).
                string FormatAxisMoney(decimal value)
                {
                    if (value <= 0m) return "0";
                    if (enClpResumen) return DashboardMoneyShort.Format(value);
                    if (string.Equals(project.MonedaPresupuesto, "UF", StringComparison.OrdinalIgnoreCase))
                        return DashboardMoneyShort.FormatUf(PriceConversion.ClpToUf(value, valorUf));
                    return FormatMoney(value);
                }

                Waterfall = DashboardCockpitComposer.BuildWaterfall(
                    state.Cost,
                    new DashboardWaterfallPolicy(
                        project.RiesgoPct,
                        project.GastosGeneralesPct,
                        project.UtilidadPct,
                        project.ValorAgregadoEquiposPct,
                        project.DescuentoPct,
                        project.IvaPct <= 0m ? 19m : project.IvaPct,
                        project.IncluirIvaEnTotal),
                    clp => enClpResumen ? DashboardMoneyShort.Format(clp) : FormatMoney(clp),
                    FormatAxisMoney);

                UpdateVerdict(state.Executive.MaterialImpact10Pct);

                SensitivityCurve.Clear();
                foreach (var pt in state.Executive.SensitivityCurve)
                    SensitivityCurve.Add(pt);
                MaterialImpact10PctLabel = $"+10% → {state.Executive.MaterialImpact10Pct:+0.0;-0.0;0.0}%";

                // Panel de cifras clave y composición del precio.
                BuildKeyFigures(state, project, valorUf, totalVentaClp, totalVentaUf, enClpResumen,
                    FormatMoney, FormatAxisMoney);

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

    private void LoadLandingState()
    {
        var recent = recentProjectsService.GetProjects();
        var items = recent
            .Select(project => DashboardLandingCoordinator.BuildRecentProject(project, project.IsMissing
                ? DashboardProjectStateKind.Unknown
                : DashboardProjectStateKind.Draft, shell, recentProjectsService, LoadLandingState))
            .ToList();

        lock (recentProjectsLock)
        {
            RecentProjects.Clear();
            foreach (var item in items)
                RecentProjects.Add(item);
        }
        HasRecentProjects = RecentProjects.Count > 0;
        filteredRecentProjects?.Refresh();
        OnPropertyChanged(nameof(PinnedProjectsCount));
        OnPropertyChanged(nameof(TotalRecentProjectsCount));
        OnPropertyChanged(nameof(HasFilteredRecentProjects));

        UpdateLandingSummary(items);
        LastRefreshTask = RefreshRecentProjectStatesAsync(recent, ++landingLoadGeneration);
    }

    private void UpdateLandingSummary(IReadOnlyList<DashboardRecentProjectVm> items)
    {
        QuickSummaryCards.Clear();
        foreach (var card in DashboardLandingCoordinator.CreateLandingSummary(items))
            QuickSummaryCards.Add(card);
    }

    private async Task RefreshRecentProjectStatesAsync(IReadOnlyList<RecentProjectInfo> projects, int generation)
    {
        var resolved = await Task.Run(() => projects
            .Select(project => (Project: project, State: DashboardLandingCoordinator.DetermineProjectState(project, formatDetector, projectRepo)))
            .ToList());

        if (!ShowEmptyState || generation != landingLoadGeneration)
            return;

        var updatedItems = resolved
            .Select(item => DashboardLandingCoordinator.BuildRecentProject(item.Project, item.State, shell, recentProjectsService, LoadLandingState))
            .ToList();

        void ApplyUpdates()
        {
            lock (recentProjectsLock)
            {
                RecentProjects.Clear();
                foreach (var item in updatedItems)
                    RecentProjects.Add(item);
            }
            HasRecentProjects = RecentProjects.Count > 0;
            filteredRecentProjects?.Refresh();
            OnPropertyChanged(nameof(PinnedProjectsCount));
            OnPropertyChanged(nameof(TotalRecentProjectsCount));
            OnPropertyChanged(nameof(HasFilteredRecentProjects));
            UpdateLandingSummary(updatedItems);
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyUpdates);
        }
        else
        {
            ApplyUpdates();
        }
    }

    private void PopulateQuickTemplates()
    {
        QuickTemplates.Clear();
        foreach (var t in DashboardLandingCoordinator.CreateQuickTemplates(shell))
            QuickTemplates.Add(t);
    }

    /// <summary>
    /// Veredicto de la oferta en una línea. Se deriva del contrato de preparación y del impacto de
    /// materiales sobre la venta neta, así que siempre coincide con el KPI y con el anillo.
    /// </summary>
    private void UpdateVerdict(decimal materialImpact10Pct)
    {
        string scenario = Readiness.TotalPartidas > 0
            ? DashboardCockpitComposer.BuildScenarioLabel(10m, materialImpact10Pct)
            : "";
        var (title, reason, kind) = DashboardCockpitComposer.BuildVerdict(Readiness, scenario);
        VerdictTitle = title;
        VerdictReason = reason;
        VerdictKind = kind;
    }
}




public interface IRefreshablePage
{
    void Refresh();
}
