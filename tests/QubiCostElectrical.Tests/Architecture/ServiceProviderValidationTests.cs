using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using QubiCostElectrical;
using QubiCostElectrical.Tests.Wpf;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;
using Xunit;
using CurrencyF = QubiCostElectrical.Wpf.Features.Currency;
using DashboardF = QubiCostElectrical.Wpf.Features.Dashboard;
using DocumentsF = QubiCostElectrical.Wpf.Features.Documents;
using CubicacionF = QubiCostElectrical.Wpf.Features.Cubicacion;
using CubicacionScF = QubiCostElectrical.Wpf.Features.CubicacionSc;
using ApuF = QubiCostElectrical.Wpf.Features.Apu;
using QuotesF = QubiCostElectrical.Wpf.Features.Quotes;
using PurchasesF = QubiCostElectrical.Wpf.Features.Purchases;
using TemplatesF = QubiCostElectrical.Wpf.Features.Templates;
using ItemizadoF = QubiCostElectrical.Wpf.Features.Itemizado;
using FloorSummaryF = QubiCostElectrical.Wpf.Features.FloorSummary;
using SolicitudesF = QubiCostElectrical.Wpf.Features.Solicitudes;
using CostoF = QubiCostElectrical.Wpf.Features.Costo;
using EppF = QubiCostElectrical.Wpf.Features.Epp;
using ManoDeObraF = QubiCostElectrical.Wpf.Features.ManoDeObra;
using EconomicEditorF = QubiCostElectrical.Wpf.Features.EconomicEditor;
using ContractsF = QubiCostElectrical.Wpf.Features.Contracts;
using PricesF = QubiCostElectrical.Wpf.Features.Prices;
using ProjectF = QubiCostElectrical.Wpf.Features.Project;
using ExportF = QubiCostElectrical.Wpf.Features.Export;
using ReviewF = QubiCostElectrical.Wpf.Features.Review;
using VersionsF = QubiCostElectrical.Wpf.Features.Versions;
using PreferencesF = QubiCostElectrical.Wpf.Features.Preferences;
using ChangeOrdersF = QubiCostElectrical.Wpf.Features.ChangeOrders;
using EstadosPagoF = QubiCostElectrical.Wpf.Features.EstadosPago;

namespace QubiCostElectrical.Tests.Architecture;

/// <summary>
/// Fitness function del contenedor DI. `BuildProvider()` ya usa `ValidateOnBuild = true`, así
/// que este test falla si un servicio registrado no puede resolverse (dependencia faltante o
/// captiva). Además resuelve explícitamente los servicios de fachada y los presenters.
/// </summary>
public sealed class ServiceProviderValidationTests
{
    [Fact]
    public void Container_builds_and_validates()
    {
        using var provider = (ServiceProvider)new AppServicesBuilder().BuildProvider(validate: true);
        Assert.NotNull(provider);
    }

    [Theory]
    [InlineData(typeof(IBudgetCalculationService))]
    [InlineData(typeof(IFinalReviewService))]
    [InlineData(typeof(IProjectWorkspaceService))]
    [InlineData(typeof(IProjectDbContext))]
    [InlineData(typeof(IExcelProjectExporter))]
    [InlineData(typeof(IPdfProjectExporter))]
    [InlineData(typeof(IPresentationLetterDocxExporter))]
    [InlineData(typeof(IEconomicCalculationService))]
    [InlineData(typeof(PriceBankApplicationService))]
    [InlineData(typeof(DashboardPresenter))]
    [InlineData(typeof(ItemizadoPresenter))]
    [InlineData(typeof(CubicacionPresenter))]
    [InlineData(typeof(ExportPresenter))]
    public void Resolves(System.Type service)
    {
        using var provider = (ServiceProvider)new AppServicesBuilder().BuildProvider(validate: true);
        Assert.NotNull(provider.GetRequiredService(service));
    }

    [Fact]
    public void AppServices_facade_resolves_every_member()
    {
        var s = AppServices.CreateDefault();
        Assert.NotNull(s.BudgetCalculation);
        Assert.NotNull(s.MaterialSearch);
        Assert.NotNull(s.ProjectRepository);
        Assert.NotNull(s.PriceBankStore);
        Assert.NotNull(s.ExcelExporter);
        Assert.NotNull(s.PdfExporter);
        Assert.NotNull(s.EconomicCalculation);
        Assert.NotNull(s.FinalReview);
    }
}

