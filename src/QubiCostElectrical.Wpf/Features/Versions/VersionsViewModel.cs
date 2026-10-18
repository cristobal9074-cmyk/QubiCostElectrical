using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Versions;

public sealed partial class VersionsViewModel : SessionAwareViewModelBase, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService workspace;
    private readonly DashboardPresenter dashboard;
    private readonly IVersionComparisonService versionComparison;
    private readonly VersionsComparisonCoordinator comparisonCoordinator;
    private readonly IBudgetRevisionDiffPdfExporter? pdfExporter;
    private readonly IBudgetRevisionDiffExcelExporter? excelExporter;
    private readonly IFileDialogService? fileDialog;
    private readonly IFileOpenService? fileOpener;

    private readonly IItemizadoSpreadsheetReader? spreadsheetReader;
    private readonly ImportarItemizadoUseCase? importItemizadoUseCase;
    private readonly IProjectRepository? projectRepository;
    private readonly WpfUndoService? undo;

    public Func<string, bool> ConfirmDeleteVersion { get; set; } = msg => WpfDialogService.Confirm("Eliminar versión", msg, isWarning: true);

    private readonly ObservableCollection<VersionRowVm> versions = new();
    private readonly ObservableCollection<VersionComparisonRowVm> comparisonRows = new();
    private readonly ICollectionView versionsView;
    private readonly ICollectionView comparisonRowsView;
    private readonly RelayCommand refreshCommand;
    private readonly RelayCommand newCommand;
    private readonly RelayCommand activateCommand;
    private readonly RelayCommand renameCommand;
    private readonly RelayCommand deleteCommand;
    private readonly RelayCommand refreshComparisonCommand;
    private readonly RelayCommand goToItemCommand;
    private readonly RelayCommand exportPdfCommand;
    private readonly RelayCommand exportExcelCommand;
    private readonly RelayCommand createChangeOrderFromDiffCommand;
    private readonly RelayCommand compareWithExcelCommand;
    private readonly RelayCommand compareWithProjectFileCommand;
    private readonly RelayCommand saveAsNewVersionCommand;
    private readonly RelayCommand applyAddendaQuantitiesCommand;
    private readonly RelayCommand saveAddendaAsVersionCommand;
    private readonly RelayCommand clearSearchCommand;

    private VersionRowVm? selectedVersion;
    private VersionRowVm? selectedComparisonVersion;
    private VersionComparisonRowVm? selectedComparisonRow;
    private BudgetVersionComparisonSummary? currentSummary;
    private RevisionDiffFilterMode activeFilterMode = RevisionDiffFilterMode.Todos;
    private bool compareAgainstCurrent = true;
    private bool isAddendaComparison;
    private string addendaFileName = "";
    private IReadOnlyList<ItemizadoRow>? lastIncomingAddendaItems;
    private bool showEmptyState = true;
    private bool isLoading;
    private string summaryText = "0 versiones";
    private string activeVersionText = "Sin versión activa";
    private string statusMessage = "Gestión de versiones no calculada.";
    private string comparisonSummaryText = "Sin comparación";
    private string comparisonStatusText = "Selecciona una versión para comparar con el estado actual.";
    private string comparisonTotalsText = "—";
    private string comparisonAddedText = "0 nuevas";
    private string comparisonChangedText = "0 modificadas";
    private string comparisonRemovedText = "0 eliminadas";
    private string comparisonUnchangedText = "0 sin cambio";
    private string comparisonAdicionalesText = "+0.000 UF";
    private string comparisonDeductivosText = "0.000 UF";
    private string comparisonNetVariationText = "0.000 UF";
    private decimal comparisonDeltaUf;
    private decimal comparisonDeltaPct;
    private string comparisonModeHint = "Versión A = seleccionada · Versión B = estado actual";
    private string searchText = "";
}
