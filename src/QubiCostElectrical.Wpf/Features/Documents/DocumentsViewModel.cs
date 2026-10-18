using System.Collections.Generic;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Documents;

public sealed partial class DocumentsViewModel : ViewModelBase, IPageWithPendingChanges, IRefreshablePage
{
    private readonly IShellNavigationHost shell;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly IPermissionService permissions;
    private readonly IDocumentsWorkspaceService documents;
    private readonly DocumentsPreviewCoordinator previewCoordinator;
    private readonly List<AntecedenteRowVm> allRows = new();
    private AntecedenteRowVm? selectedRow;
    private string searchText = "";
    private string tipoFilter = "Todos";
    private bool sinFechaCorte = true;
    private DateTime? fechaCorte;
    private string summaryText = "";
    private string statusMessage = "";
    private bool showEmptyState = true;
    private bool isDirty;
    private string selectedNavFilter = "Todos";
    private string? selectedPlanoDisciplina;
    private string inspectionFilter = "Todos";
}
