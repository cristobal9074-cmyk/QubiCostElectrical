using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Propagación maestro→esclavo en la Hoja de Cubicación: las filas con
/// CubicacionRow.Esclavo derivan su cantidad de la fila maestro con el mismo código
/// (columna CANT y casillas de zona), usando ConjuntoElectricoService.ComputeEsclavoValue.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionEsclavoPropagationTests
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

    public CubicacionEsclavoPropagationTests() => WpfTestHost.EnsureInitialized();

    private static CubicacionRow Row(string id, string codigo, decimal cantidad, CubicacionEsclavoVinculo? esclavo = null, string[]? qtyExpressions = null, string operacion = "")
        => new()
        {
            Id = id,
            ItemId = "P1",
            Tipo = "MAT",
            Codigo = codigo,
            Descripcion = codigo,
            Unidad = "ML",
            Cantidad = cantidad,
            Area = "General",
            Esclavo = esclavo,
            Operacion = operacion,
            QtyExpressions = qtyExpressions ?? Array.Empty<string>()
        };

    private static CubicacionPageVm BuildVm(params CubicacionRow[] rows)
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject($"esclavo-prop-{Guid.NewGuid():N}");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = 10m
        });
        foreach (var row in rows)
            host.Session.Cubicacion.Add(row);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        return vm;
    }

    private static CubicacionRowVm Find(CubicacionPageVm vm, string id)
        => vm.Rows.Single(r => r.Source.Id == id);

    [Fact]
    public void EditarCantidadDelMaestro_PropagaASuEsclavo_YElEsclavoNoPropaga()
    {
        var master = Row("m1", "TUBO20", 0m);
        var slave = Row("e1", "ABR", 0m, new CubicacionEsclavoVinculo { MaestroCodigo = "TUBO20", Factor = 2m });
        var vm = BuildVm(master, slave);
        var masterVm = Find(vm, "m1");
        var slaveVm = Find(vm, "e1");

        masterVm.Cantidad = 20m;

        Assert.Equal(20m, masterVm.Cantidad);
        Assert.Equal(40m, slaveVm.Cantidad);
        Assert.Equal(40m, slaveVm.Source.Cantidad);

        // Sin cadenas: editar el esclavo no retroalimenta al maestro ni a otros esclavos.
        slaveVm.Cantidad = 5m;
        Assert.Equal(20m, masterVm.Cantidad);
    }

    [Fact]
    public void EditarCasillaDeZonaDelMaestro_PropagaElValorDeLaCasilla()
    {
        var master = Row("m1", "TUBO20", 0m);
        var slave = Row("e1", "ABR", 0m, new CubicacionEsclavoVinculo { MaestroCodigo = "TUBO20", Factor = 2m });
        var vm = BuildVm(master, slave);
        var masterVm = Find(vm, "m1");
        var slaveVm = Find(vm, "e1");

        vm.SetSectorExpressionDirect(masterVm, 0, "12");
        Assert.Equal(12m, masterVm.Cantidad);
        Assert.Equal(24m, slaveVm.Cantidad);

        // Borrar la casilla del maestro limpia la del esclavo.
        vm.SetSectorExpressionDirect(masterVm, 0, "");
        Assert.Equal(0m, masterVm.Cantidad);
        Assert.Equal(0m, slaveVm.Cantidad);
    }

    [Fact]
    public void EsclavoConZonas_RecibeLaCasillaCorrespondienteDelMaestro()
    {
        var master = Row("m1", "TUBO20", 0m);
        var slave = Row(
            "e1", "ABR", 3m,
            new CubicacionEsclavoVinculo { MaestroCodigo = "TUBO20", Rendimiento = 1m, Factor = 2m },
            qtyExpressions: new[] { "3" });
        var vm = BuildVm(master, slave);
        var masterVm = Find(vm, "m1");
        var slaveVm = Find(vm, "e1");

        Assert.True(slaveVm.HasFormula);

        vm.SetSectorExpressionDirect(masterVm, 0, "12");

        // El esclavo con zonas guarda la fórmula resuelta en la MISMA casilla del maestro.
        Assert.Equal("24", slaveVm.Source.QtyExpressions[0]);
        Assert.Equal(24m, slaveVm.Cantidad);
        Assert.Equal(12m, masterVm.Cantidad);

        // Casilla 1 del maestro no afecta la casilla 0 del esclavo.
        vm.SetSectorExpressionDirect(masterVm, 1, "10");
        Assert.Equal("24", slaveVm.Source.QtyExpressions[0]);
        Assert.Equal("20", slaveVm.Source.QtyExpressions[1]);
        Assert.Equal(44m, slaveVm.Cantidad);
    }

    [Fact]
    public void EditarCantidadDelMaestro_PropagaASuEsclavo_ModoEspaciamiento()
    {
        var master = Row("m1", "TUBO20", 0m);
        var slave = Row(
            "e1", "ABR", 0m,
            new CubicacionEsclavoVinculo
            {
                MaestroCodigo = "TUBO20",
                TipoCalculo = TipoCalculoParametrico.Espaciamiento,
                Espaciamiento = 1.5m
            });
        var vm = BuildVm(master, slave);
        var masterVm = Find(vm, "m1");
        var slaveVm = Find(vm, "e1");

        masterVm.Cantidad = 1000m;

        // Ceil(1000 / 1.5) = 667
        Assert.Equal(667m, slaveVm.Cantidad);
    }

    [Fact]
    public void EsclavoSinMaestroVisible_NoSeToca()
    {
        var master = Row("m1", "TUBO20", 0m);
        var otro = Row("e2", "CINTA", 0m, new CubicacionEsclavoVinculo { MaestroCodigo = "OTRO-CODIGO" });
        var vm = BuildVm(master, otro);
        var masterVm = Find(vm, "m1");
        var otroVm = Find(vm, "e2");

        masterVm.Cantidad = 50m;

        Assert.Equal(0m, otroVm.Cantidad);
    }

    // ── Recálculo en vivo de filas Operación ──────────────────────────────────

    [Fact]
    public void EditarCantidadDeFilaReferenciada_RecalculaLaOperacionEnVivo()
    {
        // Ojo: una fila con cantidad inicial ≠ 0 recibe fórmula de zona al cargar y su
        // edición de CANT se ignora (zonas mandan). Para probar el recálculo por CANT,
        // las filas referenciadas parten en cero.
        var paz = Row("m1", "PAZ", 0m);
        var teeg = Row("m2", "TEEG500", 0m);
        var op = Row("op1", "OP1", 0m, operacion: "PAZ * 2 + TEEG500");
        var vm = BuildVm(paz, teeg, op);
        var pazVm = Find(vm, "m1");
        var teegVm = Find(vm, "m2");
        var opVm = Find(vm, "op1");

        pazVm.Cantidad = 10m;
        Assert.Equal(20m, opVm.Cantidad); // 10*2 + 0
        Assert.Equal(20m, opVm.Source.Cantidad);

        // Cualquier fila referenciada dispara el recálculo (la segunda también).
        teegVm.Cantidad = 6m;
        Assert.Equal(26m, opVm.Cantidad); // 10*2 + 6
        Assert.Equal(26m, opVm.Source.Cantidad);
    }

    [Fact]
    public void Operacion_PropagaSuValorASuEsclavo_YEsteNoPropagaMas()
    {
        var paz = Row("m1", "PAZ", 0m);
        var op = Row("op1", "OP1", 0m, operacion: "PAZ * 2");
        var esclavo = Row("e1", "DER", 0m, new CubicacionEsclavoVinculo { MaestroCodigo = "OP1", Factor = 2m });
        var vm = BuildVm(paz, op, esclavo);
        var pazVm = Find(vm, "m1");
        var opVm = Find(vm, "op1");
        var esclavoVm = Find(vm, "e1");

        pazVm.Cantidad = 5m;

        Assert.Equal(10m, opVm.Cantidad);   // PAZ * 2
        Assert.Equal(20m, esclavoVm.Cantidad); // 10 x 2 (la operación propaga a su esclavo)
        Assert.Equal(20m, esclavoVm.Source.Cantidad);
    }

    [Fact]
    public void EditarManualmenteLaOperacion_SeRevierteAlValorDeSuFormula()
    {
        var paz = Row("m1", "PAZ", 5m);
        var op = Row("op1", "OP1", 0m, operacion: "PAZ * 3");
        var vm = BuildVm(paz, op);
        var opVm = Find(vm, "op1");

        opVm.Cantidad = 99m;

        // La fórmula manda: la edición manual se revierte (5 * 3 = 15).
        Assert.Equal(15m, opVm.Cantidad);
        Assert.Equal(15m, opVm.Source.Cantidad);
    }

    [Fact]
    public void OperacionesEnCiclo_NoCuelganNiSeRecalculan()
    {
        var a = Row("op-a", "OPA", 0m, operacion: "OPB * 2");
        var b = Row("op-b", "OPB", 0m, operacion: "OPA + 1");
        var vm = BuildVm(a, b);
        var aVm = Find(vm, "op-a");
        var bVm = Find(vm, "op-b");

        // Editar una fila en ciclo no debe colgar ni tocar la otra (excluidas con advertencia).
        aVm.Cantidad = 1m;

        Assert.Equal(1m, aVm.Cantidad);
        Assert.Equal(0m, bVm.Cantidad);
    }
}
