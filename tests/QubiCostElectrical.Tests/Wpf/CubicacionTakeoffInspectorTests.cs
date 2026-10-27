using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionTakeoffInspectorTests
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

    public CubicacionTakeoffInspectorTests() => WpfTestHost.EnsureInitialized();

    [Fact]
    public void OpenForSector_ConfiguraTargetYSectorSlotCorrectamente()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("test-takeoff-sec");
        host.Session.ZonasProyecto.Clear();
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Nombre = "ZONA 1", Activa = true, Orden = 1 });
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Nombre = "ZONA 2", Activa = true, Orden = 2 });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1", Unidad = "GL", Cantidad = 1 });

        var vm = new CubicacionViewModel(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = vm.Rows[0];
        row.Codigo = "MAT-01";
        row.Descripcion = "Tubería Conduit";

        var coordinator = vm.TakeoffInspector;
        coordinator.OpenForSector(row, 0);

        Assert.True(coordinator.IsInspectorOpen);
        Assert.Equal(0, coordinator.TargetSectorSlot);
        Assert.Equal("ZONA 1", coordinator.TargetSectorName);
        Assert.Contains("MAT-01", coordinator.TargetRowText);
    }

    [Fact]
    public void OpenForQuantity_ConfiguraTargetACantidadCorrectamente()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("test-takeoff-qty");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1", Unidad = "GL", Cantidad = 1 });

        var vm = new CubicacionViewModel(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = vm.Rows[0];
        row.Codigo = "MAT-01";
        row.Descripcion = "Tubería Conduit";

        var coordinator = vm.TakeoffInspector;
        coordinator.OpenForQuantity(row);

        Assert.True(coordinator.IsInspectorOpen);
        Assert.Equal(-1, coordinator.TargetSectorSlot);
        Assert.Equal("CANT", coordinator.TargetSectorName);
        Assert.Contains("MAT-01", coordinator.TargetRowText);
    }

    [Fact]
    public void TransferFilaValue_ConSectorActivo_InyectaValorEnElSector()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("test-takeoff-transfer");
        host.Session.ZonasProyecto.Clear();
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Nombre = "ZONA 1", Activa = true, Orden = 1 });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1", Unidad = "GL", Cantidad = 1 });

        var vm = new CubicacionViewModel(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = vm.Rows[0];
        row.Codigo = "MAT-01";

        var coordinator = vm.TakeoffInspector;
        coordinator.OpenForSector(row, 0);

        var item = coordinator.SelectedItem!;
        var fila = item.AddFila(4, "Tramo Sala a Tablero");
        fila.Valores[0] = "55.5";
        item.RefreshAllDisplays();

        coordinator.TransferFilaValue(fila, 0);

        Assert.True(fila.IsPicked);
        Assert.Equal($"row:{row.Source.Id}:sec:0", fila.PickedTargetKey);
        Assert.Equal("55.5", row.Source.QtyExpressions[0]);
        Assert.Equal("55.5", row.SectorValues[0]);
    }

    [Fact]
    public void TransferFilaValue_ConCantidadActiva_InyectaSumaTotalEnCantidad()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("test-takeoff-qty-sum");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1", Unidad = "GL", Cantidad = 1 });

        var vm = new CubicacionViewModel(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = vm.Rows[0];

        var coordinator = vm.TakeoffInspector;
        coordinator.OpenForQuantity(row);

        var item = coordinator.SelectedItem!;
        var fila = item.AddFila(4, "Tramo Sala a Tablero");
        fila.Valores[0] = "20";
        fila.Valores[1] = "30";
        item.RefreshAllDisplays();

        coordinator.TransferFilaValue(fila, null); // null -> Suma

        Assert.True(fila.IsPicked);
        Assert.Equal($"row:{row.Source.Id}:qty", fila.PickedTargetKey);
        Assert.Equal(50m, row.Cantidad);
    }

    [Fact]
    public void ClearAllPicked_DesmarcaTodasLasFilasEItems()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("test-takeoff-clear");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Partida 1", Unidad = "GL", Cantidad = 1 });

        var vm = new CubicacionViewModel(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        var row = vm.Rows[0];

        var coordinator = vm.TakeoffInspector;
        coordinator.OpenForQuantity(row);

        var item = coordinator.SelectedItem!;
        var fila = item.AddFila(4, "Tramo 1");
        fila.Valores[0] = "42";
        item.RefreshAllDisplays();

        coordinator.TransferFilaValue(fila, null);
        Assert.True(fila.IsPicked);

        coordinator.ClearAllPicked();
        Assert.False(fila.IsPicked);
        Assert.Null(fila.PickedTargetKey);
    }
}
