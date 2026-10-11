using System.IO;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Delivery;
using QubiCostElectrical.Wpf.Features.Export;
using QubiCostElectrical.Wpf.Features.Review;
using QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[CollectionDefinition(Name)]
public sealed class WpfHostCollectionDefinition : ICollectionFixture<WpfHostFixture>
{
    public const string Name = "WpfHost";
}

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class ShellReviewAndVersionsTests
{
    private sealed class FakeShellHost : IShellNavigationHost
    {
        public ShellRoute CurrentRoute { get; private set; } = ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public ShellRoute? LastRoute { get; private set; }
        public WorkspaceNavigationContext? LastContext { get; private set; }

        public bool RequestNavigate(ShellRoute route, bool force = false)
        {
            CurrentRoute = route;
            LastRoute = route;
            LastContext = null;
            return true;
        }

        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
        {
            CurrentRoute = route;
            LastRoute = route;
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

    public ShellReviewAndVersionsTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void Review_GoToIssue_NavigatesToTakeoffWithContext()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("review-test");
        host.Session.ProjectInfo.Nombre = "Proyecto de prueba";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización",
            Unidad = "GL"
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Codigo = "PVC20",
            Descripcion = "PVC 20",
            Cantidad = 0m,
            MatUf = 1m
        });

        var shell = new FakeShellHost();
        var vm = new ReviewViewModel(shell, WpfAppHost.Current);

        Assert.NotEmpty(vm.Issues);

        vm.SelectedIssue = vm.Issues.First(x => x.Source.Category == FinalReviewCategory.ZeroQuantity);
        Assert.True(vm.GoToIssueCommand.CanExecute(null));

        vm.GoToIssueCommand.Execute(null);

        Assert.Equal(ShellRoute.Takeoff, shell.LastRoute);
        Assert.IsType<TakeoffNavigationContext>(shell.LastContext);
        var context = (TakeoffNavigationContext)shell.LastContext!;
        Assert.Equal("P1", context.ItemId);
        Assert.True(string.IsNullOrWhiteSpace(context.ItemCode) || context.ItemCode == "PVC20");
        Assert.True(string.IsNullOrWhiteSpace(context.Description) || context.Description == "PVC 20");
    }

    [Fact]
    public async Task Versions_ShowComparisonAgainstCurrentSnapshot()
    {
        var host = WpfAppHost.Current;
        var path = Path.Combine(Path.GetTempPath(), $"versions-cmp-{Guid.NewGuid():N}.qce");
        host.Session.NewProject("versions-test");
        host.Session.ProjectInfo.Nombre = "Proyecto versiones";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización"
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Descripcion = "PVC 20",
            Cantidad = 10m,
            MatClp = 39000m,
            MatUf = 1m
        });

        var baseline = new BudgetVersionData
        {
            Proyecto = new ProjectInfoRow { Nombre = "Proyecto versiones" },
            Itemizado =
            [
                new ItemizadoRow
                {
                    Id = "P1",
                    Item = "1.1",
                    Descripcion = "Canalización"
                }
            ],
            Cubicacion =
            [
                new CubicacionRow
                {
                    ItemId = "P1",
                    Codigo = "PVC20",
                    Descripcion = "PVC 20",
                    Cantidad = 10m,
                    MatClp = 39000m,
                    MatUf = 1m
                }
            ]
        };
        host.Session.Versiones.Add(new BudgetVersionRow
        {
            Codigo = "V1",
            Nombre = "Base",
            Snapshot = baseline,
            MontoVentaUf = 10m
        });
        host.Session.ActiveVersionId = host.Session.Versiones[0].Id;
        await host.ProjectWorkspace.SaveAsAsync(path);

        host.Session.Cubicacion[0].Cantidad = 12m;
        host.Session.MarkDirty("cubicacion");

        var vm = new VersionsViewModel(new FakeShellHost(), WpfAppHost.Current);

        Assert.NotNull(vm.SelectedVersion);
        Assert.True(vm.SelectedVersion!.IsActive);
        Assert.Equal("V1", vm.SelectedVersion.Codigo);
        Assert.Equal("1 partidas comparadas", vm.ComparisonSummaryText);
        Assert.Equal("0 nuevas", vm.ComparisonAddedText);
        Assert.Equal("0 eliminadas", vm.ComparisonRemovedText);
        Assert.Contains("Estado actual", vm.ComparisonStatusText);
        Assert.NotEmpty(vm.ComparisonRows);

        var row = vm.ComparisonRows.First();
        Assert.Equal("P1", row.ItemId);
        Assert.Contains("Activa", vm.ActiveVersionText);
    }

    [Fact]
    public void Export_NavigateToExport_BuildsDeliveryShellState()
    {
        var host = WpfAppHost.Current;
        host.Session.NewProject("export-test");
        host.Session.ProjectInfo.Nombre = "Proyecto entrega";
        host.Session.ProjectInfo.Cliente = "Cliente entrega";
        host.Session.ProjectInfo.EstadoPresupuesto = "Borrador";
        host.Session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización"
        });
        host.Session.Cubicacion.Add(new CubicacionRow
        {
            ItemId = "P1",
            Descripcion = "PVC 20",
            Cantidad = 5m,
            MatUf = 1m
        });

        var info = ShellRouteCatalog.Get(ShellRoute.OfferDocuments);
        Assert.Equal("Cierre", info.Group);
        Assert.Equal("Oferta y exportación", info.Title);

        var vm = new ExportViewModel(WpfAppHost.Current);

        Assert.Equal("Proyecto entrega", vm.ProjectTitle);
        Assert.Equal("Cliente entrega", vm.ClientName);
        Assert.Equal("Borrador", vm.StateText);
        Assert.Equal(8, vm.Actions.Count);
        Assert.Contains(vm.Actions, x => x.Primary);
        Assert.Contains(vm.Actions, x => x.Title == "Oferta comercial");
        Assert.Contains(vm.Actions, x => x.Title == "Paquete final");
        Assert.Contains("PDF de oferta", vm.DeliveryPackageText);
        Assert.Contains("Excel interno", vm.DeliveryPackageText);
        Assert.Contains("carta DOCX", vm.DeliveryPackageText);
        Assert.Equal(7, vm.DocumentSelector.Documents.Count);
        Assert.Contains(vm.DocumentSelector.Documents, x => x.Title == "Oferta comercial PDF");
        Assert.Contains(vm.DocumentSelector.Documents, x => x.Title == "Reporte t\u00e9cnico C.SEC");
        Assert.Contains("Tema corporativo", vm.CorporateThemeText);
        Assert.Contains("Plantilla de informe", vm.ReportTemplateText);
        Assert.NotEmpty(vm.SummaryText);
        Assert.NotEmpty(vm.StatusMessage);
        Assert.NotEmpty(vm.ExportReadinessText);
    }
}
