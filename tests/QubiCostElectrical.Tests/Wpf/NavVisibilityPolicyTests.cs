using QubiCostElectrical.Wpf.Navigation;

using Xunit;



namespace QubiCostElectrical.Tests.Wpf;



public sealed class NavVisibilityPolicyTests

{

    [Theory]

    [InlineData(ShellRoute.ChangeOrders)]

    [InlineData(ShellRoute.Solicitudes)]

    [InlineData(ShellRoute.ContractWorkspace)]

    [InlineData(ShellRoute.ContractEditor)]

    [InlineData(ShellRoute.ContractVersions)]

    [InlineData(ShellRoute.OfferDocuments)]

    [InlineData(ShellRoute.Takeoff)]

    [InlineData(ShellRoute.Itemized)]

    [InlineData(ShellRoute.Dashboard)]

    [InlineData(ShellRoute.Review)]

    [InlineData(ShellRoute.Purchases)]

    [InlineData(ShellRoute.Prices)]

    [InlineData(ShellRoute.Apu)]

    public void LifecycleRoutes_AreVisibleAndNavigable(ShellRoute route)

    {

        Assert.Equal(NavVisibility.Visible, NavVisibilityPolicy.Get(route));

        Assert.True(NavVisibilityPolicy.IsListed(route));

        Assert.True(NavVisibilityPolicy.IsNavigable(route));

    }

}


