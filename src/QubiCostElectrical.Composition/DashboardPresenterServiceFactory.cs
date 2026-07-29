namespace QubiCostElectrical;

public static class DashboardPresenterServiceFactory
{
    public static DashboardPresenterServices From(AppServices services) => new(
        services.BudgetCalculation,
        services.PartidaCompletion,
        services.FinalReview,
        new DashboardExecutiveService());
}
