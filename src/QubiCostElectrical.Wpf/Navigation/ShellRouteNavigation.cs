namespace QubiCostElectrical.Wpf.Navigation;

public static class ShellRouteNavigation
{
    public static bool IsProjectRoute(ShellRoute route) =>
        route is ShellRoute.Project or ShellRoute.Documents or ShellRoute.Currency;

    public static bool IsCubicacionRoute(ShellRoute route) =>
        route is ShellRoute.Takeoff or ShellRoute.CubicacionSc;

    public static bool IsCostosRoute(ShellRoute route) =>
        route is ShellRoute.Costo or ShellRoute.Epp or ShellRoute.ManoDeObra;

    public static bool IsMoreRoute(ShellRoute route) =>
        route is ShellRoute.Templates or ShellRoute.FloorSummary;

    public static bool IsBudgetRoute(ShellRoute route) =>
        route is ShellRoute.Itemized or ShellRoute.Takeoff or ShellRoute.CubicacionSc
            or ShellRoute.Apu or ShellRoute.Templates or ShellRoute.FloorSummary
            or ShellRoute.Solicitudes or ShellRoute.Costo or ShellRoute.Epp or ShellRoute.ManoDeObra;

    public static bool IsPricesRoute(ShellRoute route) =>
        route is ShellRoute.Prices or ShellRoute.Quotes or ShellRoute.Purchases
            or ShellRoute.Solicitudes;

    public static bool IsCloseRoute(ShellRoute route) =>
        route is ShellRoute.Review or ShellRoute.Versions or ShellRoute.ChangeOrders
            or ShellRoute.OfferDocuments or ShellRoute.CloseoutWorkspace
            or ShellRoute.CloseoutReception or ShellRoute.CloseoutHandover
            or ShellRoute.CloseoutLessons or ShellRoute.CloseoutFinalDashboard
            or ShellRoute.CloseoutArchive;

    public static bool IsPlanningRoute(ShellRoute route) =>
        route is ShellRoute.PlanningBaselines or ShellRoute.PlanningProgress or ShellRoute.PlanningEarnedValue
            or ShellRoute.PlanningExecutiveDashboard or ShellRoute.PlanningOperationalDashboard;

    public static bool IsProcurementRoute(ShellRoute route) =>
        route is ShellRoute.ProcurementSuppliers or ShellRoute.ProcurementRequisitions or ShellRoute.ProcurementRfq
            or ShellRoute.ProcurementQuotations or ShellRoute.ProcurementBidComparison
            or ShellRoute.ProcurementTechnicalEvaluation or ShellRoute.ProcurementAwards
            or ShellRoute.ProcurementPurchaseOrders or ShellRoute.ProcurementGoodsReceipts
            or ShellRoute.ProcurementDashboard;

    public static bool IsContractRoute(ShellRoute route) =>
        route is ShellRoute.ContractWorkspace or ShellRoute.ContractVersions
            or ShellRoute.ChangeRequestWorkspace or ShellRoute.ExtraWorkWorkspace
            or ShellRoute.ClaimWorkspace or ShellRoute.EstadoPago;
}
