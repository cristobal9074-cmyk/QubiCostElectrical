using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Epp;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class EppGroupingTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Epp;
        public string ActiveProjectLabel => "";
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            LastContext = null;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastContext = context;
            return true;
        }

        public WpfAppHost Host => WpfAppHost.Current;
        public Task NewProjectAsync() => Task.CompletedTask;
        public Task OpenProjectAsync() => Task.CompletedTask;
        public Task<bool> CloseProjectAsync() => Task.FromResult(true);
        public Task OpenRecentProjectAsync(string path) => Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public Task SaveProjectAsync() => Task.CompletedTask;
        public Task SaveProjectAsAsync() => Task.CompletedTask;
        public void RefreshChrome() { }
    }

    [Fact]
    public void EppGrouping_CuadrillaAndFaseBreakdown_MatchesTotalEpp()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("epp-test");
        host.Session.CurrentProjectPath = "epp-test.qce";

        var epp = EppBudgetData.CreateSeed(mesesObra: 8, promedioDirecto: 12, dotacionIndirecta: 5);
        host.Session.Data.Epp = epp;

        var shell = new FakeShellHost();
        var vm = new EppViewModel(shell, host);

        // Aseguramos valores
        vm.MesesObra = 8;
        vm.DotacionDirecta = 12;
        vm.DotacionIndirecta = 5;

        // Recalcular
        vm.RebuildGroupedBreakdowns();

        // Assert: Cuadrillas
        Assert.Equal(4, vm.CuadrillaGroups.Count);
        Assert.All(vm.CuadrillaGroups, g =>
        {
            Assert.True(g.Dotacion > 0);
            Assert.NotEmpty(g.Items);
            Assert.True(g.Subtotal > 0m);
        });

        decimal sumCuadrillas = vm.CuadrillaGroups.Sum(g => g.Subtotal);
        Assert.InRange(sumCuadrillas, vm.TotalEpp - 10m, vm.TotalEpp + 10m);

        // Assert: Fases de obra
        Assert.Equal(3, vm.FaseGroups.Count);
        Assert.All(vm.FaseGroups, f =>
        {
            Assert.True(f.Dotacion > 0);
            Assert.NotEmpty(f.Items);
            Assert.True(f.Subtotal > 0m);
        });

        decimal sumFases = vm.FaseGroups.Sum(f => f.Subtotal);
        Assert.InRange(sumFases, vm.TotalEpp - 10m, vm.TotalEpp + 10m);
    }

    [Fact]
    public void EppGrouping_ModeSwitching_UpdatesStateAndLabels()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("epp-modes");
        host.Session.CurrentProjectPath = "epp-modes.qce";

        var shell = new FakeShellHost();
        var vm = new EppViewModel(shell, host);

        Assert.Equal(EppGroupingMode.Categoria, vm.GroupingMode);
        Assert.True(vm.IsGroupingCategoria);
        Assert.False(vm.IsGroupingCuadrilla);
        Assert.False(vm.IsGroupingFase);

        vm.SelectCuadrillaViewCommand.Execute(null);
        Assert.Equal(EppGroupingMode.Cuadrilla, vm.GroupingMode);
        Assert.False(vm.IsGroupingCategoria);
        Assert.True(vm.IsGroupingCuadrilla);
        Assert.Contains("Cuadrilla", vm.CurrentGroupingTitle);

        vm.SelectFaseViewCommand.Execute(null);
        Assert.Equal(EppGroupingMode.Fase, vm.GroupingMode);
        Assert.False(vm.IsGroupingCuadrilla);
        Assert.True(vm.IsGroupingFase);
        Assert.Contains("Fase", vm.CurrentGroupingTitle);
    }
}
