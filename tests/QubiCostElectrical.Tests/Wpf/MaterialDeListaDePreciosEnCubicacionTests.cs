using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Alta de materiales desde la lista de precios (Alt+V / Alt+S): un tablero o una luminaria puntual
/// es del PRESUPUESTO, no del banco de precios global compartido por todos los proyectos, así que
/// debe nacer en <c>session.Materiales</c> y nunca escribirse en SQLite. Al crearlo baja solo a la
/// partida activa de la cubicación y, con el FA sin calibrar, ya muestra Material × % con factor 1
/// (una cifra provisional visible en vez de $0) que se recalcula sola cuando el FA se mueve en Costos.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class MaterialDeListaDePreciosEnCubicacionTests
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

    /// <summary>Banco global vacío que cuenta toda escritura: si el material del proyecto se colara, el test falla.</summary>
    private sealed class BancoGlobalVigilado : IPriceBankStore
    {
        private readonly List<PriceBankEntry> entries = new();

        public int Escrituras { get; private set; }

        public IReadOnlyList<PriceBankEntry> GetAll() => entries.ToList();
        public PriceBankEntry? GetByCodigo(string codigo) => entries.FirstOrDefault(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<PriceBankEntry> Search(string text) => entries.ToList();
        public void Upsert(PriceBankEntry entry) { Escrituras++; entries.Add(entry); }
        public bool Delete(string codigo) => entries.RemoveAll(e => string.Equals(e.Codigo, codigo, StringComparison.OrdinalIgnoreCase)) > 0;
        public int DeleteAll() { int borradas = entries.Count; entries.Clear(); return borradas; }
        public int UpsertMany(IEnumerable<PriceBankEntry> newEntries) { Escrituras++; entries.AddRange(newEntries); return entries.Count; }
        public int Count() => entries.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> seed) => 0;
    }

    public MaterialDeListaDePreciosEnCubicacionTests() => WpfTestHost.EnsureInitialized();

    /// <summary>El .qce abierto queda con la conexión SQLite viva: se borra sin exigir éxito.</summary>
    private static void Limpiar(string path)
    {
        foreach (string p in new[] { path, path + ".autosave", path + ".lock" })
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch { }
        }
    }

    [Fact]
    public async Task MaterialCreadoEnLaListaDePrecios_QuedaEnElProyecto_YSeAsignaALaPartida_ConFaSinCalibrar()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"material-de-proyecto-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("material-de-proyecto");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Tableros",
            Unidad = "UN",
            Cantidad = 1m
        });
        host.Session.ProjectInfo.IndiceMoFa = 0m; // proyecto recién creado: FA sin calibrar
        await host.ProjectWorkspace.SaveAsAsync(path);

        var banco = new BancoGlobalVigilado();
        var lista = new PrecioListaResumenVm(host.Session, banco);

        bool creado = lista.AgregarMaterial(
            "TAB-001",
            "TABLERO ELÉCTRICO 12 CIRCUITOS",
            "UN",
            "Equipo",
            matClp: 250_000m,
            moClp: 0m,
            proveedor: "",
            fechaPrecio: DateTime.Today,
            moFormulaTipo: MaterialMoResolver.MoFormulaPorcentaje,
            moPorcentajeMaterial: 2.5m);

        Assert.True(creado);
        MaterialRow material = Assert.Single(host.Session.Materiales);
        Assert.Equal(0, banco.Count());
        Assert.Equal(0, banco.Escrituras);

        // FA sin calibrar: la M.O. se muestra igual, como Material × % con factor 1.
        Assert.Equal(6_250m, material.MoClp);

        // Baja solo a la partida activa, sin duplicar el catálogo del proyecto.
        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.SelectedRow = vm.Rows[0];
        vm.AplicarMaterialCreado("TAB-001");

        Assert.Equal("TAB-001", vm.SelectedRow!.Codigo);
        Assert.Equal(6_250m, vm.SelectedRow.MoClp);
        Assert.Single(host.Session.Materiales);

        // FA movido en Costos: material y cubicación se recalculan solos, y el banco global sigue intacto.
        host.Session.AplicarIndiceMoFa(1.5m, banco);

        Assert.Equal(9_375m, host.Session.Materiales[0].MoClp);
        Assert.Equal(9_375m, vm.SelectedRow.MoClp);
        Assert.Equal(0, banco.Count());
        Assert.Equal(0, banco.Escrituras);

        Limpiar(path);
    }

    /// <summary>
    /// El material sobrevive al guardado y, al reabrir el .qce, sigue siendo del proyecto: la lista
    /// de precios lo vuelve a mostrar como fila local y editarlo no escribe en el banco compartido.
    /// </summary>
    [Fact]
    public async Task MaterialDelProyecto_SobreviveAlGuardado_YAlReabrirSigueSiendoSoloDelProyecto()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"material-persistido-{Guid.NewGuid():N}.qce");
        try
        {
            host.Session.NewProject("material-persistido");
            host.Session.Itemizado.Add(new ItemizadoRow
            {
                Id = "P1",
                Item = "1.1",
                Descripcion = "Luminarias",
                Unidad = "UN",
                Cantidad = 1m
            });
            host.Session.ProjectInfo.IndiceMoFa = 0m;

            var banco = new BancoGlobalVigilado();
            var lista = new PrecioListaResumenVm(host.Session, banco);
            Assert.True(lista.AgregarMaterial(
                "LUM-001",
                "LUMINARIA LED 40W",
                "UN",
                "Luminarias",
                matClp: 80_000m,
                moClp: 0m,
                proveedor: "",
                fechaPrecio: DateTime.Today,
                moFormulaTipo: MaterialMoResolver.MoFormulaPorcentaje,
                moPorcentajeMaterial: 2.5m));

            await host.ProjectWorkspace.SaveAsAsync(path);
            await host.ProjectWorkspace.OpenAsync(path);

            MaterialRow recargado = Assert.Single(host.Session.Materiales);
            Assert.Equal("LUM-001", recargado.Codigo);
            Assert.Equal(2_000m, recargado.MoClp); // 80.000 × 2,5 % con FA sin calibrar

            // El banco global quedó limpio incluso después de todo el ciclo.
            Assert.Equal(0, new BancoGlobalVigilado().Count());

            // Al reabrir la lista, la fila sigue marcada como del proyecto: editarla no toca el banco.
            var bancoReapertura = new BancoGlobalVigilado();
            var listaReabierta = new PrecioListaResumenVm(host.Session, bancoReapertura);
            PrecioListaMaterialVm fila = Assert.Single(
                listaReabierta.Items.OfType<PrecioListaMaterialVm>(), i => i.Codigo == "LUM-001");
            Assert.True(fila.EsSoloProyecto);

            fila.MatClp = 90_000m;
            listaReabierta.ActualizarPrecioEnSesion(fila);

            Assert.Equal(0, bancoReapertura.Escrituras);
            Assert.Equal(90_000m, host.Session.Materiales[0].MatClp);
        }
        finally
        {
            Limpiar(path);
        }
    }

    /// <summary>
    /// La cantidad no se escribe en CANT (esa columna es calculada), así que al crear el material se
    /// pregunta zona y cantidad y se llena la CASILLA de esa zona — lo que deja la fila cubicada con
    /// cantidad real en vez de quedar en 0. El índice de la casilla es el de la zona activa ordenada
    /// por <c>Orden</c>, igual que el encabezado de la columna: escribir en la casilla 2 no debe tocar
    /// la casilla 1.
    /// </summary>
    [Fact]
    public async Task CasillaDeZonaElegida_DejaLaFilaCubicada_ConLaCantidadIndicada()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"casilla-de-zona-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("casilla-de-zona");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Arranque 220 volts",
            Unidad = "UN",
            Cantidad = 0m
        });
        host.Session.ProjectInfo.IndiceMoFa = 0m;

        // Zonas desordenadas a propósito: manda Orden, no el orden de la lista.
        host.Session.ZonasProyecto.Clear();
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Id = "z2", Nombre = "Piso 2", Orden = 1, Activa = true });
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Id = "z1", Nombre = "Subterráneo", Orden = 0, Activa = true });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var banco = new BancoGlobalVigilado();
        var lista = new PrecioListaResumenVm(host.Session, banco);
        Assert.True(lista.AgregarMaterial(
            "GG-003",
            "GENERADOR 150KVA",
            "UN",
            "Equipo",
            matClp: 10_000_000m,
            moClp: 0m,
            proveedor: "",
            fechaPrecio: DateTime.Today));

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        vm.SelectedRow = vm.Rows[0];
        vm.AplicarMaterialCreado("GG-003");
        Assert.Equal("GG-003", vm.SelectedRow!.Codigo);

        // El índice de casilla es el de la zona activa ordenada por Orden: la columna 1 es la de
        // menor Orden, no la primera agregada a la lista.
        Assert.Equal("Subterráneo", vm.VisibleSectorHeaders[0]);
        Assert.Equal("Piso 2", vm.VisibleSectorHeaders[1]);

        // Lo que hace el diálogo al pulsar "Cubicar": escribe la cantidad en la casilla elegida.
        vm.rowMutationService.AplicarCasillaDeCubicacion("GG-003", 1, "3");

        Assert.Equal(3m, vm.SelectedRow.Cantidad);
        CubicacionRow persistida = host.Session.Cubicacion.Single(r => r.ItemId == "P1");
        Assert.Equal("3", persistida.QtyExpressions[1]);
        Assert.True(string.IsNullOrEmpty(persistida.QtyExpressions[0])); // la otra casilla sigue vacía

        // Un código que no es el de la fila activa no debe escribir nada (el material se aplica a
        // una fila concreta; un nombre cambiado en el camino no debe cubicar otra fila).
        vm.rowMutationService.AplicarCasillaDeCubicacion("OTRO-999", 0, "7");
        Assert.Equal(3m, vm.SelectedRow.Cantidad);
        Assert.True(string.IsNullOrEmpty(persistida.QtyExpressions[0]));

        // "Deshacer" del aviso revierte la acción COMPLETA (material + casilla) en un solo paso: si
        // la casilla empujara su propio punto de undo, este primer deshacer dejaría el código puesto.
        host.UndoService.Undo(host.Session);
        foreach (var fila in host.Session.Cubicacion.Where(r => r.ItemId == "P1"))
        {
            Assert.True(string.IsNullOrEmpty(fila.Codigo));
            Assert.Equal(0m, fila.Cantidad);
        }

        Limpiar(path);
    }

    /// <summary>El diálogo acepta la cantidad como la teclea el usuario ("1,5") y rechaza basura.</summary>
    [Theory]
    [InlineData("1", 1)]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)]
    [InlineData(" 12 ", 12)]
    public void CantidadDeLaCasilla_AceptaEnterosYDecimales(string texto, double esperado)
    {
        Assert.True(ZonaCantidadDialog.TryLeerCantidad(texto, out decimal cantidad));
        Assert.Equal((decimal)esperado, cantidad);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dos")]
    [InlineData("1,5,5")]
    public void CantidadDeLaCasilla_RechazaValoresInvalidos(string texto)
        => Assert.False(ZonaCantidadDialog.TryLeerCantidad(texto, out _));
}
