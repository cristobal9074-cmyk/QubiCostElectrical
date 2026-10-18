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
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly ProjectAuditRecorder audit;
    private readonly ICurrencyConversionService currency;
    private readonly DashboardPresenter dashboard;
    private readonly IProjectRepository projectRepo;
    private readonly RecentProjectsService recentProjectsService;
    private readonly ProjectFileFormatDetector formatDetector = new();
    private readonly object recentProjectsLock = new();
    private readonly DashboardReadinessCoordinator readinessCoordinator;
    private bool showEmptyState = true;
    private bool isLoading;
    private string costMaterials = "—";
    private string costLabor = "—";
    private string costOtherDirect = "—";
    private string costDirect = "—";
    private string costTotal = "—";
    private string costUfM2 = "—";
    private GridLength materialShareWidth = new(0);
    private GridLength laborShareWidth = new(0);
    private GridLength otherShareWidth = new(0);
    private bool hasRecentProjects;
    private int landingLoadGeneration;
    private readonly RelayCommand continueTakeoffCommand;
    private readonly RelayCommand primaryCtaCommand;
    private readonly RelayCommand goToProjectCommand;
    private readonly RelayCommand goToReviewCommand;
    private readonly RelayCommand goToOfferDocumentsCommand;
    private readonly RelayCommand goToItemizadoCommand;
    private readonly RelayCommand toggleDetailCommand;
    private double gastosGeneralesSimulatedPct;
    private double utilidadSimulatedPct;
    private bool isRefreshing;
    private string? selectedCategoryFilter;
    private string nextStepTitle = "Siguiente paso";
    private System.Windows.Threading.DispatcherTimer? sessionRefreshDebounceTimer;
    private bool disposed;

    public string GreetingText
    {
        get
        {
            var user = QubiCostElectrical.Wpf.Auth.CollaborationSessionBridge.CurrentUserContext();
            var name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? "Buenos días" : $"Buenos días, {name}";
        }
    }
    private string nextStepDetail = "";
    private string primaryCtaLabel = "Continuar";
    private ShellRoute primaryCtaRoute = ShellRoute.Takeoff;
    private string partidasReadyLabel = "—";
    private double partidasReadyPercent;
    private bool isDetailExpanded;
    private string topAlertTitle = "";
    private string topAlertDetail = "";
    private bool hasTopAlert;
    private string subtitleText = "";
    private string priceValidityLabel = "—";
    private string priceValidityCaption = "";
    private double priceValidityPercent;
    private string versionComparisonLabel = "Sin versiones comparables";
    private string versionDeltaCaption = "Guarde al menos dos versiones";
    private double versionDirectCostDeltaPercent;
    private bool hasVersionComparison;
    private double offerReadinessRingPercent;
    private string materialImpact10PctLabel = "+10% → —";
    private double paretoAxisMaxPct = 10;
    private readonly RelayCommand goToVersionsCommand;

    private string closingPolicyLabel = "Costo + Markup";
    public string ClosingPolicyLabel { get => closingPolicyLabel; private set => SetProperty(ref closingPolicyLabel, value); }
    private string projectClient = "";
    public string ProjectClient { get => projectClient; private set => SetProperty(ref projectClient, value); }
    private string projectTitle = "";
    public string ProjectTitle { get => projectTitle; private set => SetProperty(ref projectTitle, value); }
    private string costGo = "—";
    public string CostGo { get => costGo; private set => SetProperty(ref costGo, value); }
    private string costMargin = "—";
    public string CostMargin { get => costMargin; private set => SetProperty(ref costMargin, value); }
    private string margenRealPctLabel = "—";
    public string MargenRealPctLabel { get => margenRealPctLabel; private set => SetProperty(ref margenRealPctLabel, value); }
    private GridLength goShareWidth = new(0);
    public GridLength GoShareWidth { get => goShareWidth; private set => SetProperty(ref goShareWidth, value); }
    private GridLength marginShareWidth = new(0);
    public GridLength MarginShareWidth { get => marginShareWidth; private set => SetProperty(ref marginShareWidth, value); }
    private DashboardParetoSummary paretoSummary = DashboardParetoSummary.Empty;
    public DashboardParetoSummary ParetoSummary { get => paretoSummary; private set => SetProperty(ref paretoSummary, value); }

    private DashboardWaterfallVm waterfall = DashboardWaterfallVm.Empty;
    /// <summary>Cascada de precio: un solo origen de datos para el precio de la oferta.</summary>
    public DashboardWaterfallVm Waterfall { get => waterfall; private set => SetProperty(ref waterfall, value); }

    private DashboardReadinessContract readiness = DashboardReadinessContract.Empty;
    /// <summary>Contrato único de preparación: KPI, barra, anillo, checklist y veredicto.</summary>
    public DashboardReadinessContract Readiness { get => readiness; private set => SetProperty(ref readiness, value); }

    private string verdictTitle = "Sin itemizado";
    public string VerdictTitle { get => verdictTitle; private set => SetProperty(ref verdictTitle, value); }

    private string verdictReason = "";
    public string VerdictReason { get => verdictReason; private set => SetProperty(ref verdictReason, value); }

    /// <summary>ok | warn | bad | mute — controla el tono del veredicto.</summary>
    private string verdictKind = "mute";
    public string VerdictKind
    {
        get => verdictKind;
        private set
        {
            if (SetProperty(ref verdictKind, value))
            {
                OnPropertyChanged(nameof(IsVerdictOk));
                OnPropertyChanged(nameof(IsVerdictWarn));
                OnPropertyChanged(nameof(IsVerdictBad));
            }
        }
    }

    public bool IsVerdictOk => verdictKind == "ok";
    public bool IsVerdictWarn => verdictKind == "warn";
    public bool IsVerdictBad => verdictKind == "bad";

    private string? selectedParetoFilter;
    public string? SelectedParetoFilter
    {
        get => selectedParetoFilter;
        set
        {
            if (SetProperty(ref selectedParetoFilter, value))
            {
                OnPropertyChanged(nameof(FilteredTopPartidas));
                OnPropertyChanged(nameof(IsParetoFilterAll));
                OnPropertyChanged(nameof(IsParetoFilterA));
                OnPropertyChanged(nameof(IsParetoFilterB));
                OnPropertyChanged(nameof(IsParetoFilterC));
            }
        }
    }
    public bool IsParetoFilterAll => string.IsNullOrEmpty(selectedParetoFilter);
    public bool IsParetoFilterA => selectedParetoFilter == "A";
    public bool IsParetoFilterB => selectedParetoFilter == "B";
    public bool IsParetoFilterC => selectedParetoFilter == "C";
    public ICommand GoToPartidaCommand { get; }
    public ICommand GoToApuPartidaCommand { get; }
    public ICommand SetParetoFilterCommand { get; }
}
