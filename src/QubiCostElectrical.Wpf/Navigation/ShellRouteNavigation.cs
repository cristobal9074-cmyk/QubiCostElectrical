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
            or ShellRoute.OfferDocuments;
}
