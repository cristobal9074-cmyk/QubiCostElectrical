using System;
using System.Diagnostics;
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

// Regresión: cada edición de celda (una tecla, un sector) llamaba a ValidateRows() completo,
// que reconstruía el índice código→material desde CERO y revalidaba TODAS las filas de la
// partida — con un catálogo grande y muchas filas, esto se sentía "pegado" al escribir. Ahora
// el índice se cachea y solo se revalida la fila tocada.
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionEditPerformanceTests
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

    public CubicacionEditPerformanceTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public async Task OnRowEdited_ConCatalogoGrandeYMuchasFilas_CadaEdicionEsMilisegundos()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-editperf-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-editperf");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Partida de prueba",
            Unidad = "UN",
            Cantidad = 1m
        });

        // Catálogo grande: reconstruirlo por cada tecleo es justo lo que se quiere evitar.
        for (int i = 1; i <= 4000; i++)
        {
            host.Session.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{i:00000}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                MatUf = 0.01m * (i % 10 + 1),
                MoUf = 0.005m * (i % 5 + 1)
            });
        }

        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        // 300 filas en la partida activa (simulando un takeoff real con volumen).
        for (int i = 0; i < 300; i++)
            vm.AddRowCommand.Execute(null);

        var rows = vm.Rows.Where(r => r.IsSessionBacked).ToList();
        Assert.True(rows.Count >= 300);

        // Calentar (primera pasada reconstruye el índice cacheado una vez).
        rows[0].Codigo = "MAT-00001";
        vm.OnRowEdited(rows[0]);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++)
        {
            var row = rows[i % rows.Count];
            row.Codigo = $"MAT-{(i % 4000) + 1:00000}";
            vm.OnRowEdited(row);
        }
        sw.Stop();

        double perEditMs = sw.Elapsed.TotalMilliseconds / 200.0;
        Assert.True(perEditMs < 5.0,
            $"OnRowEdited tardó {perEditMs:N2} ms en promedio por edición (límite: 5 ms) con 4000 materiales y 300 filas.");
    }

    [Fact]
    public async Task NotifyCodigoChangedAsync_ConCatalogoGrande_CadaTeclaEsMilisegundos()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-autocomplete-perf-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-autocomplete-perf");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Partida de prueba",
            Unidad = "UN",
            Cantidad = 1m
        });

        for (int i = 1; i <= 4000; i++)
        {
            host.Session.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{i:00000}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                MatUf = 0.01m,
                MoUf = 0.005m
            });
        }

        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.AddRowCommand.Execute(null);
        var row = vm.Rows.First(r => r.IsSessionBacked);

        // Calentar: primera llamada construye el índice cacheado.
        await vm.NotifyCodigoChangedAsync(row, "MAT-00");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 30; i++)
        {
            row.Codigo = $"MAT-{i:00000}";
            await vm.NotifyCodigoChangedAsync(row, row.Codigo);
        }
        sw.Stop();

        double perKeyMs = sw.Elapsed.TotalMilliseconds / 30.0;
        // Umbral con margen para tolerar contención de CPU al correr junto al resto de la
        // suite (miles de pruebas en paralelo); lo que importa es detectar una regresión de
        // orden de magnitud, no micro-variaciones de carga de máquina.
        Assert.True(perKeyMs < 40.0,
            $"NotifyCodigoChangedAsync tardó {perKeyMs:N2} ms en promedio por tecla (límite: 40 ms) con 4000 materiales.");
    }

    // Regresión: aplicar un código a una fila nueva (típico al aceptar el autocompletado)
    // pasaba por EnsureRowInSession (antes: .Contains() sobre TODA session.Cubicacion, de
    // todas las partidas del proyecto) y EnsureMaterialInProjectCatalog (antes: .Any() sobre
    // todo el catálogo) — con un proyecto grande, ese era el "se demora en colocar el código".
    [Fact]
    public async Task ApplyMaterial_ConProyectoGrande_CadaFilaNuevaEsMilisegundos()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-applymaterial-perf-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-applymaterial-perf");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Partida de prueba",
            Unidad = "UN",
            Cantidad = 1m
        });
        // Otras partidas con filas ya guardadas — simula un proyecto con varias partidas donde
        // session.Cubicacion (todo el proyecto) es más grande que la partida activa. Nota: el
        // costo de PushUndoPoint (snapshot completo de la sesión) escala con el tamaño TOTAL
        // del proyecto y domina cualquier medición a gran escala — eso se reporta aparte como
        // hallazgo de arquitectura; este test mantiene una escala moderada para seguir siendo
        // una regresión rápida específica de EnsureRowInSession/EnsureMaterialInProjectCatalog.
        for (int i = 1; i <= 20; i++)
        {
            host.Session.Itemizado.Add(new ItemizadoRow { Id = $"OTHER-{i}", Item = $"9.{i}", Descripcion = "Otra partida", Unidad = "UN", Cantidad = 1m });
            for (int r = 0; r < 10; r++)
            {
                host.Session.Cubicacion.Add(new CubicacionRow
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ItemId = $"OTHER-{i}",
                    Codigo = $"OTHER-MAT-{i}-{r}",
                    Descripcion = "Recurso de otra partida",
                    Unidad = "UN",
                    Cantidad = 1m,
                    Area = "General"
                });
            }
        }

        for (int i = 1; i <= 500; i++)
        {
            host.Session.Materiales.Add(new MaterialRow
            {
                Codigo = $"MAT-{i:00000}",
                Descripcion = $"Material {i}",
                Unidad = "UN",
                MatUf = 0.01m,
                MoUf = 0.005m
            });
        }

        await host.ProjectWorkspace.SaveAsAsync(path);
        Assert.True(host.Session.Cubicacion.Count >= 200);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            vm.AddRowCommand.Execute(null);
            var newRow = vm.SelectedRow!;
            var material = host.Session.Materiales[i];
            vm.ApplyMaterialCommand.Execute(material);
            Assert.Equal(material.Codigo, newRow.Codigo);
        }
        sw.Stop();

        double perRowMs = sw.Elapsed.TotalMilliseconds / 20.0;
        Assert.True(perRowMs < 100.0,
            $"ApplyMaterial (fila nueva) tardó {perRowMs:N2} ms en promedio (límite: 100 ms) con un proyecto de {host.Session.Cubicacion.Count} recursos y 500 materiales.");
    }
}
