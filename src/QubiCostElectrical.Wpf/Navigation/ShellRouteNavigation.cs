namespace QubiCostElectrical.Wpf.Navigation;

public enum ShellLifecyclePhase
{
    Presupuesto = 1,
    Planificacion = 2,
    Ejecucion = 3
}

public static class ShellRouteNavigation
{
    public static ShellLifecyclePhase GetLifecyclePhase(ShellRoute route) => route switch
    {
        ShellRoute.Purchases
            or ShellRoute.FloorSummary
            or ShellRoute.ContractWorkspace
            or ShellRoute.ContractEditor
            or ShellRoute.ContractVersions => ShellLifecyclePhase.Planificacion,

        ShellRoute.ChangeOrders
            or ShellRoute.Quotes
            or ShellRoute.Solicitudes
            or ShellRoute.EstadosPagos => ShellLifecyclePhase.Ejecucion,

        _ => ShellLifecyclePhase.Presupuesto
    };

    public static bool IsPresupuestoRoute(ShellRoute route) =>
        GetLifecyclePhase(route) == ShellLifecyclePhase.Presupuesto;

    public static bool IsPlanificacionRoute(ShellRoute route) =>
        GetLifecyclePhase(route) == ShellLifecyclePhase.Planificacion;

    public static bool IsEjecucionRoute(ShellRoute route) =>
        GetLifecyclePhase(route) == ShellLifecyclePhase.Ejecucion;

    public static bool IsProjectRoute(ShellRoute route) =>
        route is ShellRoute.Project or ShellRoute.Documents or ShellRoute.Currency;

    public static bool IsCubicacionRoute(ShellRoute route) =>
        route is ShellRoute.Takeoff or ShellRoute.CubicacionSc or ShellRoute.Apu;

    public static bool IsCostosRoute(ShellRoute route) =>
        route is ShellRoute.Costo or ShellRoute.Epp or ShellRoute.ManoDeObra;

    public static bool IsMoreRoute(ShellRoute route) =>
        route is ShellRoute.Templates or ShellRoute.Preferences
            or ShellRoute.FloorSummary;

    public static bool IsBudgetRoute(ShellRoute route) =>
        route is ShellRoute.Itemized or ShellRoute.Takeoff or ShellRoute.CubicacionSc
            or ShellRoute.Apu or ShellRoute.Templates or ShellRoute.FloorSummary
            or ShellRoute.Solicitudes or ShellRoute.Costo or ShellRoute.Epp or ShellRoute.ManoDeObra;

    public static bool IsPricesRoute(ShellRoute route) =>
        route is ShellRoute.Prices or ShellRoute.Solicitudes;

    public static bool IsCloseRoute(ShellRoute route) =>
        route is ShellRoute.Review or ShellRoute.Versions or ShellRoute.ChangeOrders
            or ShellRoute.OfferDocuments or ShellRoute.ContractWorkspace
            or ShellRoute.ContractEditor or ShellRoute.ContractVersions
            or ShellRoute.Costo or ShellRoute.ManoDeObra or ShellRoute.Epp;
}
