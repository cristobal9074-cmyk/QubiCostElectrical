using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Features.Templates;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Carga el XAML real de PlantillasView (InitializeComponent) para atrapar errores de markup
/// que los tests de VM no ven. El 2026-10-07 la app crasheó en producción con
/// "Center is not a valid value for Dock" (DockPanel.Dock="Center" no existe) al abrir la
/// pestaña Plantillas: tres eventos Critical y salida de la app.
///
/// Los recursos estáticos (Form.TabControl, DataGrid.Workspace, BoolToVisibility, ...) sólo
/// resuelven si existe una Application con Theme.xaml fusionado. La Application y la vista
/// viven en el MISMO hilo STA compartido (afinidad de Dispatcher para estilos y elementos),
/// serializado por la colección WpfHostCollection.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PlantillasViewXamlSmokeTests : IDisposable
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
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

    private readonly IDisposable templateDbScope;
    private readonly string templateDbPath;
    private readonly IDisposable conjuntoDbScope;
    private readonly string conjuntoDbPath;

    public PlantillasViewXamlSmokeTests()
    {
        WpfTestHost.EnsureInitialized();
        templateDbPath = Path.Combine(Path.GetTempPath(), $"apu-templates-smoke-{Guid.NewGuid():N}.db");
        templateDbScope = ApuTemplateStore.UseDatabasePathForTests(templateDbPath);
        conjuntoDbPath = Path.Combine(Path.GetTempPath(), $"conjuntos-smoke-{Guid.NewGuid():N}.db");
        conjuntoDbScope = ConjuntoTemplateStore.UseDatabasePathForTests(conjuntoDbPath);
    }

    public void Dispose()
    {
        templateDbScope.Dispose();
        conjuntoDbScope.Dispose();
        foreach (var path in new[] { templateDbPath, templateDbPath + "-wal", templateDbPath + "-shm",
                                     conjuntoDbPath, conjuntoDbPath + "-wal", conjuntoDbPath + "-shm" })
            try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    /// <summary>
    /// Ejecuta la acción en el hilo STA compartido que hospeda la Application con el tema de la
    /// app. El host es compartido (XamlViewSmokeHost): WPF sólo admite una Application por
    /// AppDomain y crear una propia aborta el host de pruebas de toda la suite.
    /// </summary>
    private static void RunOnUiSta(Action action) => XamlViewSmokeHost.Run(action);

    /// <summary>
    /// La suite no corre Dispatcher.Run, así que la cola del hilo UI no se drena sola: las
    /// transferencias de binding diferidas (p. ej. al cambiar DataContext tras adjuntar la
    /// expresión, o el push TwoWay de SelectedValue) quedarían pendientes para siempre y
    /// ItemsSource permanecería nulo. Este frame procesa todo lo encolado hasta ApplicationIdle.
    /// </summary>
    private static void PompearBindingQueue() => XamlViewSmokeHost.Pump();

    /// <summary>
    /// Application.Current es global: si queda no-nulo después del smoke, el resto de la suite
    /// ve recursos de tema/diálogos reales donde antes veía null, y los setters que usan
    /// Application.Current?.Dispatcher encolan en un dispatcher sin pump (refrescos que nunca
    /// corren y tests que cuelgan). Se limpia el campo interno para restaurar el estado previo.
    /// </summary>
    private static void ClearApplicationCurrent() => XamlViewSmokeHost.ClearApplicationCurrent();

    [Fact]
    public void PlantillasView_InitializeComponent_NoLanzaXamlParseException()
    {
        RunOnUiSta(() =>
        {
            try
            {
                var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
                var view = new PlantillasView(vm);

                Assert.NotNull(view);
                Assert.Same(vm, view.DataContext);
            }
            finally
            {
                ClearApplicationCurrent();
                // Guardia: si la limpieza falla, la suite entera se contamina de forma opaca
                // (fallos intermitentes en PurchaseList/Quotes). Mejor fallar aquí.
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }

    /// <summary>
    /// Rediseño (docs/propuesta-plantillas.html): selección múltiple Extendida para la barra
    /// contextual ámbar y dos pestañas (Conjuntos eléctricos / Plantillas APU). Estos nombres
    /// son contrato con el code-behind (SelectionChanged → SetSelectedRecursos).
    /// </summary>
    [Fact]
    public void PlantillasView_ElementosClaveDelRediseño_Existen()
    {
        RunOnUiSta(() =>
        {
            try
            {
                var vm = new PlantillasVm(new FakeShellHost(), WpfAppHost.Current);
                var view = new PlantillasView(vm);
                PompearBindingQueue(); // los bindings XAML (Command, visibilidad) se resuelven al tener DataContext

                var grid = view.FindName("ResourcesGrid") as DataGrid;
                Assert.NotNull(grid);
                Assert.Equal(DataGridSelectionMode.Extended, grid!.SelectionMode);
                Assert.Equal(DataGridSelectionUnit.FullRow, grid.SelectionUnit);
                Assert.True(grid.Columns.Count >= 6, $"columnas: {grid.Columns.Count}");

                // Columna «Valor»: edición nativa con LostFocus — mientras se teclea "1,5"
                // no se reformatea (N2 en cada tecla rompía el cursor y el parseo con coma).
                var valor = grid.Columns.OfType<DataGridBoundColumn>()
                    .FirstOrDefault(c => c.Binding is System.Windows.Data.Binding b && b.Path.Path == "PasoFactor");
                Assert.NotNull(valor);
                var valorBinding = (System.Windows.Data.Binding)valor!.Binding!;
                Assert.Equal(System.Windows.Data.UpdateSourceTrigger.LostFocus, valorBinding.UpdateSourceTrigger);

                // Nav Excel activado en el ctor DESPUÉS del guard de teclado (Backspace/Delete
                // ya no vacía la descripción de la fila al cambiar un valor).
                Assert.True(DataGridExcelNavigation.GetEnable(grid));

                // Guardado global: botón «Guardar» en acciones globales + indicador «● sin guardar».
                var btnGuardar = view.FindName("BtnGuardarPlantillas") as Button;
                Assert.NotNull(btnGuardar);
                Assert.NotNull(btnGuardar!.Command);
                var txtSinGuardar = view.FindName("TxtSinGuardar") as TextBlock;
                Assert.NotNull(txtSinGuardar);
                Assert.Contains("sin guardar", txtSinGuardar!.Text);

                Assert.NotNull(view.FindName("CtxBar"));

                var tabs = view.FindName("PlantillasTabs") as TabControl;
                Assert.NotNull(tabs);
                Assert.Equal(2, tabs!.Items.Count);
            }
            finally
            {
                ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }

    /// <summary>
    /// Conjunto de prueba con 2 maestros (m1, m2) y 2 filas libres (s1, s2) para los tests
    /// de «Dividir de». Se crea dentro del hilo STA (misma afinidad que la vista).
    /// </summary>
    private static (PlantillasVm Vm, List<ConjuntoRecursoVm> Filas) CrearVmConFilas()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject($"smoke-dividir-{Guid.NewGuid():N}");
        var conjunto = new ConjuntoElectricoRow
        {
            Codigo = "CE-SM",
            Nombre = "Smoke dividir",
            Recursos = new List<ConjuntoRecursoRow>
            {
                new() { Id = "m1", Codigo = "MAT1", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "m2", Codigo = "MAT2", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "s1", Codigo = "MAT3", Tipo = ApuTipoRecurso.MAT },
                new() { Id = "s2", Codigo = "MAT4", Tipo = ApuTipoRecurso.MAT }
            }
        };
        host.Session.ConjuntosElectricos.Add(conjunto);

        var vm = new PlantillasVm(new FakeShellHost(), host);
        vm.SelectedConjunto = vm.Conjuntos.First(c => c.Source.Id == conjunto.Id);
        return (vm, vm.ConjuntoRecursos.ToList());
    }

    /// <summary>
    /// Réplica del binding real de la columna «Dividir de»: ItemsSource = MaestrosDisponibles,
    /// SelectedValue = MaestroId (TwoWay + PropertyChanged), SelectedValuePath = Id.
    /// </summary>
    private static ComboBox CrearComboDividirDe()
    {
        var combo = new ComboBox
        {
            SelectedValuePath = "Id",
            DisplayMemberPath = "Label"
        };
        combo.SetBinding(ComboBox.ItemsSourceProperty,
            new System.Windows.Data.Binding(nameof(ConjuntoRecursoVm.MaestrosDisponibles)));
        combo.SetBinding(ComboBox.SelectedValueProperty,
            new System.Windows.Data.Binding(nameof(ConjuntoRecursoVm.MaestroId))
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
        return combo;
    }

    /// <summary>
    /// Bug reportado: "en el Dividir de no se puede colocar más de un maestro; pongo un maestro
    /// en un material, luego en otro material pongo otro maestro y no carga, se elimina el otro".
    /// Elegir un maestro en una fila y otro distinto en otra NO debe borrar el primer vínculo;
    /// tampoco cambiar de maestro en la misma celda.
    /// </summary>
    [Fact]
    public void ComboDividirDe_DosFilasDosMaestros_ElPrimerVinculoNoSeBorra()
    {
        RunOnUiSta(() =>
        {
            try
            {
                var (vm, filas) = CrearVmConFilas();

                var combo1 = CrearComboDividirDe();
                combo1.DataContext = filas[2]; // s1
                PompearBindingQueue();
                var combo2 = CrearComboDividirDe();
                combo2.DataContext = filas[3]; // s2
                PompearBindingQueue();

                Assert.NotNull(combo1.ItemsSource);
                Assert.Equal(3, ((System.Collections.ICollection)combo1.ItemsSource!).Count); // m1, m2, s2

                combo1.SelectedValue = "m1";
                PompearBindingQueue();
                Assert.Equal("m1", filas[2].MaestroId);      // push a la VM
                Assert.Equal("m1", combo1.SelectedValue);    // y el objetivo lo conserva

                combo2.SelectedValue = "m2";
                PompearBindingQueue();
                Assert.Equal("m2", filas[3].MaestroId);
                Assert.Equal("m1", filas[2].MaestroId);   // el primer vínculo sigue intacto
                Assert.Equal("m1", combo1.SelectedValue); // y la primera celda lo sigue mostrando

                // Cambiar de maestro en la MISMA celda
                combo2.SelectedValue = "m1";
                PompearBindingQueue();
                Assert.Equal("m1", filas[3].MaestroId);
                Assert.Equal("m1", filas[2].MaestroId);
            }
            finally
            {
                ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }

    /// <summary>
    /// Barra contextual: elegir un maestro en «Esclavo de:» aplica de inmediato a las filas
    /// seleccionadas (AsignarMaestroSeleccionCommand estaba cableado en la VM pero la XAML no
    /// lo ejecutaba en ningún lado — "no carga").
    /// </summary>
    [Fact]
    public void CtxBar_EsclavoDe_AplicaElMaestroALasFilasSeleccionadas()
    {
        RunOnUiSta(() =>
        {
            try
            {
                var (vm, filas) = CrearVmConFilas();
                var view = new PlantillasView(vm);
                PompearBindingQueue(); // los bindings XAML se adjuntan con DataContext aún nulo
                vm.SetSelectedRecursos(new[] { filas[2], filas[3] });
                PompearBindingQueue();

                var combo = view.FindName("EsclavoDeCombo") as ComboBox;
                Assert.NotNull(combo);
                Assert.NotNull(combo!.ItemsSource);
                Assert.NotEmpty(vm.MaestrosParaSeleccion);

                var opcion = vm.MaestrosParaSeleccion.First(o => o.Id == "m1");
                combo.SelectedItem = opcion;
                PompearBindingQueue();

                Assert.Equal("m1", filas[2].MaestroId);
                Assert.Equal("m1", filas[3].MaestroId);
                Assert.Null(combo.SelectedItem); // se limpia: permite repetir el mismo maestro
            }
            finally
            {
                ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }

    /// <summary>
    /// Réplica del binding real de la columna «Regla» (DataGridTemplateColumn):
    /// ItemsSource = array estático compartido PlantillasView.TiposCalculo,
    /// SelectedItem = TipoCalculo (TwoWay por defecto).
    /// </summary>
    private static ComboBox CrearComboRegla()
    {
        var combo = new ComboBox();
        combo.SetBinding(ComboBox.ItemsSourceProperty,
            new System.Windows.Data.Binding { Source = PlantillasView.TiposCalculo });
        combo.SetBinding(ComboBox.SelectedItemProperty,
            new System.Windows.Data.Binding(nameof(ConjuntoRecursoVm.TipoCalculo)));
        return combo;
    }

    /// <summary>
    /// Bug reportado: "al cambiar esto a espaciemiento no se guarda, se queda en factor
    /// al cambiar de pestaña o de plantilla". El combo de Regla debe escribir TipoCalculo
    /// en la fila (y marcar la pantalla como pendiente), y al reutilizarse el combo en
    /// otra fila (reciclaje de contenedores de la grilla) su selección previa NO debe
    /// escribirse en la fila nueva.
    /// </summary>
    [Fact]
    public void ComboRegla_EscribeTipoCalculo_YAlReutilizarEnOtraFilaNoLoRevierte()
    {
        RunOnUiSta(() =>
        {
            try
            {
                var (vm, filas) = CrearVmConFilas();

                var combo = CrearComboRegla();
                combo.DataContext = filas[2];
                PompearBindingQueue();
                Assert.Equal(TipoCalculoParametrico.Factor, filas[2].TipoCalculo);

                combo.SelectedItem = TipoCalculoParametrico.Espaciamiento;
                PompearBindingQueue();

                // Push a la fila y marca «sin guardar» en la pantalla
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, filas[2].TipoCalculo);
                Assert.True(vm.HasPendingChanges);

                // El combo se reutiliza para otra fila (reciclaje de contenedores):
                // la selección previa no debe filtrarse hacia la fila nueva…
                combo.DataContext = filas[3];
                PompearBindingQueue();
                Assert.Equal(TipoCalculoParametrico.Factor, filas[3].TipoCalculo);

                // …y la fila original conserva el Espaciamiento (bug: volvía a Factor).
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, filas[2].TipoCalculo);
            }
            finally
            {
                ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject root)
        where T : System.Windows.DependencyObject
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deep in FindVisualChildren<T>(child)) yield return deep;
        }
    }

    /// <summary>
    /// Repro del bug en la GRILLA REAL: cambiar Regla a «Espaciamiento» debía llegar al
    /// source de la fila. El BindingGroup de la fila de la DataGrid difería el traslado
    /// hasta el commit de celda, de modo que si el usuario salía de la grilla (cambiar de
    /// plantilla / pestaña) el cambio se perdía y la fila seguía en Factor. El handler
    /// SelectionChanged de la grilla fuerza UpdateSource y el cambio sobrevive.
    /// </summary>
    [Fact]
    public void GrillaReal_CambiarRegla_YReconstruirFilas_NoRevientaAFactor()
    {
        RunOnUiSta(() =>
        {
            try
            {
                var (vm, filas) = CrearVmConFilas();
                var view = new PlantillasView(vm);
                PompearBindingQueue();

                var grid = view.FindName("ResourcesGrid") as DataGrid;
                Assert.NotNull(grid);

                // Materializar contenedores de fila (sin esto la DataGrid no genera visuales)
                view.Measure(new System.Windows.Size(1400, 900));
                view.Arrange(new System.Windows.Rect(0, 0, 1400, 900));
                PompearBindingQueue();

                var todos = FindVisualChildren<ComboBox>(grid!).ToList();
                static string PathDe(System.Windows.Controls.ComboBox c) =>
                    System.Windows.Data.BindingOperations.GetBindingExpression(
                        c, System.Windows.Controls.Primitives.Selector.SelectedItemProperty)
                        ?.ParentBinding.Path.Path ?? "none";
                var combo = todos
                    .FirstOrDefault(c => PathDe(c) == "TipoCalculo"
                                      && ReferenceEquals(c.DataContext, filas[2]));
                if (combo == null)
                {
                    var diag = string.Join(" | ", todos.Select(c =>
                        $"ds={c.DataContext?.GetType().Name ?? "null"}," +
                        $"is={c.ItemsSource?.GetType().Name ?? "null"}," +
                        $"path={PathDe(c)}"));
                    Assert.Fail($"combo Regla no encontrado. {diag}");
                }

                combo!.SelectedItem = TipoCalculoParametrico.Espaciamiento;
                PompearBindingQueue();

                var bexp = System.Windows.Data.BindingOperations.GetBindingExpression(
                    combo, System.Windows.Controls.Primitives.Selector.SelectedItemProperty);
                Assert.True(bexp != null, "SelectedItem no tiene BindingExpression");
                Assert.Equal("TipoCalculo", bexp!.ParentBinding.Path.Path);
                Assert.True(bexp.Status == System.Windows.Data.BindingStatus.Active,
                    $"BindingStatus = {bexp.Status} ({bexp.ValidationError?.ErrorContent})");
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, combo.SelectedItem);

                // El SelectionChanged de la grilla fuerza el traslado al source: sin ese
                // empujón el BindingGroup de la fila lo diferiría y el cambio se perdería
                // al salir de la grilla sin pasar por una celda editable.
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, filas[2].TipoCalculo);

                // Cambiar de plantilla y volver: ConjuntoRecursos.Clear()+Add recrea
                // los contenedores de la grilla
                vm.SelectedConjunto = null;
                PompearBindingQueue();
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, filas[2].TipoCalculo);
                vm.SelectedConjunto = vm.Conjuntos.First();
                PompearBindingQueue();
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, filas[2].TipoCalculo);
                view.Measure(new System.Windows.Size(1400, 900));
                view.Arrange(new System.Windows.Rect(0, 0, 1400, 900));
                PompearBindingQueue();

                var filaS1 = vm.ConjuntoRecursos.First(r => r.Source.Id == "s1");
                Assert.Equal(TipoCalculoParametrico.Espaciamiento, filaS1.TipoCalculo);
                Assert.True(vm.HasPendingChanges);
            }
            finally
            {
                ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }
}
