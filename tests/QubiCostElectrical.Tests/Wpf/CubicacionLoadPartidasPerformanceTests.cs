using System;
using System.Diagnostics;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// LoadPartidas() (llamada al construir la VM, al cambiar Itemizado, y en Refresh()) arma
/// un índice de materiales y recorre TODA session.Cubicacion para calcular conteos y el
/// punto rojo de "issues" por partida. Mide que ese costo se mantenga fluido en un proyecto
/// grande y maduro (muchas partidas + catálogo corporativo grande).
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionLoadPartidasPerformanceTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Takeoff;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false) { CurrentRoute = route; return true; }
        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) => RequestNavigate(route, force);
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

    public CubicacionLoadPartidasPerformanceTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void Refresh_LargeMatureProject_UnderBudget()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-loadperf");

        // Catálogo corporativo grande.
        for (int i = 0; i < 5000; i++)
            host.Session.Materiales.Add(new MaterialRow { Codigo = $"MAT-{i:00000}", MatUf = 0.01m });

        // Proyecto maduro: 800 partidas, algunas con recursos (algunos con problemas
        // deliberados para que el cálculo de HasIssues tenga trabajo real que hacer).
        for (int p = 0; p < 800; p++)
        {
            string itemId = $"item-{p:0000}";
            host.Session.Itemizado.Add(new ItemizadoRow { Id = itemId, Item = $"{p}", Descripcion = $"Partida {p}", Cantidad = 1m });

            for (int r = 0; r < 15; r++)
            {
                host.Session.Cubicacion.Add(new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = itemId,
                    Codigo = r == 0 ? "NO-EXISTE" : $"MAT-{(r * 7 % 5000):00000}",
                    Cantidad = r + 1,
                    MatUf = r == 0 ? 0m : 0.01m
                });
            }
        }

        // No se persiste a disco: la VM lee de host.Session en memoria, y SaveAsAsync solo
        // agregaba ~1 minuto de I/O de SQLite irrelevante para lo que mide este test.

        // Primera construcción "en frío" (paga costo de JIT de los caminos de LoadPartidas
        // que ningún test anterior ejecutó) — se reporta pero no es la que se evalúa: en la
        // app real, para cuando el usuario abre Cubicación ya hubo actividad previa (login,
        // dashboard) que jitea estos caminos genéricos de LINQ/Dictionary.
        var swCold = Stopwatch.StartNew();
        _ = new CubicacionPageVm(new FakeShell(), host);
        swCold.Stop();

        var sw = Stopwatch.StartNew();
        var vm = new CubicacionPageVm(new FakeShell(), host);
        sw.Stop();

        Assert.Equal(800, vm.Partidas.Count);

        var swRefresh = Stopwatch.StartNew();
        vm.Refresh();
        swRefresh.Stop();

        // Construir la pantalla (LoadPartidas en el constructor) y refrescarla luego de
        // cualquier cambio externo (importación, otra pestaña) deben sentirse instantáneos.
        Assert.True(sw.Elapsed.TotalMilliseconds < 300,
            $"Construir CubicacionViewModel con 800 partidas x 15 filas y catálogo de 5000 " +
            $"materiales tardó {sw.Elapsed.TotalMilliseconds:N1} ms (en frío: {swCold.Elapsed.TotalMilliseconds:N1} ms).");
        Assert.True(swRefresh.Elapsed.TotalMilliseconds < 300,
            $"Refresh() con el mismo proyecto tardó {swRefresh.Elapsed.TotalMilliseconds:N1} ms.");
    }
}
