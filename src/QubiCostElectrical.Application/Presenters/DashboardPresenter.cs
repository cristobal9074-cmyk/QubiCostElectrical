namespace QubiCostElectrical;

public sealed record DashboardPageState(
    ProjectInfoRow Project,
    CostBreakdown Cost,
    FinalReviewSummary Alerts,
    PartidaCompletionSummary Completion,
    DashboardExecutiveViewModel Executive,
    bool IsDirty,
    int StateVersion,
    DateTime BuiltAt);

public sealed record DashboardPresenterServices(
    IBudgetCalculationService BudgetCalculation,
    IPartidaCompletionService PartidaCompletion,
    IFinalReviewService FinalReview,
    DashboardExecutiveService Executive);

public sealed class DashboardPresenter
{
    private readonly DashboardPresenterServices services;
    private readonly BudgetProjectSession? session;

    public DashboardPresenter(DashboardPresenterServices services) : this(services, null)
    {
    }

    public DashboardPresenter(DashboardPresenterServices services, BudgetProjectSession? session)
    {
        this.services = services;
        this.session = session;
    }

    public DashboardPageState BuildPageState()
        => BuildPageState(session?.Data ?? new ProjectFileData(), session?.IsDirty ?? false, session?.StateVersion ?? 0);

    public DashboardPageState BuildPageState(ProjectFileData data, bool isDirty = false, int stateVersion = 0)
    {
        data ??= new ProjectFileData();
        var cost = BuildCost(data);
        var alerts = BuildAlerts(data);
        var completion = services.PartidaCompletion.Build(data.Itemizado, data.Cubicacion);

        var project = data.Proyecto ?? new ProjectInfoRow();
        string currencyLabel = string.IsNullOrWhiteSpace(project.MonedaPresupuesto) ? "CLP" : project.MonedaPresupuesto.Trim();
        var executive = services.Executive.BuildViewModel(cost, alerts, data.Itemizado, project, currencyLabel);

        return new DashboardPageState(
            project,
            cost,
            alerts,
            completion,
            executive,
            isDirty,
            stateVersion,
            AppClock.Now);
    }

    public CostBreakdown BuildCost(ProjectFileData data)
    {
        data ??= new ProjectFileData();
        var settings = BudgetSettings.FromProject(data.Proyecto);
        ExcelCostosBridge.EnrichFromProjectData(settings, data);
        return services.BudgetCalculation.CalculateBudget(
            data.Cubicacion,
            settings,
            data.Apus,
            data.Itemizado);
    }

    public FinalReviewSummary BuildAlerts(ProjectFileData data)
    {
        data ??= new ProjectFileData();
        return services.FinalReview.Build(
            data.Itemizado,
            data.Materiales,
            data.Cubicacion,
            data.Apus,
            data.Proyecto,
            BudgetSettings.FromProject(data.Proyecto),
            antecedentes: data.Proyecto?.AntecedentesTecnicos);
    }
}
