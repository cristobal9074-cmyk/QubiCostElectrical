using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class VersionsViewModelSelectionTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
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

    private static BudgetVersionRow MakeVersion(string codigo, string nombre, decimal montoUf = 0m)
    {
        var snapshot = new BudgetVersionData
        {
            Proyecto = new ProjectInfoRow { Nombre = nombre },
            Itemizado = [],
            Cubicacion = []
        };
        return new BudgetVersionRow
        {
            Codigo = codigo,
            Nombre = nombre,
            Estado = "Preliminar",
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow,
            FechaModificacion = DateTime.UtcNow,
            MontoVentaUf = montoUf,
            Snapshot = snapshot
        };
    }

    public VersionsViewModelSelectionTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public async Task TargetVersionOptions_PopulatedWithCurrentStateAndOtherVersions()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"ver-target-options-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("ver-target-options");
        host.Session.Versiones.Clear();
        host.Session.Versiones.Add(MakeVersion("V1", "Rev A", 10m));
        host.Session.Versiones.Add(MakeVersion("V2", "Rev B", 15m));
        host.Session.Versiones.Add(MakeVersion("V3", "Rev C", 20m));
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new VersionsViewModel(new FakeShellHost(), host);
        vm.SelectedVersion = vm.Versions.First(v => v.Codigo == "V1");

        Assert.NotEmpty(vm.TargetVersionOptions);
        Assert.NotNull(vm.SelectedTargetOption);
        Assert.True(vm.SelectedTargetOption!.IsCurrentState);
        Assert.Contains(vm.TargetVersionOptions, o => o.IsCurrentState);
        Assert.Contains(vm.TargetVersionOptions, o => !o.IsCurrentState && o.Codigo == "V1");
        Assert.Contains(vm.TargetVersionOptions, o => !o.IsCurrentState && o.Codigo == "V2");
        Assert.Contains(vm.TargetVersionOptions, o => !o.IsCurrentState && o.Codigo == "V3");

        // Seleccionar versión B = V2
        var optV2 = vm.TargetVersionOptions.First(o => o.Codigo == "V2");
        vm.SelectedTargetOption = optV2;

        Assert.False(vm.CompareAgainstCurrent);
        Assert.Equal("V2", vm.SelectedComparisonVersion?.Codigo);

        // Swap V1 <-> V2
        Assert.True(vm.CanSwapVersions);
        Assert.True(vm.SwapVersionsCommand.CanExecute(null));
        vm.SwapVersionsCommand.Execute(null);

        Assert.Equal("V2", vm.SelectedVersion?.Codigo);
        Assert.Equal("V1", vm.SelectedComparisonVersion?.Codigo);

        // Volver a estado actual
        var optCurrent = vm.TargetVersionOptions.First(o => o.IsCurrentState);
        vm.SelectedTargetOption = optCurrent;

        Assert.True(vm.CompareAgainstCurrent);
        Assert.Null(vm.SelectedComparisonVersion);
        Assert.False(vm.CanSwapVersions);
    }
}
