using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical.Wpf.Features.Costo;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

/// <summary>
/// Carga el XAML real de CostoView (rediseño 2.0: hero de 6 KPIs + 5 pestañas) para atrapar
/// errores de markup, de recursos y de binding que los tests de VM no ven. El rediseño
/// reemplazó la pantalla unificada por una barra segmentada; aquí se fija el contrato visible:
/// orden de pestañas, orden de las cuatro sub-familias del costo directo y ausencia de errores
/// de binding al materializar cada pestaña.
///
/// El hilo STA y la Application con el tema los aporta <see cref="XamlViewSmokeHost"/>.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CostoViewXamlSmokeTests
{
    private const string TabComposicion = "Composición";
    private const string TabCierre = "Márgenes y cierre";
    private const string TabFamilias = "Familias";
    private const string TabMo = "M.O. y viáticos";
    private const string TabGo = "Gastos operacionales";

    private sealed class FakeShell : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
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

    /// <summary>Recolecta los errores de binding que WPF escribe en DataBindingSource.</summary>
    private sealed class BindingErrorListener : TraceListener
    {
        private readonly List<string> destino;
        public BindingErrorListener(List<string> destino) => this.destino = destino;
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                destino.Add(message!);
        }
    }

    public CostoViewXamlSmokeTests() => WpfTestHost.EnsureInitialized();

    private static CostoViewModel NuevoVm()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject($"costo-smoke-{Guid.NewGuid():N}");
        // HasOpenProject se deriva de CurrentProjectPath: sin esto la pantalla queda en estado
        // vacío y no se construyen los KPIs ni las sub-familias del costo directo.
        host.Session.CurrentProjectPath = Path.Combine(Path.GetTempPath(), $"costo-smoke-{Guid.NewGuid():N}.qce");
        var vm = new CostoViewModel(new FakeShell(), host);
        vm.Refresh();
        return vm;
    }

    [Fact]
    public void CostoView_InitializeComponent_NoLanzaYConservaCoefFactorsList()
    {
        XamlViewSmokeHost.Run(() =>
        {
            try
            {
                var view = new CostoView { DataContext = NuevoVm() };
                XamlViewSmokeHost.Pump();
                Assert.NotNull(view.FindName("CoefFactorsList"));
            }
            finally
            {
                XamlViewSmokeHost.ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }

    /// <summary>El orden de pestañas y de sub-familias es contrato con el diseño aprobado.</summary>
    [Fact]
    public void CostoView_PestanasYSubFamilias_RespetanElOrdenAprobado()
    {
        var vm = NuevoVm();

        Assert.Equal(
            string.Join(" | ", TabComposicion, TabCierre, TabFamilias, TabMo, TabGo),
            string.Join(" | ", vm.Tabs.Items.Select(t => t.Label)));

        Assert.True(vm.Tabs.IsComposicion, "La pestaña inicial debe ser Composición");

        Assert.Equal(
            "Materiales | Mano de obra | Equipos | Complementarios",
            string.Join(" | ", vm.Tabs.SubFamilies.Select(g => g.Titulo)));
    }

    [Fact]
    public void CostoView_FinancialRatio_SegmentationDirectosIndirectosMargen_HasValidDataAndRatio()
    {
        var vm = NuevoVm();
        Assert.NotNull(vm.FinancialRatioDirectosPct);
        Assert.NotNull(vm.FinancialRatioIndirectosPct);
        Assert.NotNull(vm.FinancialRatioMargenPct);
        Assert.NotNull(vm.FinancialRatioDirectosMonto);
        Assert.NotNull(vm.FinancialRatioIndirectosMonto);
        Assert.NotNull(vm.FinancialRatioMargenMonto);
        Assert.True(vm.FinancialRatioDirectosStar.Value > 0);
        Assert.True(vm.FinancialRatioIndirectosStar.Value > 0);
        Assert.True(vm.FinancialRatioMargenStar.Value > 0);
    }

    /// <summary>
    /// Materializa las cinco pestañas con el XAML real. Cualquier ruta de binding inexistente
    /// (p. ej. una propiedad renombrada en la VM) queda registrada en DataBindingSource y falla.
    /// </summary>
    [Fact]
    public void CostoView_CadaPestanaSeRenderizaSinErroresDeBinding()
    {
        XamlViewSmokeHost.Run(() =>
        {
            var errores = new List<string>();
            var listener = new BindingErrorListener(errores);
            var source = PresentationTraceSources.DataBindingSource;
            PresentationTraceSources.Refresh();
            source.Switch.Level = SourceLevels.Error;
            source.Listeners.Add(listener);
            try
            {
                // Autochequeo: sin esto el test podría pasar sin haber capturado nada (un listener
                // mal configurado daría un falso verde). Se provoca un error de binding conocido.
                var sonda = new System.Windows.Controls.TextBlock { DataContext = new object() };
                System.Windows.Data.BindingOperations.SetBinding(
                    sonda,
                    System.Windows.Controls.TextBlock.TextProperty,
                    new System.Windows.Data.Binding("RutaInexistenteDeSonda"));
                XamlViewSmokeHost.Pump();
                Assert.NotEmpty(errores);
                errores.Clear();

                var vm = NuevoVm();
                var view = new CostoView { DataContext = vm };
                XamlViewSmokeHost.Pump();
                Assert.Equal(5, vm.Tabs.Items.Count);
                Assert.Equal(4, vm.Tabs.SubFamilies.Count);

                for (var i = 0; i < vm.Tabs.Items.Count; i++)
                {
                    vm.Tabs.Items[i].IsSelected = true;
                    XamlViewSmokeHost.Pump();
                    view.Measure(new Size(1600, 1000));
                    view.Arrange(new Rect(0, 0, 1600, 1000));
                    view.UpdateLayout();
                    XamlViewSmokeHost.Pump();
                }

                Assert.True(errores.Count == 0,
                    "Errores de binding en CostoView:\n" + string.Join("\n", errores));
            }
            finally
            {
                source.Listeners.Remove(listener);
                XamlViewSmokeHost.ClearApplicationCurrent();
                Assert.Null(System.Windows.Application.Current);
            }
        });
    }
}
