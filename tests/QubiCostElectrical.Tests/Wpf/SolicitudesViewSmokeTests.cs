using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Solicitudes;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class SolicitudesViewSmokeTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Solicitudes;
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

    public SolicitudesViewSmokeTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    private static void RunOnUiSta(Action action) => XamlViewSmokeHost.Run(action);
    private static void PompearBindingQueue() => XamlViewSmokeHost.Pump();
    private static void ClearApplicationCurrent() => XamlViewSmokeHost.ClearApplicationCurrent();

    [Fact]
    public void SolicitudesView_CellSelectionAndKeyboardWorkflow_RetainsFocusAndIndicator()
    {
        RunOnUiSta(() =>
        {
            try
            {
                QubiCostElectrical.Wpf.Controls.AppMessageDialog.DialogHandlerForTesting = (_, _) => QubiCostElectrical.Wpf.Controls.AppMessageDialogResult.Primary;
                var host = WpfAppHost.Current;
                var session = host.Session;

                var path = Path.Combine(Path.GetTempPath(), $"sol-test-{Guid.NewGuid():N}.qce");
                session.NewProject("Proyecto Test Solicitudes");
                session.Solicitudes.Clear();
                session.Solicitudes.Add(new SolicitudItemRow
                {
                    SolicitudCodigo = "SM 1",
                    Tipo = "Material",
                    Codigo = "CABLE-01",
                    Descripcion = "Cable THHN 2.5 mm2",
                    Unidad = "M",
                    Cantidad = 100
                });
                session.Solicitudes.Add(new SolicitudItemRow
                {
                    SolicitudCodigo = "SM 1",
                    Tipo = "Material",
                    Codigo = "TUBO-01",
                    Descripcion = "Tubo Conduit 20mm",
                    Unidad = "TIRA",
                    Cantidad = 50
                });
                host.ProjectWorkspace.SaveAsAsync(path).GetAwaiter().GetResult();

                var vm = new SolicitudesViewModel(new FakeShellHost(), host);
                var view = new SolicitudesView { DataContext = vm };

                var win = new Window
                {
                    Content = view,
                    Width = 1000,
                    Height = 700
                };
                win.Show();
                PompearBindingQueue();

                var grid = view.MaterialesGrid;
                Assert.NotNull(grid);
                Assert.Equal(2, grid.Items.Count);

                // 1. Verificar foco inicial directo
                view.FocusCellDirectly(0, view.CodigoColumn);
                PompearBindingQueue();

                Assert.True(grid.SelectedCells.Count > 0, "Debe haber al menos una celda seleccionada");
                var currentCell = grid.CurrentCell;
                Assert.True(currentCell.IsValid, "CurrentCell debe ser válida");
                Assert.Equal(view.CodigoColumn, currentCell.Column);

                // 2. Simular escribir en la celda Código
                bool began = DataGridExcelNavigation.TryBeginEditFromChar(grid, "C");
                PompearBindingQueue();
                Assert.True(began, "TryBeginEditFromChar debe iniciar edición");
                Assert.True(grid.IsKeyboardFocusWithin, "Al escribir, el foco debe estar dentro del editor");

                // 3. Confirmar con Enter y bajar
                // Disparamos Key.Enter sobre el editor
                var editor = Keyboard.FocusedElement as TextBox;
                Assert.NotNull(editor);
                editor.Text = "CABLE-TEST";

                // Simular PreviewKeyDown con Enter sobre el editor
                var enterArgs = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor)!, 0, Key.Enter)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                editor.RaiseEvent(enterArgs);
                PompearBindingQueue();

                // Debe haber bajado a la fila 1, columna Código
                Assert.Equal(1, grid.Items.IndexOf(grid.CurrentCell.Item));
                Assert.Equal(view.CodigoColumn, grid.CurrentCell.Column);
                Assert.True(grid.IsKeyboardFocusWithin, "Tras Enter, el foco debe seguir dentro de la grilla");
                Assert.True(grid.SelectedCells.Count > 0, "Debe haber celda seleccionada tras Enter");
                Assert.Equal(grid.CurrentCell.Item, grid.SelectedCells[0].Item);
                Assert.Equal(view.CodigoColumn, grid.SelectedCells[0].Column);

                // 4. Probar Delete / Supr sobre la fila seleccionada
                var deleteArgs = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(grid)!, 0, Key.Delete)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                };
                grid.RaiseEvent(deleteArgs);
                PompearBindingQueue();

                // Debe quedar 1 fila y la celda seleccionada debe seguir activa
                Assert.Equal(1, grid.Items.Count);
                Assert.True(grid.SelectedCells.Count > 0, "Tras Supr, la celda restante debe seguir seleccionada");
                Assert.True(grid.CurrentCell.IsValid, "CurrentCell debe ser válida tras Supr");
                Assert.Equal(0, grid.Items.IndexOf(grid.CurrentCell.Item));

                win.Close();
            }
            finally
            {
                QubiCostElectrical.Wpf.Controls.AppMessageDialog.DialogHandlerForTesting = null;
                ClearApplicationCurrent();
            }
        });
    }
}
