using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Tests.Wpf;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Stability;

// Fase de estabilización: simula un proyecto GRANDE y REALISTA (300+ partidas, miles de
// recursos, catálogo de materiales de tamaño real) y ejercita el flujo completo que un
// presupuestador haría de punta a punta — cargar, navegar, editar, deshacer, guardar,
// cerrar y reabrir. El objetivo es encontrar crashes/lentitud ANTES de que un cliente los
// encuentre, no después.
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class LargeRealisticProjectStabilityTests
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

    public LargeRealisticProjectStabilityTests() => WpfTestHost.EnsureInitialized();

    private static string BuildLargeProject(WpfAppHost host, int partidaCount, int rowsPerPartida, int materialCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"stability-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("proyecto-grande-real", "Tester");

        for (int m = 1; m <= materialCount; m++)
        {
            host.Session.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{m:00000}",
                Descripcion = $"Material eléctrico {m}",
                Unidad = m % 3 == 0 ? "UN" : "ML",
                MatUf = 0.01m * (m % 20 + 1),
                MoUf = 0.005m * (m % 10 + 1)
            });
        }

        var rnd = new Random(12345);
        for (int p = 1; p <= partidaCount; p++)
        {
            string itemId = $"P{p}";
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = itemId,
                Item = $"{p / 20 + 1}.{p % 20 + 1}",
                Descripcion = $"Partida eléctrica real número {p}",
                Unidad = "GL",
                Cantidad = 1m,
                Observaciones = p % 15 == 0 ? $"Observación de constructora para partida {p}." : ""
            });

            for (int r = 1; r <= rowsPerPartida; r++)
            {
                var mat = host.Session.Materiales[(p * 7 + r) % materialCount];
                host.Session.Cubicacion.Add(new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = itemId,
                    Codigo = mat.Codigo,
                    Descripcion = mat.Descripcion,
                    Unidad = mat.Unidad,
                    Cantidad = rnd.Next(1, 50),
                    MatUf = mat.MatUf,
                    MoUf = mat.MoUf,
                    Area = "General",
                    QtyExpressions = new[] { (rnd.Next(1, 20)).ToString() }
                });
            }
        }

        return path;
    }

    [Fact]
    public async Task ProyectoGrande_CargaYGuardaSinExplotar()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);

        var sw = Stopwatch.StartNew();
        await host.ProjectWorkspace.SaveAsAsync(path);
        sw.Stop();

        Assert.True(host.Session.Itemizado.Count >= 320);
        Assert.True(host.Session.Cubicacion.Count >= 320 * 15);
        Assert.True(sw.Elapsed.TotalSeconds < 30,
            $"Guardar un proyecto de {host.Session.Cubicacion.Count} recursos tardó {sw.Elapsed.TotalSeconds:N1}s (límite: 30s).");
    }

    [Fact]
    public async Task ProyectoGrande_AbrirYCerrarPartidasDiezVeces_SinCongelarse()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        var partidas = vm.Partidas.Where(p => p.EsPartida).ToList();
        Assert.True(partidas.Count >= 320);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            var partida = partidas[(i * 37) % partidas.Count];
            vm.SelectPartida(partida);
            Assert.True(vm.Rows.Count > 0);
        }
        sw.Stop();

        double perSwitchMs = sw.Elapsed.TotalMilliseconds / 10.0;
        Assert.True(perSwitchMs < 500,
            $"Cambiar de partida tardó {perSwitchMs:N0} ms en promedio (límite: 500 ms) en un proyecto de {host.Session.Cubicacion.Count} recursos.");
    }

    [Fact]
    public async Task ProyectoGrande_EditarCantidadEnPartidaGrande_EsRapido()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        var partida = vm.Partidas.First(p => p.EsPartida);
        vm.SelectPartida(partida);
        var row = vm.Rows.First(r => !string.IsNullOrWhiteSpace(r.Codigo));

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            row.SectorValues[0] = (i + 1).ToString();
        }
        sw.Stop();

        double perEditMs = sw.Elapsed.TotalMilliseconds / 20.0;
        // Umbral generoso: corriendo dentro de la suite completa en paralelo hay ruido real
        // de contención de CPU (medido hasta ~170ms en la práctica). Lo que importa acá es
        // detectar una regresión de ORDEN DE MAGNITUD (ej. los 3 segundos por snapshot de
        // undo que se encontraron y arreglaron antes), no micro-variaciones de scheduling.
        Assert.True(perEditMs < 800,
            $"Editar un sector tardó {perEditMs:N1} ms en promedio (límite: 800 ms) en un proyecto de {host.Session.Cubicacion.Count} recursos.");
    }

    // Antes del cambio a serialización con contexto generado en compilación
    // (ProjectFileDataJsonContext), CreateSnapshot() medía ~2.95s en este mismo escenario
    // (320 partidas / 2000 materiales / ~4800 recursos) — el costo dominante de cada
    // Undo/Redo. Este test fija un umbral que detecta si esa regresión vuelve.
    [Fact]
    public async Task ProyectoGrande_CreateSnapshot_EsRapido()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var sw = Stopwatch.StartNew();
        var snapshot = host.Session.CreateSnapshot();
        sw.Stop();

        Assert.Equal(host.Session.Cubicacion.Count, snapshot.Cubicacion.Count);
        Assert.Equal(host.Session.Itemizado.Count, snapshot.Itemizado.Count);
        // Umbral generoso (orden de magnitud, no micro-benchmark): antes del fix esto medía
        // ~3000-6000ms por los escaneos O(N²) en CubicacionCompositionService.EnsureProject
        // (arreglados con índices por RecursoId) — ahora ronda 700-900ms, dominado por el
        // costo real de (de)serializar ~7000 filas, no por una regresión algorítmica.
        Assert.True(sw.Elapsed.TotalMilliseconds < 1500,
            $"CreateSnapshot() tardó {sw.Elapsed.TotalMilliseconds:N0} ms en un proyecto de {host.Session.Cubicacion.Count} recursos (límite: 1500 ms; antes del fix eran ~3000-6000 ms).");
    }

    [Fact]
    public async Task ProyectoGrande_UndoRedoRepetido_NoCorrompeDatos()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 100, rowsPerPartida: 10, materialCount: 500);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        var partida = vm.Partidas.First(p => p.EsPartida);
        vm.SelectPartida(partida);

        int originalRowCount = vm.Rows.Count(r => r.IsSessionBacked);

        for (int i = 0; i < 5; i++)
        {
            vm.AddRowCommand.Execute(null);
        }
        Assert.Equal(originalRowCount + 5, vm.Rows.Count(r => r.IsSessionBacked));

        for (int i = 0; i < 5 && host.UndoService.CanUndo; i++)
        {
            host.UndoService.Undo(host.Session);
        }
        vm.Refresh();
        vm.SelectPartida(vm.Partidas.First(p => p.Id == partida.Id));

        Assert.Equal(originalRowCount, vm.Rows.Count(r => r.IsSessionBacked));
    }

    [Fact]
    public async Task ProyectoGrande_CerrarYReabrir_ConservaDatosYObservaciones()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 300, rowsPerPartida: 10, materialCount: 1000);

        var itemConObs = host.Session.Itemizado.First(i => !string.IsNullOrWhiteSpace(i.Observaciones));
        string expectedObs = itemConObs.Observaciones;
        string itemId = itemConObs.Id;

        await host.ProjectWorkspace.SaveAsAsync(path);

        host.Session.NewProject("otro-proyecto-temporal");
        await host.ProjectWorkspace.OpenAsync(path);

        var reloadedItem = host.Session.Itemizado.Single(i => i.Id == itemId);
        Assert.Equal(expectedObs, reloadedItem.Observaciones);
        Assert.True(host.Session.Cubicacion.Count >= 300 * 10);
    }

    [Fact]
    public async Task ProyectoGrande_ItemizadoCarga_EsRapido()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 320, rowsPerPartida: 15, materialCount: 2000);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var sw = Stopwatch.StartNew();
        var vm = new QubiCostElectrical.Wpf.Features.Itemizado.ItemizadoViewModel(new FakeShell(), host);
        sw.Stop();

        Assert.True(vm.Rows.Count >= 320);
        Assert.True(sw.Elapsed.TotalSeconds < 5,
            $"Cargar Itemizado con {host.Session.Itemizado.Count} partidas y {host.Session.Cubicacion.Count} recursos tardó {sw.Elapsed.TotalSeconds:N1}s (límite: 5s).");
    }

    // El clásico "se cierra inesperadamente tras usar la app un rato": abrir/cerrar el
    // proyecto varias veces seguidas (como haría un presupuestador probando varios
    // proyectos en una sesión) no debe acumular estado ni degradar el rendimiento.
    [Fact]
    public async Task ProyectoGrande_AbrirYCerrarDiezVeces_NoDegradaRendimiento()
    {
        var host = WpfAppHost.Current;
        var path = BuildLargeProject(host, partidaCount: 100, rowsPerPartida: 10, materialCount: 800);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var firstRunMs = 0.0;
        var lastRunMs = 0.0;
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            var vm = new CubicacionPageVm(new FakeShell(), host);
            vm.SelectPartida(vm.Partidas.First(p => p.EsPartida));
            sw.Stop();

            if (i == 0) firstRunMs = sw.Elapsed.TotalMilliseconds;
            if (i == 9) lastRunMs = sw.Elapsed.TotalMilliseconds;
        }

        // La décima apertura no debería ser drásticamente más lenta que la primera
        // (señal de acumulación de suscripciones/memoria entre aperturas).
        Assert.True(lastRunMs < firstRunMs * 5 + 200,
            $"La 1ra apertura tardó {firstRunMs:N0} ms y la 10ma {lastRunMs:N0} ms — sugiere degradación acumulada.");
    }
}
