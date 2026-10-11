using QubiCostElectrical.Wpf.Navigation;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ShellRouteNavigationTests
{
    [Theory]
    [InlineData(ShellRoute.Project, true, false, false, false)]
    [InlineData(ShellRoute.Documents, true, false, false, false)]
    [InlineData(ShellRoute.Itemized, false, true, false, false)]
    [InlineData(ShellRoute.Takeoff, false, true, false, false)]
    [InlineData(ShellRoute.Apu, false, true, false, false)]
    [InlineData(ShellRoute.Prices, false, false, true, false)]
    [InlineData(ShellRoute.Review, false, false, false, true)]
    [InlineData(ShellRoute.OfferDocuments, false, false, false, true)]
    [InlineData(ShellRoute.ChangeOrders, false, false, false, true)]
    public void RouteGrouping_MatchesNavDropdowns(
        ShellRoute route,
        bool project,
        bool budget,
        bool prices,
        bool close)
    {
        Assert.Equal(project, ShellRouteNavigation.IsProjectRoute(route));
        Assert.Equal(budget, ShellRouteNavigation.IsBudgetRoute(route));
        Assert.Equal(prices, ShellRouteNavigation.IsPricesRoute(route));
        Assert.Equal(close, ShellRouteNavigation.IsCloseRoute(route));
    }

    [Theory]
    [InlineData(ShellRoute.Takeoff, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.Itemized, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.CubicacionSc, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.Apu, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.Prices, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.Quotes, ShellLifecyclePhase.Ejecucion)]
    [InlineData(ShellRoute.Review, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.OfferDocuments, ShellLifecyclePhase.Presupuesto)]
    [InlineData(ShellRoute.Purchases, ShellLifecyclePhase.Planificacion)]
    [InlineData(ShellRoute.FloorSummary, ShellLifecyclePhase.Planificacion)]
    [InlineData(ShellRoute.ContractWorkspace, ShellLifecyclePhase.Planificacion)]
    [InlineData(ShellRoute.ChangeOrders, ShellLifecyclePhase.Ejecucion)]
    [InlineData(ShellRoute.Solicitudes, ShellLifecyclePhase.Ejecucion)]
    [InlineData(ShellRoute.EstadosPagos, ShellLifecyclePhase.Ejecucion)]
    public void GetLifecyclePhase_ResolvesCorrectLifecyclePhase(ShellRoute route, ShellLifecyclePhase expectedPhase)
    {
        var phase = ShellRouteNavigation.GetLifecyclePhase(route);
        Assert.Equal(expectedPhase, phase);

        Assert.Equal(expectedPhase == ShellLifecyclePhase.Presupuesto, ShellRouteNavigation.IsPresupuestoRoute(route));
        Assert.Equal(expectedPhase == ShellLifecyclePhase.Planificacion, ShellRouteNavigation.IsPlanificacionRoute(route));
        Assert.Equal(expectedPhase == ShellLifecyclePhase.Ejecucion, ShellRouteNavigation.IsEjecucionRoute(route));
    }
}
