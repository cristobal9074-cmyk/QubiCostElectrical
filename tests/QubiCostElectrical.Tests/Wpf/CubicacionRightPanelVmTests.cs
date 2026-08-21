using System;
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

/// <summary>
/// CubicacionRightPanelVm no tenía tests propios pese a alojar la regla de negocio de "precio
/// vencido" (ver ProjectInfoRowPrecioVencidoTests) y la lógica de alertas de la fila
/// seleccionada en la pantalla más grande y más usada del programa (Cubicación).
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class CubicacionRightPanelVmTests
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

    public CubicacionRightPanelVmTests() => WpfTestHost.EnsureInitialized();

    private static async Task<CubicacionPageVm> CreateVmWithPartidaAsync(WpfAppHost host, decimal cantidad = 10m)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cubicacion-rightpanel-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("cubicacion-rightpanel");
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "ML",
            Cantidad = cantidad
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new CubicacionPageVm(new FakeShell(), host);
        vm.SelectPartida(vm.Partidas.Single(p => p.Id == "P1"));
        return vm;
    }

    [Fact]
    public async Task LoadRow_Null_ShowsNoRowSelectedState()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);

        // SelectedRow arranca en null: se selecciona una fila real primero para que el paso a
        // null sea un cambio real (el setter no dispara LoadRow si el valor no cambia).
        vm.SelectedRow = vm.Rows[0];
        vm.SelectedRow = null;

        var panel = vm.RightPanel;
        Assert.False(panel.HasCurrentRow);
        Assert.Equal("Sin recurso seleccionado", panel.CurrentRowTitle);
        Assert.Equal("—", panel.CurrentRowTotals);
        Assert.False(panel.HasAlertas);
        Assert.Equal(15, panel.ZoneDistribution.Count);
        Assert.False(panel.HasZonasConfiguradas);
    }

    [Fact]
    public async Task LoadRow_EmptyRow_ShowsPlaceholderTitleAndAllAlerts()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);

        vm.SelectedRow = vm.Rows[0];

        var panel = vm.RightPanel;
        Assert.True(panel.HasCurrentRow);
        Assert.Equal("Fila nueva — sin código ni descripción", panel.CurrentRowTitle);
        Assert.True(panel.HasAlertas);
        Assert.Contains("Sin código de material asignado.", panel.Alertas);
        Assert.Contains("Cantidad es cero.", panel.Alertas);
        Assert.Contains("Precio unitario es cero.", panel.Alertas);
    }

    [Fact]
    public async Task LoadRow_RowWithCodigoNotInCatalog_FlagsCodigoNoEncontrado()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        var row = vm.Rows[0];
        row.Codigo = "NO-EXISTE";
        row.Cantidad = 1m;
        row.MatUf = 1m;

        vm.SelectedRow = row;

        Assert.Contains("Código no encontrado en el catálogo de materiales.", vm.RightPanel.Alertas);
    }

    [Fact]
    public async Task LoadRow_HealthyRowWithMatchingMaterial_HasNoAlerts()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        host.Session.Materiales.Add(new MaterialRow { Codigo = "MAT-1", MatUf = 1m, FechaPrecio = DateTime.Today });

        var row = vm.Rows[0];
        row.Codigo = "MAT-1";
        row.Cantidad = 5m;
        row.MatUf = 1m;

        vm.SelectedRow = row;

        Assert.False(vm.RightPanel.HasAlertas);
        Assert.Empty(vm.RightPanel.Alertas);
    }

    [Fact]
    public async Task LoadRow_MaterialWithStalePrice_FlagsPrecioVencido_UsingProjectThreshold()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        host.Session.ProjectInfo.PriceExpiryCriticalDays = 30;
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "MAT-1",
            MatUf = 1m,
            FechaPrecio = DateTime.Today.AddDays(-61) // vencido con umbral 30
        });

        var row = vm.Rows[0];
        row.Codigo = "MAT-1";
        row.Cantidad = 5m;
        row.MatUf = 1m;

        vm.SelectedRow = row;

        Assert.True(vm.RightPanel.PrecioVencido);
        Assert.Contains(vm.RightPanel.Alertas, a => a.Contains("antigüedad"));
    }

    [Fact]
    public async Task LoadRow_MaterialWithinThreshold_DoesNotFlagPrecioVencido()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        host.Session.ProjectInfo.PriceExpiryCriticalDays = 90;
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "MAT-1",
            MatUf = 1m,
            FechaPrecio = DateTime.Today.AddDays(-61) // no vencido con umbral 90
        });

        var row = vm.Rows[0];
        row.Codigo = "MAT-1";
        row.Cantidad = 5m;
        row.MatUf = 1m;

        vm.SelectedRow = row;

        Assert.False(vm.RightPanel.PrecioVencido);
    }

    [Theory]
    [InlineData(-1, "(hereda proyecto)")]
    [InlineData(0, "0,00%")]
    [InlineData(5.5, "5,50%")]
    public async Task MermaPctOverrideDisplay_FormatsSentinelAndPercentages(double value, string expected)
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        vm.SelectedRow = vm.Rows[0];

        vm.RightPanel.MermaPctOverride = (decimal)value;

        Assert.Equal(expected, vm.RightPanel.MermaPctOverrideDisplay);
    }

    [Fact]
    public async Task Zona_Setter_WritesThroughToSelectedRow()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        var row = vm.Rows[0];
        vm.SelectedRow = row;

        vm.RightPanel.Zona = "Piso 3";

        Assert.Equal("Piso 3", row.Area);
        Assert.Equal("Piso 3", row.Source.Area);
    }

    [Fact]
    public async Task LoadRow_WithoutZonasConfiguradas_ShowsFirst15EmptyZones()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);

        vm.SelectedRow = vm.Rows[0];

        var panel = vm.RightPanel;
        Assert.False(panel.HasZonasConfiguradas);
        Assert.Equal(15, panel.ZoneDistribution.Count);
    }

    [Fact]
    public async Task LoadRow_WithZonasConfiguradas_ShowsActiveZoneNames()
    {
        var host = WpfAppHost.Current;
        var vm = await CreateVmWithPartidaAsync(host);
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Nombre = "Piso 1", Activa = true, Orden = 0 });
        host.Session.ZonasProyecto.Add(new ZonaProyecto { Nombre = "Piso 2", Activa = true, Orden = 1 });

        vm.SelectedRow = vm.Rows[0];
        // Reforzar la carga con las zonas ya presentes (SelectedRow ya disparó LoadRow, pero
        // las zonas se agregaron antes así que ya deberían estar reflejadas).
        vm.RightPanel.LoadRow(vm.Rows[0]);

        var panel = vm.RightPanel;
        Assert.True(panel.HasZonasConfiguradas);
        Assert.Equal("Piso 1", panel.ZoneDistribution[0].ZoneDisplayName);
        Assert.Equal("Piso 2", panel.ZoneDistribution[1].ZoneDisplayName);
    }
}
