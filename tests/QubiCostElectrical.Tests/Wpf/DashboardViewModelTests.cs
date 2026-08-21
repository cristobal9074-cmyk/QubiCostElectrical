using System.IO;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class DashboardViewModelTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) =>
            RequestNavigate(route, force);

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
    public async Task Load_WithOpenProject_PopulatesCostDistributionAndKpiAccents()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        host.Session.NewProject("dashboard-visual-test");
        host.Session.ProjectInfo.Nombre = "Proyecto visual";
        host.Session.ProjectInfo.MetrosCuadrados = 120m;

        var path = Path.Combine(Path.GetTempPath(), $"dashboard-visual-{Guid.NewGuid():N}.qce");
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new DashboardViewModel(new FakeShell(), WpfAppHost.Current);
        vm.Load();

        Assert.False(vm.ShowEmptyState);
        Assert.Equal(4, vm.KpiCards.Count);
        Assert.Equal(3, vm.CostShares.Count);
        Assert.Equal(GridUnitType.Star, vm.MaterialShareWidth.GridUnitType);
        Assert.Equal(GridUnitType.Star, vm.LaborShareWidth.GridUnitType);
        Assert.Equal(GridUnitType.Star, vm.OtherShareWidth.GridUnitType);
        Assert.Equal(6, vm.WorkflowSteps.Count);
    }

    [Fact]
    public async Task Load_WithoutProject_ShowsLandingScreenAndBuildsSummaryFromRecentFiles()
    {
        WpfTestHost.EnsureInitialized();

        var prefsPath = Path.Combine(Path.GetTempPath(), $"dashboard-recent-{Guid.NewGuid():N}.json");
        using var prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(prefsPath);

        var host = WpfAppHost.Current;
        var repo = host.Services.ProjectRepository;

        string CreateProject(string prefix, string estado)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.qce");
            repo.SaveSQLiteSnapshot(path, new ProjectFileData
            {
                Proyecto = new ProjectInfoRow
                {
                    Nombre = prefix,
                    Cliente = "Cliente",
                    Ubicacion = "Obra",
                    EstadoPresupuesto = estado,
                    FechaModificacion = DateTime.UtcNow
                }
            });
            return path;
        }

        var completePath = CreateProject("proyecto-completo", "Completo");
        var progressPath = CreateProject("proyecto-progreso", "En progreso");
        var draftPath = CreateProject("proyecto-borrador", "Borrador");

        var recent = new RecentProjectsService();
        recent.AddProject(draftPath);
        recent.AddProject(progressPath);
        recent.AddProject(completePath);

        var vm = new DashboardViewModel(new FakeShell(), host);
        vm.Load();
        if (vm.LastRefreshTask != null) await vm.LastRefreshTask;

        Assert.True(vm.ShowEmptyState);
        Assert.True(vm.HasRecentProjects);
        Assert.Equal(3, vm.RecentProjects.Count);
        Assert.Equal(4, vm.QuickSummaryCards.Count);
        Assert.Equal("3", vm.QuickSummaryCards[0].Value);
        Assert.Equal("1", vm.QuickSummaryCards[1].Value);
        Assert.Equal("1", vm.QuickSummaryCards[2].Value);
        Assert.Equal("1", vm.QuickSummaryCards[3].Value);
        Assert.Equal("Completo", vm.RecentProjects[0].StatusText);
        Assert.Equal("En progreso", vm.RecentProjects[1].StatusText);
        Assert.Equal("Borrador", vm.RecentProjects[2].StatusText);
        Assert.Empty(vm.KpiCards);
        Assert.Empty(vm.CostShares);
        Assert.Empty(vm.WorkflowSteps);
        Assert.Equal(0d, vm.MaterialShareWidth.Value);
    }

    // Regresión: la pantalla de Datos del proyecto ahora ofrece "Aprobado" como estado fijo
    // (antes el campo era texto libre), y el Dashboard debe reconocerlo como "Completo" en
    // vez de caer al valor por defecto "Borrador" (que era el comportamiento antes de este fix).
    [Fact]
    public async Task Load_WithoutProject_ClassifiesAprobadoAsComplete()
    {
        WpfTestHost.EnsureInitialized();

        var prefsPath = Path.Combine(Path.GetTempPath(), $"dashboard-aprobado-{Guid.NewGuid():N}.json");
        using var prefsScope = UserPreferencesStore.UsePreferencesFilePathForTests(prefsPath);

        var host = WpfAppHost.Current;
        var repo = host.Services.ProjectRepository;

        var path = Path.Combine(Path.GetTempPath(), $"proyecto-aprobado-{Guid.NewGuid():N}.qce");
        repo.SaveSQLiteSnapshot(path, new ProjectFileData
        {
            Proyecto = new ProjectInfoRow
            {
                Nombre = "proyecto-aprobado",
                Cliente = "Cliente",
                Ubicacion = "Obra",
                EstadoPresupuesto = "Aprobado",
                FechaModificacion = DateTime.UtcNow
            }
        });

        var recent = new RecentProjectsService();
        recent.AddProject(path);

        var vm = new DashboardViewModel(new FakeShell(), host);
        vm.Load();
        if (vm.LastRefreshTask != null) await vm.LastRefreshTask;

        Assert.Equal("Completo", vm.RecentProjects[0].StatusText);
    }

    // Regresión: el usuario reporta que la app "se queda pegada" o "se cierra inesperadamente"
    // tras usarla un rato — un patrón típico es abrir y cerrar proyectos repetidamente sin
    // reiniciar la app, ya que cada cierre vuelve a esta pantalla (Load() se llama de nuevo).
    // Este test simula 10 ciclos de abrir/cerrar sobre la MISMA instancia de ViewModel (como
    // pasaría con el cacheo de páginas del shell) y verifica que ninguna colección crezca sin
    // límite (señal de un Clear() olvidado) y que no se lance ninguna excepción.
    [Fact]
    public async Task Load_OpenAndCloseProjectTenTimes_DoesNotThrowOrAccumulateState()
    {
        WpfTestHost.EnsureInitialized();
        var host = WpfAppHost.Current;
        var vm = new DashboardViewModel(new FakeShell(), host);

        for (int cycle = 0; cycle < 10; cycle++)
        {
            host.Session.NewProject($"ciclo-{cycle}");
            host.Session.ProjectInfo.Nombre = $"Proyecto ciclo {cycle}";
            host.Session.ProjectInfo.MetrosCuadrados = 100m;
            host.Session.Itemizado.Add(new ItemizadoRow { Id = $"item-{cycle}", Item = "1", Descripcion = "Partida", Cantidad = 1m });
            host.Session.Cubicacion.Add(new CubicacionRow { Id = Guid.NewGuid().ToString("N"), ItemId = $"item-{cycle}", Codigo = "MAT-1", Cantidad = 1m, MatUf = 1m });

            var path = Path.Combine(Path.GetTempPath(), $"dashboard-cycle-{cycle}-{Guid.NewGuid():N}.qce");
            await host.ProjectWorkspace.SaveAsAsync(path);

            vm.Load();
            Assert.False(vm.ShowEmptyState);
            Assert.Equal(4, vm.KpiCards.Count);
            Assert.Equal(3, vm.CostShares.Count);
            Assert.Equal(6, vm.WorkflowSteps.Count);

            // "Cerrar" el proyecto: NewProject() deja CurrentProjectPath vacío, que es lo
            // que HasOpenProject usa para decidir si mostrar la pantalla de bienvenida.
            host.Session.NewProject($"cerrado-{cycle}");
            vm.Load();
            Assert.True(vm.ShowEmptyState);
            Assert.Empty(vm.KpiCards);
            Assert.Empty(vm.CostShares);
            Assert.Empty(vm.WorkflowSteps);
        }
    }
}
