namespace QubiCostElectrical;

public static class PresenterFactory
{
    public static CubicacionPresenter CreateCubicacionPresenter(AppServices services, BudgetProjectSession session)
        => new(new CubicacionPresenterServices(
            services.BudgetCalculation,
            new CubicacionCompositionService()), session);

    public static ExportPresenter CreateExportPresenter(AppServices services, BudgetProjectSession session)
        => new(new ExportPresenterServices(new DashboardPresenterServices(
            services.BudgetCalculation,
            services.PartidaCompletion,
            services.FinalReview,
            new DashboardExecutiveService())), session);
}
