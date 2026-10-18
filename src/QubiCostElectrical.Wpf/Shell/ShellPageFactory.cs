using System;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Features.Currency;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Inicio;
using QubiCostElectrical.Wpf.Features.Documents;
using CubicacionFeature = QubiCostElectrical.Wpf.Features.Cubicacion;
using CubicacionScFeature = QubiCostElectrical.Wpf.Features.CubicacionSc;
using ApuFeature = QubiCostElectrical.Wpf.Features.Apu;
using QuotesFeature = QubiCostElectrical.Wpf.Features.Quotes;
using PurchasesFeature = QubiCostElectrical.Wpf.Features.Purchases;
using TemplatesFeature = QubiCostElectrical.Wpf.Features.Templates;
using QubiCostElectrical.Wpf.Features.Itemizado;
using QubiCostElectrical.Wpf.Features.FloorSummary;
using QubiCostElectrical.Wpf.Features.Solicitudes;
using QubiCostElectrical.Wpf.Features.MaterialControl;
using QubiCostElectrical.Wpf.Features.Costo;
using QubiCostElectrical.Wpf.Features.Epp;
using QubiCostElectrical.Wpf.Features.ManoDeObra;
using EconomicEditorFeature = QubiCostElectrical.Wpf.Features.EconomicEditor;
using ContractsFeature = QubiCostElectrical.Wpf.Features.Contracts;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Features.Project;
using QubiCostElectrical.Wpf.Navigation;
using ExportFeature = QubiCostElectrical.Wpf.Features.Export;
using ReviewFeature = QubiCostElectrical.Wpf.Features.Review;
using VersionsFeature = QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Features.Preferences;
using QubiCostElectrical.Wpf.Features.ChangeOrders;
using EstadosPagoFeature = QubiCostElectrical.Wpf.Features.EstadosPago;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Crea (View, ViewModel) por <see cref="ShellRoute"/>. Sin cache ni lifecycle de sesión.
/// </summary>
internal static class ShellPageFactory
{
    public static (UserControl View, object? ViewModel) Create(
        ShellRoute route,
        System.IServiceProvider sp,
        IShellNavigationHost shell)
    {
        var appHost = sp.GetRequiredService<WpfAppHost>();
        TVm Vm<TVm>(params object[] extra) => ActivatorUtilities.CreateInstance<TVm>(sp, PrependShell(shell, extra));
        switch (route)
        {
            case ShellRoute.Dashboard:
                {
                    var vm = Vm<DashboardViewModel>();
                    // Home unificado (misma ruta Dashboard, mismo VM):
                    // - Sin proyecto → InicioView (landing; IsLandingScreen = true)
                    // - Con proyecto → DashboardView (resumen operativo)
                    UserControl view = appHost.ProjectWorkspace.HasOpenProject
                        ? new DashboardView { DataContext = vm }
                        : new InicioView { DataContext = vm };
                    return (view, vm);
                }
            case ShellRoute.Project:
                {
                    var vm = Vm<ProjectViewModel>();
                    return (new ProjectView { DataContext = vm }, vm);
                }
            case ShellRoute.Documents:
                {
                    var vm = Vm<DocumentsViewModel>();
                    return (new DocumentsView { DataContext = vm }, vm);
                }
            case ShellRoute.Itemized:
                {
                    var vm = Vm<ItemizadoViewModel>();
                    return (new ItemizadoView { DataContext = vm }, vm);
                }
            case ShellRoute.Takeoff:
                {
                    var vm = Vm<CubicacionFeature.CubicacionViewModel>();
                    return (new CubicacionFeature.CubicacionView(vm), vm);
                }
            case ShellRoute.CubicacionSc:
                {
                    var vm = Vm<CubicacionScFeature.CubicacionScViewModel>();
                    return (new CubicacionScFeature.CubicacionScView(vm), vm);
                }
            case ShellRoute.Apu:
                {
                    var vm = Vm<ApuFeature.ApuWorkspaceVm>();
                    return (new ApuFeature.ApuView(vm), vm);
                }
            case ShellRoute.Templates:
                {
                    var vm = Vm<TemplatesFeature.PlantillasVm>();
                    return (new TemplatesFeature.PlantillasView(vm), vm);
                }
            case ShellRoute.Prices:
                {
                    var vm = Vm<PriceBankViewModel>();
                    return (new PriceBankView(vm), vm);
                }
            case ShellRoute.Quotes:
                {
                    var vm = Vm<QuotesFeature.QuotesViewModel>();
                    return (new QuotesFeature.QuotesView { DataContext = vm }, vm);
                }
            case ShellRoute.Purchases:
                {
                    var vm = Vm<PurchasesFeature.PurchaseListViewModel>();
                    return (new PurchasesFeature.PurchaseListView { DataContext = vm }, vm);
                }
            case ShellRoute.Review:
                {
                    var vm = Vm<ReviewFeature.ReviewViewModel>();
                    return (new ReviewFeature.ReviewView { DataContext = vm }, vm);
                }
            case ShellRoute.Versions:
                {
                    var vm = Vm<VersionsFeature.VersionsViewModel>();
                    return (new VersionsFeature.VersionsView { DataContext = vm }, vm);
                }
            case ShellRoute.OfferDocuments:
                {
                    var vm = Vm<ExportFeature.ExportViewModel>();
                    return (new ExportFeature.ExportView { DataContext = vm }, vm);
                }
            case ShellRoute.Currency:
                {
                    var vm = ActivatorUtilities.CreateInstance<CurrencyViewModel>(sp);
                    return (new CurrencyView { DataContext = vm }, vm);
                }
            case ShellRoute.Preferences:
                {
                    var vm = ActivatorUtilities.CreateInstance<PreferencesViewModel>(sp);
                    return (new PreferencesView(appHost) { DataContext = vm }, vm);
                }
            case ShellRoute.ChangeOrders:
                {
                    var vm = ActivatorUtilities.CreateInstance<ChangeOrdersViewModel>(sp);
                    return (new ChangeOrdersView { DataContext = vm }, vm);
                }
            case ShellRoute.FloorSummary:
                {
                    var vm = Vm<FloorSummaryViewModel>();
                    return (new FloorSummaryView { DataContext = vm }, vm);
                }
            case ShellRoute.Solicitudes:
                {
                    var vm = Vm<SolicitudesViewModel>();
                    return (new SolicitudesView { DataContext = vm }, vm);
                }
            case ShellRoute.MaterialControl:
                {
                    var vm = Vm<MaterialControlViewModel>();
                    return (new MaterialControlView { DataContext = vm }, vm);
                }
            case ShellRoute.EstadosPagos:
                {
                    var vm = Vm<EstadosPagoFeature.EstadosPagoViewModel>();
                    return (new EstadosPagoFeature.EstadosPagoView { DataContext = vm }, vm);
                }
            case ShellRoute.Costo:
                {
                    var vm = Vm<CostoViewModel>();
                    return (new CostoView { DataContext = vm }, vm);
                }
            case ShellRoute.Epp:
                {
                    var vm = Vm<EppViewModel>();
                    return (new EppView { DataContext = vm }, vm);
                }
            case ShellRoute.ManoDeObra:
                {
                    var vm = Vm<ManoDeObraViewModel>();
                    return (new ManoDeObraView { DataContext = vm }, vm);
                }
            case ShellRoute.EconomicEditor:
                {
                    var vm = Vm<EconomicEditorFeature.EconomicEditorViewModel>();
                    return (new EconomicEditorFeature.EconomicEditorView { DataContext = vm }, vm);
                }
            case ShellRoute.ContractWorkspace:
                {
                    var vm = Vm<ContractsFeature.ContractWorkspaceViewModel>();
                    return (new ContractsFeature.ContractWorkspaceView { DataContext = vm }, vm);
                }
            case ShellRoute.ContractEditor:
                {
                    var placeholder = new Contract { Id = ContractId.New(), Status = ContractStatus.Borrador };
                    var vm = Vm<ContractsFeature.ContractEditorViewModel>(placeholder);
                    return (new ContractsFeature.ContractEditorView { DataContext = vm }, vm);
                }
            case ShellRoute.ContractVersions:
                {
                    var vm = Vm<ContractsFeature.ContractVersionsViewModel>(ContractId.New());
                    return (new ContractsFeature.ContractVersionsView { DataContext = vm }, vm);
                }
            default:
                throw new InvalidOperationException($"La ruta {route} no tiene vista registrada.");
        }
    }

    private static object[] PrependShell(IShellNavigationHost shell, object[] extra)
    {
        var args = new object[extra.Length + 1];
        args[0] = shell;
        System.Array.Copy(extra, 0, args, 1, extra.Length);
        return args;
    }
}
