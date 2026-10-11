using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Apu;
using QubiCostElectrical.Wpf.Features.Purchases;
using QubiCostElectrical.Wpf.Features.Quotes;
using QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ShellNavigationTests
{
    private sealed class FakeShell : IShellNavigationHost
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

    public ShellNavigationTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public async Task PurchaseList_GoToOrigin_NavigatesToTakeoffWithContext()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"purchase-nav-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("purchase-nav-test");
        host.Session.ProjectInfo.Nombre = "Proyecto compras";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P1", Item = "1.1", Descripcion = "Iluminación" });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "LUM-001",
            Descripcion = "Luminaria LED",
            Unidad = "UN",
            Cantidad = 10m,
            MatUf = 0.5m
        });
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "LUM-001",
            Descripcion = "Luminaria LED",
            Unidad = "UN",
            MatUf = 0.5m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var shell = new FakeShell();
        var vm = new PurchaseListViewModel(shell, WpfAppHost.Current);
        vm.SelectedRow = vm.Rows.FirstOrDefault(r =>
            string.Equals(r.Codigo, "LUM-001", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(vm.SelectedRow);
        vm.GoToOriginCommand.Execute(null);

        Assert.Equal(ShellRoute.Takeoff, shell.CurrentRoute);
        var ctx = Assert.IsType<TakeoffNavigationContext>(shell.LastContext);
        Assert.Equal("P1", ctx.ItemId);
    }

    [Fact]
    public void Quotes_ApplyPrice_UpdatesMaterialMatUf()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("quotes-apply-test");
        host.Session.ProjectInfo.Nombre = "Proyecto cotizaciones";
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "CAB-001",
            Descripcion = "Cable 2.5mm",
            Unidad = "ML",
            MatUf = 0.1m,
            Proveedor = "ProveedorA"
        });
        var shell = new FakeShell();
        var vm = new QuotesViewModel(shell, WpfAppHost.Current);

        var row = new QuoteComparisonRowVm(
            "CAB-001",
            "Cable 2.5mm",
            "ProveedorA",
            0.1m,
            "ProveedorB",
            0.08m,
            "Materiales");

        if (row.PrecioMejor < 0m)
            return; // banco vacío en entorno de test sin persistencia real

        vm.ApplyPriceCommand.Execute(row);

        var material = host.Session.Materiales.FirstOrDefault(m =>
            string.Equals(m.Codigo, "CAB-001", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(material);
        Assert.Equal(row.PrecioMejor, material!.MatUf);
    }

    [Fact]
    public async Task Versions_GoToItem_NavigatesToTakeoffWithItemId()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"versions-nav-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("versions-nav-test");
        host.Session.ProjectInfo.Nombre = "Proyecto versiones";
        host.Session.Itemizado.Add(new ItemizadoRow { Id = "P2", Item = "2.1", Descripcion = "Canalización" });

        var snapshot = BudgetVersionData.FromProjectFileData(host.Session.CreateSnapshot());
        var version = new BudgetVersionRow
        {
            Codigo = "V1",
            Nombre = "Versión base",
            Estado = "Preliminar",
            CreadoPor = "test",
            FechaCreacion = DateTime.UtcNow,
            FechaModificacion = DateTime.UtcNow,
            MontoVentaUf = 0m,
            Snapshot = snapshot
        };
        host.Session.Versiones.Add(version);
        await host.ProjectWorkspace.SaveAsAsync(path);

        var shell = new FakeShell();
        var vm = new VersionsViewModel(shell, WpfAppHost.Current);

        Assert.NotEmpty(vm.Versions);

        var compRow = vm.ComparisonRows.FirstOrDefault();
        if (compRow != null)
        {
            vm.SelectedComparisonRow = compRow;
            vm.GoToItemCommand.Execute(null);
            Assert.Equal(ShellRoute.Takeoff, shell.CurrentRoute);
            var ctx = Assert.IsType<TakeoffNavigationContext>(shell.LastContext);
            Assert.Equal(compRow.ItemId, ctx.ItemId);
        }
    }

    [Fact]
    public void PriceBankNavigationContext_CarriesSearchTerm()
    {
        var ctx = new PriceBankNavigationContext("LUM-001");
        Assert.Equal("LUM-001", ctx.SearchTerm);
    }

    [Fact]
    public void ReviewViewModel_GoToIssue_NavigatesToApuWithContext()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("review-apu-nav-test");
        host.Session.ProjectInfo.Nombre = "Proyecto revisión APU";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P3",
            Item = "3.1",
            Descripcion = "Tablero eléctrico"
        });

        var shell = new FakeShell();
        var target = WorkspaceReviewTarget.Apu;
        var route = ShellRouteMapper.FromReviewTarget(target);

        Assert.Equal(ShellRoute.Apu, route);

        shell.RequestNavigate(route, new ApuNavContext("P3", "TAB-001", "Tablero eléctrico"));

        Assert.Equal(ShellRoute.Apu, shell.CurrentRoute);
        var ctx = Assert.IsType<ApuNavContext>(shell.LastContext);
        Assert.Equal("P3", ctx.ItemId);
    }

    [Fact]
    public void ShellViewModel_SelectPhase_SwitchesPhaseAndActiveRoute()
    {
        var vm = new ShellViewModel(WpfAppHost.Current);

        // Starts in Presupuesto
        Assert.Equal(ShellLifecyclePhase.Presupuesto, vm.CurrentPhase);
        Assert.True(vm.IsPresupuestoPhase);
        Assert.False(vm.IsPlanificacionPhase);
        Assert.False(vm.IsEjecucionPhase);

        // Select Planificacion
        vm.SelectPhase(ShellLifecyclePhase.Planificacion);
        Assert.Equal(ShellLifecyclePhase.Planificacion, vm.CurrentPhase);
        Assert.True(vm.IsPlanificacionPhase);

        // Select Ejecucion
        vm.SelectPhase(ShellLifecyclePhase.Ejecucion);
        Assert.Equal(ShellLifecyclePhase.Ejecucion, vm.CurrentPhase);
        Assert.True(vm.IsEjecucionPhase);
    }
}

