using System.Linq;
using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Purchases;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PurchaseListViewModelTests
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

    public PurchaseListViewModelTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Refresh_WithoutProject_ShowsEmptyStateAndDisablesExport()
    {
        var vm = new PurchaseListViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.True(vm.ShowEmptyState);
        Assert.Empty(vm.Rows);
        Assert.Equal("Sin proyecto abierto", vm.SummaryText);
        Assert.Equal("Sin proyecto abierto.", vm.ProjectSummary);
        Assert.Contains("Abre o crea un proyecto", vm.StatusMessage);
        Assert.False(vm.ExportExcelCommand.CanExecute(null));
        Assert.Equal("Todas", vm.CategoryOptions[0]);
    }

    [Fact]
    public async Task Refresh_WithProject_BuildsConsolidatedRowsAndCategories()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"purchase-list-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("purchase-list-test");
        host.Session.ProjectInfo.Nombre = "Proyecto compra";
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "PVC20",
            Descripcion = "PVC 20 mm",
            Unidad = "UN",
            Cantidad = 10m,
            MatUf = 0.35m,
            Area = "Cables"
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P2",
            Codigo = "THHN10",
            Descripcion = "THHN 10 AWG",
            Unidad = "ML",
            Cantidad = 5m,
            MatUf = 0.5m,
            Area = "Conductores"
        });
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "PVC20",
            Descripcion = "PVC 20 mm",
            Unidad = "UN",
            Categoria = "Cables",
            MatUf = 0.35m
        });
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "THHN10",
            Descripcion = "THHN 10 AWG",
            Unidad = "ML",
            Categoria = "Conductores",
            MatUf = 0.5m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new PurchaseListViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.False(vm.ShowEmptyState);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("Proyecto compra · 2 recursos", vm.ProjectSummary);
        Assert.Contains("Lista consolidada", vm.StatusMessage);
        Assert.True(vm.ExportExcelCommand.CanExecute(null));
        Assert.Contains(vm.CategoryOptions, x => x == "Cables");
        Assert.Contains(vm.CategoryOptions, x => x == "Conductores");
    }

    [Fact]
    public async Task SearchText_FiltersRows()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"purchase-list-filter-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("purchase-list-filter-test");
        host.Session.ProjectInfo.Nombre = "Proyecto filtro";
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "PVC20",
            Descripcion = "PVC 20 mm",
            Unidad = "UN",
            Cantidad = 10m,
            MatUf = 0.35m,
            Area = "Cables"
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P2",
            Codigo = "THHN10",
            Descripcion = "THHN 10 AWG",
            Unidad = "ML",
            Cantidad = 5m,
            MatUf = 0.5m,
            Area = "Conductores"
        });
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "PVC20",
            Descripcion = "PVC 20 mm",
            Unidad = "UN",
            Categoria = "Cables",
            MatUf = 0.35m
        });
        host.Session.Materiales.Add(new MaterialRow
        {
            Codigo = "THHN10",
            Descripcion = "THHN 10 AWG",
            Unidad = "ML",
            Categoria = "Conductores",
            MatUf = 0.5m
        });
        await host.ProjectWorkspace.SaveAsAsync(path);

        var vm = new PurchaseListViewModel(new FakeShellHost(), WpfAppHost.Current);
        vm.SearchText = "PVC";

        var visible = vm.FilteredRows.Cast<CompraMaterialViewModel>().ToList();
        Assert.Single(visible);
        Assert.Equal("PVC20", visible[0].Codigo);
        Assert.Contains("1 visibles", vm.SummaryText);
    }
}
