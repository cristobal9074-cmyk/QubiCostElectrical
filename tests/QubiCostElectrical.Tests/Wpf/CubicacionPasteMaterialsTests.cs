using System;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using CubicacionPageVm = QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionViewModel;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Pegado de una columna de códigos en la grilla de Cubicación: cada código debe convertirse en
/// material (Descripción/UD) y no dejarse solo como texto crudo.
///
/// Regresión del 11-10: el comportamiento Excel de la grilla engancha Ctrl+V en PreviewKeyDown y
/// marcaba el evento como atendido, lo que suprimía el KeyDown que burbujeaba hasta el UserControl
/// donde vivía el pegado con lógica de negocio. Al pegar códigos entraba solo el texto y la
/// Descripción quedaba en blanco. Se corrige con <c>HandlePasteByHost</c> en la grilla (el pegado
/// genérico se aparta) más el Ctrl+V atendido en la propia grilla.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionPasteMaterialsTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Takeoff;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false) { CurrentRoute = route; return true; }
        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) => RequestNavigate(route, force);
        public WpfAppHost Host => WpfAppHost.Current;
        public System.Threading.Tasks.Task NewProjectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task OpenProjectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<bool> CloseProjectAsync() => System.Threading.Tasks.Task.FromResult(true);
        public System.Threading.Tasks.Task OpenRecentProjectAsync(string path) => System.Threading.Tasks.Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public System.Threading.Tasks.Task SaveProjectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task SaveProjectAsAsync() => System.Threading.Tasks.Task.CompletedTask;
        public void RefreshChrome() { }
    }

    public CubicacionPasteMaterialsTests() => WpfTestHost.EnsureInitialized();

    /// <summary>Prepara un proyecto con tres materiales en el catálogo y una partida activa.</summary>
    private static CubicacionPageVm NuevoVmConCatalogo()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject($"cubicacion-pegado-{Guid.NewGuid():N}");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Ductos y cajas",
            Unidad = "GL",
            Cantidad = 1m
        });

        host.Session.Materiales.Add(new MaterialRow { Codigo = "PV6S40", Descripcion = "PVC CONDUIT 6\" (140 mm) SUBTERRANEO", Unidad = "ML", MatClp = 1200m });
        host.Session.Materiales.Add(new MaterialRow { Codigo = "SOPORTE", Descripcion = "SOPORTE TIPO A", Unidad = "UN", MatClp = 800m });
        host.Session.Materiales.Add(new MaterialRow { Codigo = "PVC1", Descripcion = "PVC CONDUIT 1\"", Unidad = "ML", MatClp = 450m });

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        return vm;
    }

    private static System.Windows.Controls.DataGrid GridAncladoEnCodigo(CubicacionPageVm vm)
    {
        var grid = new System.Windows.Controls.DataGrid { ItemsSource = vm.Rows, SelectedItem = vm.Rows.First() };
        var col = new System.Windows.Controls.DataGridTextColumn { Header = "CÓDIGO" };
        grid.Columns.Add(col);
        grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(vm.Rows.First(), col);
        return grid;
    }

    private static void EnSta(Action action)
    {
        Exception? captured = null;
        var thread = new System.Threading.Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(captured).Throw();
    }

    [Fact]
    public void PegarColumnaDeCodigos_CompletaDescripcionYUnidadDeCadaMaterial()
    {
        EnSta(() =>
        {
            var vm = NuevoVmConCatalogo();
            vm.SelectedRow = vm.Rows.First();
            var grid = GridAncladoEnCodigo(vm);

            // Tres códigos, uno por línea: lo que Excel entrega al copiar una columna.
            bool ok = vm.TryPasteAnchored(grid, "PV6S40\nSOPORTE\nPVC1");

            Assert.True(ok);
            Assert.Equal("PV6S40", vm.Rows[0].Codigo);
            Assert.Equal("SOPORTE", vm.Rows[1].Codigo);
            Assert.Equal("PVC1", vm.Rows[2].Codigo);

            // El punto del reporte: los materiales deben "ponerse", no quedar en blanco.
            Assert.Equal("PVC CONDUIT 6\" (140 mm) SUBTERRANEO", vm.Rows[0].Descripcion);
            Assert.Equal("SOPORTE TIPO A", vm.Rows[1].Descripcion);
            Assert.Equal("PVC CONDUIT 1\"", vm.Rows[2].Descripcion);
            Assert.Equal("ML", vm.Rows[0].Unidad);
            Assert.Equal("UN", vm.Rows[1].Unidad);
            Assert.Equal("ML", vm.Rows[2].Unidad);
        });
    }

    [Fact]
    public void PegarBloqueDeDosColumnasEnCasillas_LlenaFilasYColumnasComoExcel()
    {
        EnSta(() =>
        {
            var vm = NuevoVmConCatalogo();
            vm.SelectedRow = vm.Rows.First();

            var grid = new System.Windows.Controls.DataGrid { ItemsSource = vm.Rows, SelectedItem = vm.Rows.First() };
            var codigo = new System.Windows.Controls.DataGridTextColumn { Header = "CÓDIGO" };
            var s1 = new DisplayBindingTextColumn { Header = "S1", SectorSlot = 0 };
            var s2 = new DisplayBindingTextColumn { Header = "S2", SectorSlot = 1 };
            grid.Columns.Add(codigo);
            grid.Columns.Add(s1);
            grid.Columns.Add(s2);
            grid.CurrentCell = new System.Windows.Controls.DataGridCellInfo(vm.Rows.First(), s1);

            // Bloque 2×2 anclado en la primera casilla: es la segunda imagen del reporte
            // (dos columnas de cantidades) y debe caer fila por fila y columna por columna.
            bool ok = vm.TryPasteAnchored(grid, "20\t2\n4\t4");

            Assert.True(ok);
            Assert.Equal("20", vm.Rows[0].SectorValues[0]);
            Assert.Equal("2", vm.Rows[0].SectorValues[1]);
            Assert.Equal("4", vm.Rows[1].SectorValues[0]);
            Assert.Equal("4", vm.Rows[1].SectorValues[1]);
            Assert.Equal(22m, vm.Rows[0].Cantidad);
            Assert.Equal(8m, vm.Rows[1].Cantidad);
        });
    }

    [Fact]
    public void GrillaDeCubicacion_DeclaraHandlePasteByHost_YConservaNavegacionExcel()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var vm = NuevoVmConCatalogo();
                var view = new CubicacionView(vm);
                XamlViewSmokeHost.Pump();

                var grid = view.FindName("ResourceGrid") as System.Windows.Controls.DataGrid;
                Assert.NotNull(grid);

                // Sin HandlePasteByHost el pegado genérico se adelanta y el pegado con lógica de
                // negocio queda muerto; sin Enable se pierde la navegación tipo Excel.
                Assert.True(DataGridExcelNavigation.GetHandlePasteByHost(grid!),
                    "La grilla de Cubicación debe dejar el pegado a su dueño (HandlePasteByHost).");
                Assert.True(DataGridExcelNavigation.GetEnable(grid!),
                    "La grilla de Cubicación debe conservar la navegación tipo Excel.");
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
            }
        });
    }

    [Fact]
    public void HandlePasteByHost_EsFalsoPorDefecto_ParaNoCambiarLasDemasGrillas()
    {
        EnSta(() =>
        {
            var grid = new System.Windows.Controls.DataGrid();
            Assert.False(DataGridExcelNavigation.GetHandlePasteByHost(grid));

            DataGridExcelNavigation.SetHandlePasteByHost(grid, true);
            Assert.True(DataGridExcelNavigation.GetHandlePasteByHost(grid));
        });
    }
}
