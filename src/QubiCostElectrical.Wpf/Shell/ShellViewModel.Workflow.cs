using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

partial class ShellViewModel
{
    private readonly ObservableCollection<ShellWorkflowStepVm> workflowSteps = new();

    public ObservableCollection<ShellWorkflowStepVm> WorkflowSteps => workflowSteps;

    /// <summary>Módulos de trabajo a pantalla completa (sin context bar ni stepper global).</summary>
    public bool IsWorkstationMode => CurrentRoute switch
    {
        ShellRoute.Itemized => true,
        ShellRoute.Takeoff => true,
        ShellRoute.CubicacionSc => true,
        ShellRoute.Apu => true,
        _ => false
    };

    public bool ShowContextBar => HasOpenProject && !IsLandingScreen && !IsWorkstationMode
        && CurrentRoute != ShellRoute.Project
        && CurrentRoute != ShellRoute.Documents
        && CurrentRoute != ShellRoute.Costo
        && CurrentRoute != ShellRoute.Epp;

    /// <summary>
    /// Stepper de flujo de trabajo: visible cuando hay un proyecto abierto.
    /// ShellWindow.xaml enlaza Visibility a este flag; RefreshWorkflowSteps dispara el PropertyChanged
    /// en cada apertura/cierre de proyecto a través de RefreshChrome.
    /// </summary>
    public bool ShowWorkflowStepper => projectWorkspace.HasOpenProject;

    public bool ShowContextPageTitle => CurrentRoute switch
    {
        ShellRoute.Itemized => false,
        ShellRoute.Takeoff => false,
        ShellRoute.CubicacionSc => false,
        ShellRoute.Apu => false,
        ShellRoute.Review => false,
        ShellRoute.OfferDocuments => false,
        ShellRoute.Project => false,
        ShellRoute.Dashboard => false,
        ShellRoute.Costo => false,
        ShellRoute.Epp => false,
        ShellRoute.ManoDeObra => false,
        ShellRoute.Quotes => false,
        ShellRoute.Solicitudes => false,
        ShellRoute.FloorSummary => false,
        _ => true
    };

    /// <summary>
    /// Identidad izquierda de la context bar (breadcrumb / título).
    /// En Proyecto el hero de la página ya carga nombre + chips; se oculta para no duplicar.
    /// </summary>
    public bool ShowContextIdentity => CurrentRoute != ShellRoute.Project && !IsWorkstationMode;

    // El Shell nunca reserva un margen externo: cada pantalla controla su propio
    // espacio de lectura y el ScrollViewer se mantiene alineado al borde derecho.
    public bool UseCompactPagePadding => true;

    private static bool IsWorkflowFlowRoute(ShellRoute route) => route switch
    {
        ShellRoute.Project => true,
        ShellRoute.Itemized => true,
        ShellRoute.Takeoff => true,
        ShellRoute.CubicacionSc => true,
        ShellRoute.Apu => true,
        ShellRoute.Quotes => true,
        ShellRoute.Purchases => true,
        ShellRoute.Review => true,
        ShellRoute.OfferDocuments => true,
        _ => false
    };

    private void RefreshWorkflowSteps()
    {
        workflowSteps.Clear();

        if (!projectWorkspace.HasOpenProject)
        {
            OnPropertyChanged(nameof(ShowWorkflowStepper));
            OnPropertyChanged(nameof(ShowContextBar));
            return;
        }

        // Misma regla que AlertCount en RefreshChrome: no BuildAlerts aquí (Session.Changed
        // es de alta frecuencia). Usar cache del dashboard; sin cache, resumen vacío.
        var review = dashboard.LastAlertSummary ?? new FinalReviewSummary();
        var definitions = ShellWorkflowLogic.BuildSteps(
            CurrentRoute,
            review,
            session.ProjectInfo,
            session.Itemizado,
            session.CubicacionSC);

        foreach (var def in definitions)
        {
            var route = def.NavigateRoute;
            workflowSteps.Add(new ShellWorkflowStepVm
            {
                Title = def.Title,
                Detail = def.Detail,
                StepNumber = def.StepNumber,
                IsComplete = def.IsComplete,
                IsActive = def.IsActive,
                IsAuxiliary = def.IsAuxiliary,
                NavigateCommand = new RelayCommand(() => RequestNavigate(route))
            });
        }

        OnPropertyChanged(nameof(ShowWorkflowStepper));
        OnPropertyChanged(nameof(ShowContextBar));
    }
}
