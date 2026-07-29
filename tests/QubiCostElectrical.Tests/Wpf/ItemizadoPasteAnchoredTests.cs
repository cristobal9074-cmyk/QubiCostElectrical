using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Cubre los 4 huecos silenciosos del pegado anclado (TryPasteAnchored) detectados en la
/// revisión CEO: la grilla no crecía con pegados grandes, las filas en blanco del origen
/// desalineaban el resto, el mapeo de columnas dependía del texto del encabezado, y las
/// columnas sobrantes se descartaban sin aviso.
///
/// DataGrid y el ObservableCollection que respalda ItemizadoViewModel.Rows exigen afinidad de
/// hilo (Dispatcher/STA); este proyecto no tiene infraestructura [StaFact], así que cada test
/// construye la VM, la grilla y ejecuta el pegado dentro del mismo hilo STA vía RunOnSta, sin
/// agregar dependencias nuevas.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ItemizadoPasteAnchoredTests
{
    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Itemized;
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

    public ItemizadoPasteAnchoredTests() => WpfTestHost.EnsureInitialized();

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? capturedError = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { capturedError = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        capturedError?.Throw();
    }

    /// <summary>
    /// Replica las 5 columnas editables de ItemizadoView.xaml. El binding path de cada
    /// columna es lo que TryPasteAnchored usa para mapear la columna anclada (no el Header).
    /// </summary>
    private static DataGrid BuildGrid(ItemizadoViewModel vm)
    {
        var grid = new DataGrid { ItemsSource = vm.FilteredRows };
        grid.Columns.Add(new DataGridTextColumn { Header = "Ítem", Binding = new Binding(nameof(ItemizadoRowVm.Item)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Descripción", Binding = new Binding(nameof(ItemizadoRowVm.Descripcion)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Ud.", Binding = new Binding(nameof(ItemizadoRowVm.Unidad)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Cantidad", Binding = new Binding(nameof(ItemizadoRowVm.CantidadText)) });
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Excl. cubic.", Binding = new Binding(nameof(ItemizadoRowVm.ExcluidoCubicacion)) });
        return grid;
    }

    /// <summary>
    /// Debe llamarse dentro de RunOnSta: construye la VM en el mismo hilo que luego mutará su
    /// ObservableCollection (crecimiento de grilla), evitando el cross-thread guard de WPF.
    /// </summary>
    private static (WpfAppHost host, ItemizadoViewModel vm) Setup(string projectName, params ItemizadoRow[] rows)
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"{projectName}-{Guid.NewGuid():N}.qce");
        host.Session.NewProject(projectName);
        foreach (var row in rows)
            host.Session.Itemizado.Add(row);
        host.ProjectWorkspace.SaveAsAsync(path).GetAwaiter().GetResult();

        var vm = new ItemizadoViewModel(new FakeShell(), host);
        vm.Refresh();
        return (host, vm);
    }

    [Fact]
    public void TryPasteAnchored_PasteExceedingRows_GrowsGridInsteadOfTruncating()
    {
        RunOnSta(() =>
        {
            var (host, vm) = Setup("itemizado-paste-grow",
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Original", Unidad = "UN", Cantidad = 1m });

            var grid = BuildGrid(vm);
            var descripcionColumn = grid.Columns[1];
            grid.CurrentCell = new DataGridCellInfo(vm.Rows[0], descripcionColumn);

            // 3 líneas pegadas, pero la grilla sólo tiene 1 fila: debe crecer a 3, no truncar a 1.
            string pasted = "Canalización PVC\nCableado\nTablero";
            bool handled = vm.TryPasteAnchored(grid, pasted);

            Assert.True(handled);
            Assert.Equal(3, host.Session.Itemizado.Count);
            Assert.Contains(host.Session.Itemizado, r => r.Descripcion == "Canalización PVC");
            Assert.Contains(host.Session.Itemizado, r => r.Descripcion == "Cableado");
            Assert.Contains(host.Session.Itemizado, r => r.Descripcion == "Tablero");

            vm.Dispose();
        });
    }

    [Fact]
    public void TryPasteAnchored_BlankSeparatorLine_KeepsAlignmentInsteadOfShifting()
    {
        RunOnSta(() =>
        {
            var (host, vm) = Setup("itemizado-paste-blank",
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "FilaA", Unidad = "UN", Cantidad = 1m },
                new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "FilaB-sin-tocar", Unidad = "UN", Cantidad = 1m },
                new ItemizadoRow { Id = "P3", Item = "1.3", Descripcion = "FilaC", Unidad = "UN", Cantidad = 1m });

            var grid = BuildGrid(vm);
            var descripcionColumn = grid.Columns[1];
            grid.CurrentCell = new DataGridCellInfo(vm.Rows[0], descripcionColumn);

            // Línea vacía en medio (separador de sección en el Excel de origen).
            string pasted = "Nueva-A\n\nNueva-C";
            bool handled = vm.TryPasteAnchored(grid, pasted);

            Assert.True(handled);
            Assert.Equal("Nueva-A", vm.Rows[0].Descripcion);
            Assert.Equal("FilaB-sin-tocar", vm.Rows[1].Descripcion); // no se tocó ni se corrió
            Assert.Equal("Nueva-C", vm.Rows[2].Descripcion); // sigue alineada con su fila original

            vm.Dispose();
        });
    }

    [Fact]
    public void TryPasteAnchored_ExtraColumns_WarnsInsteadOfSilentlyDropping()
    {
        RunOnSta(() =>
        {
            var (host, vm) = Setup("itemizado-paste-overflow",
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "A", Unidad = "UN", Cantidad = 1m },
                new ItemizadoRow { Id = "P2", Item = "1.2", Descripcion = "B", Unidad = "UN", Cantidad = 1m });

            var grid = BuildGrid(vm);
            var itemColumn = grid.Columns[0]; // startColumn = 0 → sólo caben 5 columnas (0..4)
            grid.CurrentCell = new DataGridCellInfo(vm.Rows[0], itemColumn);

            // 6 columnas pegadas por fila: Item, Descripcion, Unidad, Cantidad, ExclCubic, EXTRA.
            string pasted = "1.1\tCanalización\tML\t50\tFALSE\tEXTRA1\n1.2\tCableado\tML\t30\tFALSE\tEXTRA2";
            bool handled = vm.TryPasteAnchored(grid, pasted);

            Assert.True(handled);
            Assert.Contains("columna(s) pegada(s) fuera de rango", vm.StatusMessage);

            vm.Dispose();
        });
    }

    [Fact]
    public void TryPasteAnchored_SingleCellNoTabs_AppliesDirectlyWithoutGrowth()
    {
        RunOnSta(() =>
        {
            var (host, vm) = Setup("itemizado-paste-single",
                new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Original", Unidad = "UN", Cantidad = 1m });

            var grid = BuildGrid(vm);
            var descripcionColumn = grid.Columns[1];
            grid.CurrentCell = new DataGridCellInfo(vm.Rows[0], descripcionColumn);

            bool handled = vm.TryPasteAnchored(grid, "Reemplazada");

            Assert.True(handled);
            Assert.Equal(1, host.Session.Itemizado.Count);
            Assert.Equal("Reemplazada", vm.Rows[0].Descripcion);

            vm.Dispose();
        });
    }
}
