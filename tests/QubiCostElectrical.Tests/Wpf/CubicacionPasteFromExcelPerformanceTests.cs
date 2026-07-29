using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Al pegar filas desde Excel, cada fila con código nuevo llama a TryAutoResolveFromPriceBank,
/// que si el código no está en el catálogo del proyecto consulta IPriceBankStore.GetByCodigo —
/// una operación que abre una conexión SQLite propia (con EnsureSchema) por llamada. Sin
/// procesar el pegado en lote, pegar N filas desde Excel abre N conexiones SQLite síncronas en
/// el hilo de UI. Este test mide ese costo end-to-end (no solo la query aislada) para detectar
/// si un pegado grande empieza a sentirse "pegado" (freeze).
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionPasteFromExcelPerformanceTests
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

    public CubicacionPasteFromExcelPerformanceTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void PasteClipboardDirect_TwoHundredRows_UnderBudget()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("cubicacion-paste-perf");

        string itemId = "item-0001";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = itemId, Item = "1", Descripcion = "Partida de prueba", Cantidad = 1m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        var partida = vm.Partidas.First(p => p.Id == itemId);
        vm.SelectPartida(partida);

        // 200 códigos que NO existen en el catálogo del proyecto ni en el banco de precios:
        // fuerza el mismo camino que un pegado real de materiales nuevos (el peor caso, ya
        // que cada fila cae a ResolveFromPriceBank -> IPriceBankStore.GetByCodigo).
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++)
            sb.Append($"NUEVO-{i:0000}\tMaterial nuevo {i}\tUN\t{i + 1}\n");

        var sw = Stopwatch.StartNew();
        vm.PasteClipboardDirect(sb.ToString());
        sw.Stop();

        Assert.Equal(200, vm.Rows.Count(r => r.IsSessionBacked));

        // Pegar 200 filas debe sentirse instantáneo. Si esto empieza a acercarse al segundo,
        // es la señal de que TryAutoResolveFromPriceBank volvió a abrir una conexión SQLite
        // por fila en vez de resolver los códigos en lote antes del bucle.
        Assert.True(sw.Elapsed.TotalMilliseconds < 1500,
            $"Pegar 200 filas desde Excel (todas con código nuevo, forzando consulta al banco " +
            $"de precios) tardó {sw.Elapsed.TotalMilliseconds:N1} ms.");
    }
}
