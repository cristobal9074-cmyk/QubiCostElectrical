using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed partial class CubicacionViewModelTests
{
    [Fact]
    public async Task MoverFila_GuardarAbrirYDeshacer_ConservaOrdenFormulasYUnidades()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-orden-{System.Guid.NewGuid():N}.qce");
        host.UndoService.Clear();
        host.Session.NewProject("orden-partida");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "m", Cantidad = 10m });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Otra", Unidad = "un", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R1", ItemId = "P1", Codigo = "CAB", Descripcion = "Cable", Unidad = "m", Cantidad = 75m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "RX", ItemId = "P2", Codigo = "OTRO", Descripcion = "Ajena", Unidad = "un", Cantidad = 1m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            Id = "R2", ItemId = "P1", Codigo = "TUB", Descripcion = "Tubo", Unidad = "ml", Cantidad = 12m,
            QtyExpressions = ["12"]
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.SelectedRow = vm.Rows.Single(r => r.Codigo == "TUB");

        Assert.True(vm.MoveRowUpCommand.CanExecute(null));
        vm.MoveRowUpCommand.Execute(null);

        Assert.Equal(["TUB", "CAB"], vm.Rows.Where(r => r.IsSessionBacked).Select(r => r.Codigo));
        Assert.Equal(["TUB", "CAB"], host.Session.Cubicacion.Where(r => r.ItemId == "P1").Select(r => r.Codigo));
        Assert.Equal("OTRO", host.Session.Cubicacion[1].Codigo);
        Assert.Equal("12", host.Session.Cubicacion.Single(r => r.Codigo == "TUB").QtyExpressions![0]);
        Assert.Equal("ml", host.Session.Cubicacion.Single(r => r.Codigo == "TUB").Unidad);
        Assert.All(vm.Rows.Where(r => !r.IsSessionBacked), r => Assert.False(host.Session.Cubicacion.Any(s => s.Id == r.Id)));

        var saved = Path.Combine(Path.GetTempPath(), $"cubicacion-orden-guardado-{System.Guid.NewGuid():N}.qce");
        await host.ProjectWorkspace.SaveAsAsync(saved);
        await host.ProjectWorkspace.OpenAsync(saved);
        var reloaded = host.Session.Cubicacion.Where(r => r.ItemId == "P1").ToList();
        Assert.Equal(["TUB", "CAB"], reloaded.Select(r => r.Codigo));
        Assert.Equal("12", reloaded[0].QtyExpressions![0]);
        Assert.Equal("ml", reloaded[0].Unidad);
        Assert.Equal(12m, reloaded[0].Cantidad);
        Assert.Equal(75m, reloaded[1].Cantidad);

        Assert.True(host.UndoService.Undo(host.Session));
        vm.Refresh();
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        Assert.Equal(["CAB", "TUB"], vm.Rows.Where(r => r.IsSessionBacked).Select(r => r.Codigo));
        Assert.Equal(["CAB", "TUB"], host.Session.Cubicacion.Where(r => r.ItemId == "P1").Select(r => r.Codigo));
    }

    [Fact]
    public void MoverDiezVeces_NoDesfasaLaGrilla_YNoEmpujaUndoSiNoHayVecino()
    {
        var host = WpfAppHost.Current;
        host.UndoService.Clear();
        host.Session.NewProject("orden-diez");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "m", Cantidad = 10m });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Otra", Unidad = "un", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "R1", ItemId = "P1", Codigo = "CAB", Unidad = "m", Cantidad = 75m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "RX", ItemId = "P2", Codigo = "OTRO", Unidad = "un", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "R2", ItemId = "P1", Codigo = "TUB", Unidad = "ml", Cantidad = 12m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.SelectedRow = vm.Rows.Single(r => r.Codigo == "TUB");

        for (int i = 0; i < 10; i++)
        {
            vm.MoveRowUpCommand.Execute(null);
            int undoAtTop = host.UndoService.UndoCount;
            vm.MoveRowUpCommand.Execute(null);
            Assert.Equal(undoAtTop, host.UndoService.UndoCount);
            vm.MoveRowDownCommand.Execute(null);
        }

        Assert.Equal(["CAB", "TUB"], vm.Rows.Where(r => r.IsSessionBacked).Select(r => r.Codigo));
        Assert.Equal(
            vm.Rows.Where(r => r.IsSessionBacked).Select(r => r.Codigo),
            host.Session.Cubicacion.Where(r => r.ItemId == "P1").Select(r => r.Codigo));
        Assert.Equal(["CAB", "OTRO", "TUB"], host.Session.Cubicacion.Select(r => r.Codigo));
    }

    [Fact]
    public void Pegar_InsertaAlFinalDeLaPartida_NoDespuesDeLasCasillasVacias()
    {
        var host = WpfAppHost.Current;
        host.UndoService.Clear();
        host.Session.NewProject("orden-pegar");
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Canalización", Unidad = "m", Cantidad = 10m });
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "Otra", Unidad = "un", Cantidad = 1m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "R1", ItemId = "P1", Codigo = "CAB", Unidad = "m", Cantidad = 75m });
        host.Session.Cubicacion.Add(new CubicacionRow { Id = "RX", ItemId = "P2", Codigo = "OTRO", Unidad = "un", Cantidad = 1m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        int undoBefore = host.UndoService.UndoCount;
        vm.ApplyClipboardRows(
        [
            new CubicacionClipboardRow { Codigo = "TUB", Descripcion = "Tubo", Unidad = "ml", CantidadRaw = "12" }
        ]);

        Assert.True(host.UndoService.UndoCount > undoBefore);
        Assert.Equal(["CAB", "TUB"], host.Session.Cubicacion.Where(r => r.ItemId == "P1").Select(r => r.Codigo));
        Assert.Equal(["OTRO"], host.Session.Cubicacion.Where(r => r.ItemId == "P2").Select(r => r.Codigo));
        var backed = vm.Rows.Where(r => r.IsSessionBacked).ToList();
        Assert.Equal(["CAB", "TUB"], backed.Select(r => r.Codigo));
        Assert.True(vm.Rows.IndexOf(backed[^1]) < vm.Rows.IndexOf(vm.Rows.First(r => !r.IsSessionBacked)));
    }
}