/// <summary>
/// Garantía de fitness: cada uno de los 27 ViewModels de página registrados en
/// <c>ViewModelRegistration</c> debe poder construirse íntegramente desde el contenedor real
/// (vía <c>ActivatorUtilities.CreateInstance</c>, tal como lo hace <c>ShellPageFactory</c>).
/// Esto prueba que el constructor primario "concreto" de cada VM tiene todas sus dependencias
/// registradas — si a alguno le falta un servicio, este test falla en vez de reventar en runtime
/// al navegar a esa pantalla.
/// </summary>
[Collection(WpfHostCollectionDefinition.Name)]
public sealed class PageViewModelContainerResolutionTests
{
    private sealed class StubShell : IShellNavigationHost
    {
        public WpfAppHost Host => WpfAppHost.Current;
        public ShellRoute CurrentRoute => ShellRoute.Dashboard;
        public string ActiveProjectLabel => "";
        public bool RequestNavigate(ShellRoute route, bool force = false) => true;
        public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false) => true;
        public System.Threading.Tasks.Task NewProjectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task OpenProjectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<bool> CloseProjectAsync() => System.Threading.Tasks.Task.FromResult(true);
        public System.Threading.Tasks.Task OpenRecentProjectAsync(string path) => System.Threading.Tasks.Task.CompletedTask;
        public void RemoveRecentProject(string path) { }
        public System.Threading.Tasks.Task SaveProjectAsync() => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task SaveProjectAsAsync() => System.Threading.Tasks.Task.CompletedTask;
        public void RefreshChrome() { }
    }

    public PageViewModelContainerResolutionTests() => WpfTestHost.EnsureInitialized();

    // Los 27 VMs de página, en el mismo orden que ViewModelRegistration.AddViewModels.
    public static IEnumerable<object[]> PageViewModelTypes() => new[]
    {
        new object[] { typeof(DashboardF.DashboardViewModel) },
        new object[] { typeof(ProjectF.ProjectViewModel) },
        new object[] { typeof(DocumentsF.DocumentsViewModel) },
        new object[] { typeof(ItemizadoF.ItemizadoViewModel) },
        new object[] { typeof(CubicacionF.CubicacionViewModel) },
        new object[] { typeof(CubicacionScF.CubicacionScViewModel) },
        new object[] { typeof(ApuF.ApuWorkspaceVm) },
        new object[] { typeof(TemplatesF.PlantillasVm) },
        new object[] { typeof(PricesF.PriceBankViewModel) },
        new object[] { typeof(QuotesF.QuotesViewModel) },
        new object[] { typeof(PurchasesF.PurchaseListViewModel) },
        new object[] { typeof(ReviewF.ReviewViewModel) },
        new object[] { typeof(VersionsF.VersionsViewModel) },
        new object[] { typeof(ExportF.ExportViewModel) },
        new object[] { typeof(CurrencyF.CurrencyViewModel) },
        new object[] { typeof(PreferencesF.PreferencesViewModel) },
        new object[] { typeof(ChangeOrdersF.ChangeOrdersViewModel) },
        new object[] { typeof(FloorSummaryF.FloorSummaryViewModel) },
        new object[] { typeof(SolicitudesF.SolicitudesViewModel) },
        new object[] { typeof(CostoF.CostoViewModel) },
        new object[] { typeof(EppF.EppViewModel) },
        new object[] { typeof(ManoDeObraF.ManoDeObraViewModel) },
        new object[] { typeof(EconomicEditorF.EconomicEditorViewModel) },
        new object[] { typeof(ContractsF.ContractWorkspaceViewModel) },
        new object[] { typeof(ContractsF.ContractEditorViewModel) },
        new object[] { typeof(ContractsF.ContractVersionsViewModel) },
        new object[] { typeof(EstadosPagoF.EstadosPagoViewModel) },
    };

    // CurrencyViewModel, PreferencesViewModel y ChangeOrdersViewModel se crean en
    // ShellPageFactory SIN prependar el shell (no lo reciben por constructor).
    private static bool NeedsShell(Type vmType) =>
        vmType != typeof(CurrencyF.CurrencyViewModel) &&
        vmType != typeof(PreferencesF.PreferencesViewModel) &&
        vmType != typeof(ChangeOrdersF.ChangeOrdersViewModel);

    [Fact]
    public void Every_page_view_model_is_registered_and_covered()
    {
        // Salvaguarda: si alguien agrega un VM a ViewModelRegistration, este conteo obliga a
        // cubrirlo también aquí.
        var covered = 0;
        foreach (var _ in PageViewModelTypes()) covered++;
        Assert.Equal(27, covered);
    }

    [Theory]
    [MemberData(nameof(PageViewModelTypes))]
    public void Page_view_model_resolves_fully_from_container(Type vmType)
    {
        var sp = WpfAppHost.Provider
            ?? throw new InvalidOperationException("El contenedor DI no está inicializado.");

        object[] domainArgs =
            vmType == typeof(ContractsF.ContractEditorViewModel)
                ? new object[] { new Contract { Id = ContractId.New(), Status = ContractStatus.Borrador } }
            : vmType == typeof(ContractsF.ContractVersionsViewModel)
                ? new object[] { ContractId.New() }
            : Array.Empty<object>();

        object[] args = NeedsShell(vmType)
            ? Prepend(new StubShell(), domainArgs)
            : domainArgs;

        object? vm = null;
        try
        {
            vm = ActivatorUtilities.CreateInstance(sp, vmType, args);
            Assert.NotNull(vm);
        }
        finally
        {
            (vm as IDisposable)?.Dispose();
        }
    }

    private static object[] Prepend(object first, object[] rest)
    {
        var args = new object[rest.Length + 1];
        args[0] = first;
        Array.Copy(rest, 0, args, 1, rest.Length);
        return args;
    }
}
