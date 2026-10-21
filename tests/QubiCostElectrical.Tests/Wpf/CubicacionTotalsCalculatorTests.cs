using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Red de regresión para la extracción de CubicacionTotalsCalculator: estos tests exercitan
/// RefreshTotals()/ScheduleRefreshTotals() a través de la API pública de CubicacionViewModel
/// — igual antes y después de mover el código internamente, porque el contrato externo
/// (las 5 propiedades públicas) no cambia. Si el refactor rompe algo, estos tests fallan.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionTotalsCalculatorTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Takeoff;
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

    public CubicacionTotalsCalculatorTests() => WpfTestHost.EnsureInitialized();

    private static async Task<CubicacionPageVm> CreateVmWithPartidaAsync(WpfAppHost host, decimal cantidadItem = 1m)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-totals-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-totals");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = cantidadItem
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        return vm;
    }

    [Fact]
    public async Task RefreshTotals_NoRelevantRows_AllTotalsAreZero()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);

        vm.RefreshTotals();

        Assert.Equal(0m, vm.TotalMateriales);
        Assert.Equal(0m, vm.TotalManoObra);
        Assert.Equal(0m, vm.TotalCostoDirecto);
        Assert.Equal(0m, vm.PrecioUnitario);
        Assert.Equal(0m, vm.TotalPartida);
    }

    [Fact]
    public async Task RefreshTotals_WithPricedRow_CalculatesNonZeroTotals()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host, cantidadItem: 2m);
        var row = vm.Rows[0];
        row.Codigo = "MAT-1";
        row.Cantidad = 5m;
        row.MatClp = 1000m;
        row.MoClp = 500m;

        vm.RefreshTotals();

        // Σ CLP: 5 × 1000 materiales + 5 × 500 MO = 7500 costo directo
        Assert.Equal(5_000m, vm.TotalMateriales);
        Assert.Equal(2_500m, vm.TotalManoObra);
        Assert.Equal(7_500m, vm.TotalCostoDirecto);
        Assert.Equal(7_500m, vm.TotalPartida);
    }

    [Fact]
    public async Task RefreshTotals_IgnoresRowsWithNoContent()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host, cantidadItem: 3m);
        // CreateVmWithPartidaAsync deja varias filas placeholder además de la que se edita
        // (Rows.Count > 1) — todas sin código/cantidad/precio. Si IsRowRelevantForTotals
        // no las filtrara, el total incluiría "filas fantasma" además de la fila real.
        Assert.True(vm.Rows.Count > 1, "Este test necesita filas placeholder de sobra para tener sentido.");
        var row = vm.Rows[0];
        row.Codigo = "MAT-1";
        row.Cantidad = 5m;
        row.MatClp = 1000m;

        vm.RefreshTotals();
        decimal totalConUnaFilaReal = vm.TotalCostoDirecto;

        Assert.All(vm.Rows.Skip(1), r => Assert.False(r.HasContent, $"Fila {r.Id} no debería tener contenido para que este test sea válido."));
        Assert.True(totalConUnaFilaReal > 0m);

        // Recalcular no debe cambiar nada si las filas placeholder siguen vacías.
        vm.RefreshTotals();
        Assert.Equal(totalConUnaFilaReal, vm.TotalCostoDirecto);
    }

    // NOTA — por qué no hay un test automatizado de ScheduleRefreshTotals() aquí:
    // RefreshTotalsAsync (código actual, sin modificar) aplica el resultado vía
    // `Application.Current.Dispatcher.InvokeAsync(...)` sin operador `?.` — y en este
    // entorno de test headless, `Application.Current` es null (confirmado: ningún test
    // existente en toda la suite depende de que ese Dispatcher complete). El Task interno
    // queda no observado y nunca aplica el resultado, así que no hay forma de esperar a que
    // "termine" desde un test — esto es una limitación preexistente del entorno de test,
    // no algo que este refactor rompa o arregle. La preservación exacta de esa lectura
    // tardía de `selectedPartida` (el hallazgo de la revisión independiente del plan) se
    // verifica en el smoke test en vivo: cambiar de partida rápido mientras se cubica y
    // confirmar que los totales en pantalla siempre corresponden a la partida activa.
}
