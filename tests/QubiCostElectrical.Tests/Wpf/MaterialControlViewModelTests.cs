using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.MaterialControl;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class MaterialControlViewModelTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            LastContext = null;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastContext = context;
            return true;
        }

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

    public MaterialControlViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Refresh_WithoutProject_ShowsEmptyState()
    {
        var vm = new MaterialControlViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.Rows);
        Assert.Equal(0m, vm.TotalCubicado);
        Assert.Equal(0m, vm.TotalSolicitado);
        Assert.Equal(0, vm.CantidadSobrepedidos);
    }

    [Fact]
    public async Task Refresh_WithProjectAndSolicitudes_PopulatesRowsAndFlagsSobrepedidos()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"mat-control-test-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("mat-control-test");
        host.Session.ProjectInfo.Nombre = "Proyecto Control";

        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "PVC20",
            Descripcion = "TUBERIA PVC 20 MM",
            Unidad = "MTS",
            Cantidad = 100m,
            MatUf = 0.35m
        });

        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "PVC20",
            Descripcion = "TUBERIA PVC 20 MM",
            Unidad = "MTS",
            Categoria = "Canalizaciones",
            MatUf = 0.35m
        });

        host.Session.Solicitudes.Add(new SolicitudItemRow
        {
            Tipo = "Material",
            Codigo = "PVC20",
            Descripcion = "TUBERIA PVC 20 MM",
            Unidad = "MTS",
            Cantidad = 120m, // Excede 100 + 5% tol (105m)
            SolicitudCodigo = "SM 1",
            SolicitadoPor = "Juan Perez"
        });

        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new MaterialControlViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.False(vm.ShowEmptyState);
        Assert.Single(vm.Rows);
        var row = vm.Rows[0];
        Assert.Equal("PVC20", row.Codigo);
        Assert.True(row.TieneSobrepedido);
        Assert.Equal(1, vm.CantidadSobrepedidos);
        Assert.True(vm.TieneAlertas);
        Assert.NotNull(vm.SelectedRow);
        Assert.Single(vm.SelectedSolicitudes);
    }

    [Fact]
    public async Task FilterMode_And_SearchText_FilterRowsView()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"mat-control-filter-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("mat-control-filter");
        host.Session.ProjectInfo.Nombre = "Proyecto Filtro";

        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "PVC20",
            Descripcion = "TUBERIA PVC 20 MM",
            Unidad = "MTS",
            Cantidad = 100m
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P2",
            Codigo = "THHN10",
            Descripcion = "CONDUCTOR THHN 10",
            Unidad = "MTS",
            Cantidad = 200m
        });

        host.Session.Materiales.Add(new MaterialRow { Codigo = "PVC20", Descripcion = "TUBERIA PVC 20 MM" });
        host.Session.Materiales.Add(new MaterialRow { Codigo = "THHN10", Descripcion = "CONDUCTOR THHN 10" });

        // Solo PVC20 tiene sobrepedido
        host.Session.Solicitudes.Add(new SolicitudItemRow
        {
            Tipo = "Material",
            Codigo = "PVC20",
            Descripcion = "TUBERIA PVC 20 MM",
            Cantidad = 150m,
            SolicitudCodigo = "SM 1"
        });

        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new MaterialControlViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.Equal(2, vm.Rows.Count);

        // Filtrar solo alertas
        vm.SetFilterCommand.Execute("alert");
        var visibleAlerts = vm.FilteredRows.Cast<MaterialControlRowVm>().ToList();
        Assert.Single(visibleAlerts);
        Assert.Equal("PVC20", visibleAlerts[0].Codigo);

        // Filtrar pendientes
        vm.SetFilterCommand.Execute("pending");
        var visiblePending = vm.FilteredRows.Cast<MaterialControlRowVm>().ToList();
        Assert.Single(visiblePending);
        Assert.Equal("THHN10", visiblePending[0].Codigo);

        // Buscar texto
        vm.SetFilterCommand.Execute("all");
        vm.SearchText = "THHN";
        var visibleSearch = vm.FilteredRows.Cast<MaterialControlRowVm>().ToList();
        Assert.Single(visibleSearch);
        Assert.Equal("THHN10", visibleSearch[0].Codigo);
    }

    [Fact]
    public void MaterialControlView_ConsumoGreaterThanCubicado_TriggersAmberAlert()
    {
        var rowModel = new MaterialControlRow(
            Codigo: "CONDUIT25",
            Descripcion: "TUBERIA CONDUIT 25MM",
            Unidad: "M",
            Categoria: "Canalización",
            CantidadCubicada: 100m,
            CantidadSolicitada: 140m, // Consumo > Cubicado
            CantidadPendiente: 0m,
            CantidadExceso: 40m,
            PorcentajeAvance: 140.0,
            EstadoControl: "⚠️ Sobrepedido (+40)",
            TieneSobrepedido: true,
            Solicitudes: Array.Empty<MaterialSolicitudUso>());

        var rowVm = new MaterialControlRowVm(rowModel);

        Assert.True(rowVm.TieneSobreconsumo);
        Assert.True(rowVm.TieneSobrepedido);
        Assert.Equal(140.0, rowVm.PorcentajeAvance);

        // Verificación de conversores visuales ámbar
        var progressConverter = new PercentToProgressBrushConverter();
        var progressBrush = progressConverter.Convert(140.0, typeof(System.Windows.Media.Brush), null, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(progressBrush);
        Assert.Equal(0xD9, progressBrush.Color.R);
        Assert.Equal(0x77, progressBrush.Color.G);
        Assert.Equal(0x06, progressBrush.Color.B);

        var bgConverter = new StatusToBadgeBgConverter();
        var bgBrush = bgConverter.Convert(rowVm.EstadoControl, typeof(System.Windows.Media.Brush), null, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(bgBrush);
        Assert.Equal(0xFE, bgBrush.Color.R);
        Assert.Equal(0xF3, bgBrush.Color.G);
        Assert.Equal(0xC7, bgBrush.Color.B);

        var borderConverter = new StatusToBadgeBorderConverter();
        var borderBrush = borderConverter.Convert(rowVm.EstadoControl, typeof(System.Windows.Media.Brush), null, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(borderBrush);
        Assert.Equal(0xFC, borderBrush.Color.R);
        Assert.Equal(0xD3, borderBrush.Color.G);
        Assert.Equal(0x4D, borderBrush.Color.B);

        var fgConverter = new StatusToBadgeFgConverter();
        var fgBrush = fgConverter.Convert(rowVm.EstadoControl, typeof(System.Windows.Media.Brush), null, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
        Assert.NotNull(fgBrush);
        Assert.Equal(0x78, fgBrush.Color.R);
        Assert.Equal(0x35, fgBrush.Color.G);
        Assert.Equal(0x0F, fgBrush.Color.B);
    }
}
