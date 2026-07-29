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
}
