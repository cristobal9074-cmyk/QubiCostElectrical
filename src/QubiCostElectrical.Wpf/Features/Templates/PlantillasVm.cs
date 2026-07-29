using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Templates;

public sealed partial class PlantillasVm : ViewModelBase, IRefreshablePage, IPageWithPendingChanges
{
    private readonly IShellNavigationHost shell;
    private readonly WpfAppHost host;
    private readonly BudgetProjectSession session;
    private readonly IApuTemplateStore? templateStore;
    private readonly ConjuntoElectricoService conjuntoSvc = new();

    private ApuTemplateItemVm? selectedTemplate;
    private ConjuntoItemVm? selectedConjunto;
    private string templateFilter = "";
    private string conjuntoFilter = "";
    private bool showEmptyState = false;
    private string statusMessage = "";
    private int activeTab;
    private string? pendingSelectedConjuntoId;
    private bool hasPendingChanges;

    bool IPageWithPendingChanges.HasPendingChanges => hasPendingChanges;
    bool IPageWithPendingChanges.TryDiscardChanges() { hasPendingChanges = false; return true; }
}
