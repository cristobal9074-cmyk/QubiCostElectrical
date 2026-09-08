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

public enum DashboardProjectStateKind
{
    Draft,
    InProgress,
    Complete,
    Unknown
}

public sealed class DashboardKpiCardVm
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Caption { get; init; } = "";
    public bool IsPrimary { get; init; }
    public DashboardExecutiveKpiAccent Accent { get; init; }

    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
}

public sealed class DashboardWorkflowStepVm
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool IsComplete { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsLast { get; init; }
    public ICommand? NavigateCommand { get; init; }
}

public sealed class DashboardAlertVm
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string SeverityLabel { get; init; } = "";
    public FinalReviewCategory Category { get; init; }
    public ICommand NavigateCommand { get; init; } = null!;
}

public sealed class DashboardPartidaVm
{
    public string Item { get; init; } = "";
    public string Partida { get; init; } = "";
    public string TotalLabel { get; init; } = "";
    public string TotalClpLabel { get; init; } = "";
    public string TotalUfLabel { get; init; } = "";
    public double SharePct { get; init; }
    public string PercentLabel => $"{SharePct:N1}%";
    public GridLength FillWidth => new(Math.Max(0.001, SharePct), GridUnitType.Star);
    public GridLength RemainderWidth => new(Math.Max(0.001, 100 - SharePct), GridUnitType.Star);
    public string Category { get; init; } = "Otros";
}

public sealed class DashboardMetricVm
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string Caption { get; init; } = "";
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
}

public sealed class DashboardRecentProjectVm
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string DisplayPath => Path;
    public string StatusText { get; init; } = "";
    public string StatusCaption { get; init; } = "";
    public string DisplayDate { get; init; } = "";
    public bool IsMissing { get; init; }
    public DashboardProjectStateKind StateKind { get; init; }
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
    public Brush StatusForeground { get; init; } = DashboardKpiBrushes.Blue;
}

public sealed class DashboardCostShareVm
{
    public string Label { get; init; } = "";
    public string AmountLabel { get; init; } = "";
    public string PercentageLabel { get; init; } = "";
    public double PercentageValue { get; init; }
    public DashboardExecutiveKpiAccent Accent { get; init; }
    public Brush AccentBrush { get; init; } = DashboardKpiBrushes.Blue;
    public Brush AccentSoftBrush { get; init; } = DashboardKpiBrushes.BlueSoft;
}

public sealed class DashboardOfferReadinessItemVm
{
    public string Label { get; init; } = "";
    public bool IsComplete { get; init; }
    public bool IsWarning { get; init; }
    public Brush StatusBrush { get; init; } = DashboardKpiBrushes.Green;
}

public sealed class DashboardNavItemVm
{
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
}

internal static class DashboardKpiBrushes
{
    public static Brush Blue       => ThemeOrFallback("Brush.Blue", "#2563EB");
    public static Brush BlueDark   => ThemeOrFallback("Brush.BlueDark", "#1D4ED8");
    public static Brush Amber      => ThemeOrFallback("Brush.Amber", "#B45309");
    public static Brush DangerDark => ThemeOrFallback("Brush.DangerDark", "#B91C1C");
    public static Brush AmberRisk  => ThemeOrFallback("Brush.AmberRisk", "#D97706");
    public static Brush Green      => ThemeOrFallback("Brush.Green", "#15803D");
    public static Brush BlueSoft   => ThemeOrFallback("Brush.BlueSoft", "#EAF1FF");
    public static Brush AmberSoft  => ThemeOrFallback("Brush.AmberSoft", "#FFF5DD");
    public static Brush RedSoft    => ThemeOrFallback("Brush.RedSoft", "#FFF0F0");
    public static Brush GreenSoft  => ThemeOrFallback("Brush.GreenSoft", "#EAF8EF");

    public static Brush ForAccent(DashboardExecutiveKpiAccent accent) => accent switch
    {
        DashboardExecutiveKpiAccent.Primary    => Blue,
        DashboardExecutiveKpiAccent.Blue       => BlueDark,
        DashboardExecutiveKpiAccent.Orange     => Amber,
        DashboardExecutiveKpiAccent.RiskHigh   => DangerDark,
        DashboardExecutiveKpiAccent.RiskMedium => AmberRisk,
        DashboardExecutiveKpiAccent.RiskLow    => Green,
        _                                      => Blue
    };

    public static Brush ForAccentSoft(DashboardExecutiveKpiAccent accent) => accent switch
    {
        DashboardExecutiveKpiAccent.Primary    => BlueSoft,
        DashboardExecutiveKpiAccent.Blue       => BlueSoft,
        DashboardExecutiveKpiAccent.Orange     => AmberSoft,
        DashboardExecutiveKpiAccent.RiskHigh   => RedSoft,
        DashboardExecutiveKpiAccent.RiskMedium => AmberSoft,
        DashboardExecutiveKpiAccent.RiskLow    => GreenSoft,
        _                                      => BlueSoft
    };

    private static Brush ThemeOrFallback(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        brush.Freeze();
        return brush;
    }
}

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
    private int blockingAlertCount;
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
    private readonly RelayCommand goToVersionsCommand;
}
