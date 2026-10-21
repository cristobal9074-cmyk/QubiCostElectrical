using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Review;

public enum ChecklistStatus { Pass, Warning, Fail }

internal static class FinalReviewIssueExtensions
{
    public static string CategoryTextFor(FinalReviewCategory category) => category switch
    {
        FinalReviewCategory.FormulaSyntaxError => "Error en f\u00f3rmula",
        FinalReviewCategory.EmptyPartida => "Partida vac\u00eda",
        FinalReviewCategory.MaterialWithoutPrice => "Material sin precio",
        FinalReviewCategory.AreaNotAssigned => "\u00c1rea no asignada",
        FinalReviewCategory.MissingApu => "Partida sin APU",
        FinalReviewCategory.ZeroQuantity => "Cantidad cero",
        FinalReviewCategory.MissingCurrencyUf => "UF / moneda no configurada",
        FinalReviewCategory.NegativeMargin => "Margen negativo",
        FinalReviewCategory.ZeroTotalSale => "Total venta cero",
        FinalReviewCategory.IvaInconsistent => "IVA inconsistente",
        FinalReviewCategory.ApuIncomplete => "APU incompleto",
        FinalReviewCategory.CommercialModelIncomplete => "Modelo comercial incompleto",
        FinalReviewCategory.PriceTraceability => "Trazabilidad de precio",
        FinalReviewCategory.MarkupInvalid => "Markup inv\u00e1lido",
        FinalReviewCategory.AntecedentePendienteMandante => "Pendiente del mandante",
        FinalReviewCategory.MedicionSinRegistroPlano => "Medici\u00f3n sin registro de plano",
        FinalReviewCategory.FechaCorteAntecedentesNoDefinida => "Fecha de corte no definida",
        FinalReviewCategory.PartidaSinValorizacionGlobal => "Sin valorización global",
        FinalReviewCategory.FamiliaSinFactorPresentacion => "Familia sin factor de presentación",
        _ => "Revisi\u00f3n"
    };
}

public sealed class ReviewIssueVm
{
    public ReviewIssueVm(FinalReviewIssue source)
    {
        Source = source;
    }

    public FinalReviewIssue Source { get; }
    public string SeverityText => Source.SeverityText;
    public string CategoryText => Source.CategoryText;
    public string ItemId => Source.ItemId;
    public string Item => Source.Item;
    public string Partida => Source.Partida;
    public string Codigo => Source.Codigo;
    public string Descripcion => Source.Descripcion;
    public int Count => Source.Count;
    public string Problem => Source.Problem;
    public string Recommendation => Source.Recommendation;
    public FinalReviewSeverity Severity => Source.Severity;
    public bool IsBlocker => Severity == FinalReviewSeverity.Blocker;
    public bool IsCritical => Severity == FinalReviewSeverity.Critical;
    public bool IsWarning => Severity == FinalReviewSeverity.Warning;
    public bool IsInfo => Severity == FinalReviewSeverity.Info;
}

public sealed class ChecklistItemVm
{
    private readonly Action<FinalReviewCategory?> onFilter;
    private readonly Func<bool> canFilter;

    public ChecklistItemVm(
        string text,
        ChecklistStatus status,
        int issueCount,
        FinalReviewCategory? category,
        Action<FinalReviewCategory?> onFilter,
        Func<bool> canFilter)
    {
        Text = text;
        Status = status;
        IssueCount = issueCount;
        Category = category;
        this.onFilter = onFilter;
        this.canFilter = canFilter;
        NavigateCommand = new RelayCommand(ExecuteFilter, () => canFilter() && issueCount > 0 && category.HasValue);
    }

    public string Text { get; }
    public ChecklistStatus Status { get; }
    public int IssueCount { get; }
    public FinalReviewCategory? Category { get; }
    public bool CanNavigate => IssueCount > 0 && Category.HasValue;
    public ICommand NavigateCommand { get; }

    public string Icon => Status switch
    {
        ChecklistStatus.Pass => "\u2713",
        ChecklistStatus.Warning => "\u25B2",
        _ => "\u2717"
    };

    private void ExecuteFilter()
    {
        onFilter(Category);
    }
}

public sealed partial class ReviewViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private static readonly string[] SeverityOptionsInternal =
    [
        "Todas",
        "Bloqueante",
        "Cr\u00edtico",
        "Advertencia",
        "Info"
    ];

    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly IFinalReviewService finalReview;
    private readonly ObservableCollection<ReviewIssueVm> issues = new();
    private readonly ObservableCollection<ChecklistItemVm> checklist = new();
    private readonly ObservableCollection<string> blockers = new();
    private readonly ObservableCollection<string> criticals = new();
    private readonly ObservableCollection<string> warnings = new();
    private readonly ObservableCollection<string> infos = new();
    private readonly ICollectionView issuesView;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand fixEquipmentClassificationCommand;
    private readonly RelayCommand goToIssueCommand;
    private readonly RelayCommand resetFilterCommand;
    private readonly RelayCommand clearSearchCommand;
    private readonly RelayCommand nextWarRoomIssueCommand;
    private readonly RelayCommand previousWarRoomIssueCommand;
    private readonly RelayCommand markResolvedLocalCommand;
    private readonly HashSet<string> locallyResolvedIds = new(StringComparer.OrdinalIgnoreCase);

    private string searchText = "";
    private string selectedSeverity = "Todas";
    private string summaryText = "0 problemas";
    private string projectSummary = "Sin proyecto abierto.";
    private string statusMessage = "Revisi\u00f3n no calculada.";
    private string readinessMessage = "Sin validaci\u00f3n.";
    private System.Windows.Threading.DispatcherTimer? sessionRefreshDebounceTimer;
    private string visibleIssuesLabel = "0 visibles";
    private bool showEmptyState;
    private bool isLoading;
    private bool readyToExport;
    private bool isWarRoomMode = true;
    private decimal confidencePct;
    private ReviewIssueVm? selectedIssue;
    private ReviewIssueVm? focusIssue;
    private FinalReviewCategory? activeCategoryFilter;
    private string filterLabel = "Todas las categor\u00edas";
}
