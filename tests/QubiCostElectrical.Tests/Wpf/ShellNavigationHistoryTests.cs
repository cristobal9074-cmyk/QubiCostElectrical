using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class ShellNavigationHistoryTests
{
    [Fact]
    public void InitialState_HasNoHistory()
    {
        var history = new ShellNavigationHistory();

        Assert.False(history.CanNavigateBack);
        Assert.False(history.CanNavigateForward);
        Assert.Equal(0, history.BackCount);
        Assert.Equal(0, history.ForwardCount);
    }

    [Fact]
    public void RecordNavigation_AddsToBackStack()
    {
        var history = new ShellNavigationHistory();

        history.RecordNavigation(ShellRoute.Itemized, ShellRoute.Dashboard);

        Assert.True(history.CanNavigateBack);
        Assert.False(history.CanNavigateForward);
        Assert.Equal(1, history.BackCount);
    }

    [Fact]
    public void RecordNavigation_SameRoute_DoesNotPush()
    {
        var history = new ShellNavigationHistory();

        history.RecordNavigation(ShellRoute.Dashboard, ShellRoute.Dashboard);

        Assert.False(history.CanNavigateBack);
        Assert.Equal(0, history.BackCount);
    }

    [Fact]
    public void TryNavigateBack_And_TryNavigateForward_WorkInOrder()
    {
        var history = new ShellNavigationHistory();

        // Dashboard -> Itemized -> Takeoff
        history.RecordNavigation(ShellRoute.Itemized, ShellRoute.Dashboard);
        history.RecordNavigation(ShellRoute.Takeoff, ShellRoute.Itemized);

        Assert.Equal(2, history.BackCount);

        // Back from Takeoff -> should get Itemized
        var backOk = history.TryNavigateBack(ShellRoute.Takeoff, out var route1);
        Assert.True(backOk);
        Assert.Equal(ShellRoute.Itemized, route1);
        Assert.True(history.CanNavigateBack);
        Assert.True(history.CanNavigateForward);

        // Back from Itemized -> should get Dashboard
        backOk = history.TryNavigateBack(ShellRoute.Itemized, out var route2);
        Assert.True(backOk);
        Assert.Equal(ShellRoute.Dashboard, route2);
        Assert.False(history.CanNavigateBack);
        Assert.True(history.CanNavigateForward);

        // Forward from Dashboard -> should get Itemized
        var fwdOk = history.TryNavigateForward(ShellRoute.Dashboard, out var routeFwd1);
        Assert.True(fwdOk);
        Assert.Equal(ShellRoute.Itemized, routeFwd1);
        Assert.True(history.CanNavigateBack);
        Assert.True(history.CanNavigateForward);

        // Forward from Itemized -> should get Takeoff
        fwdOk = history.TryNavigateForward(ShellRoute.Itemized, out var routeFwd2);
        Assert.True(fwdOk);
        Assert.Equal(ShellRoute.Takeoff, routeFwd2);
        Assert.True(history.CanNavigateBack);
        Assert.False(history.CanNavigateForward);
    }

    [Fact]
    public void NewNavigation_ClearsForwardStack()
    {
        var history = new ShellNavigationHistory();

        // Dashboard -> Itemized -> Takeoff
        history.RecordNavigation(ShellRoute.Itemized, ShellRoute.Dashboard);
        history.RecordNavigation(ShellRoute.Takeoff, ShellRoute.Itemized);

        // Back to Itemized
        Assert.True(history.TryNavigateBack(ShellRoute.Takeoff, out _));
        Assert.True(history.CanNavigateForward);

        // Branching: navigate to Costo instead of going forward
        history.RecordNavigation(ShellRoute.Costo, ShellRoute.Itemized);

        Assert.False(history.CanNavigateForward);
        Assert.Equal(0, history.ForwardCount);
        Assert.True(history.CanNavigateBack);
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var history = new ShellNavigationHistory();

        history.RecordNavigation(ShellRoute.Itemized, ShellRoute.Dashboard);
        history.RecordNavigation(ShellRoute.Takeoff, ShellRoute.Itemized);
        history.TryNavigateBack(ShellRoute.Takeoff, out _);

        Assert.True(history.CanNavigateBack);
        Assert.True(history.CanNavigateForward);

        history.Clear();

        Assert.False(history.CanNavigateBack);
        Assert.False(history.CanNavigateForward);
        Assert.Equal(0, history.BackCount);
        Assert.Equal(0, history.ForwardCount);
    }
}
