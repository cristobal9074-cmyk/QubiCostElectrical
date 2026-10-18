using Microsoft.Extensions.DependencyInjection;
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
using MaterialControlF = QubiCostElectrical.Wpf.Features.MaterialControl;
using CostoF = QubiCostElectrical.Wpf.Features.Costo;
using EstadosPagoF = QubiCostElectrical.Wpf.Features.EstadosPago;
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

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Registro de los ViewModels de página como transient. El <c>IShellNavigationHost</c> (el
/// <c>ShellViewModel</c> vivo) NO va al contenedor: se pasa por <c>ActivatorUtilities</c> en
/// <c>ShellPageFactory</c> porque es estado de UI, no un servicio.
/// </summary>
internal static class ViewModelRegistration
{
    public static IServiceCollection AddViewModels(this IServiceCollection s)
    {
        s.AddTransient<DashboardF.DashboardViewModel>();
        s.AddTransient<ProjectF.ProjectViewModel>();
        s.AddTransient<DocumentsF.DocumentsViewModel>();
        s.AddTransient<ItemizadoF.ItemizadoViewModel>();
        s.AddTransient<CubicacionF.CubicacionViewModel>();
        s.AddTransient<CubicacionScF.CubicacionScViewModel>();
        s.AddTransient<ApuF.ApuWorkspaceVm>();
        s.AddTransient<TemplatesF.PlantillasVm>();
        s.AddTransient<PricesF.PriceBankViewModel>();
        s.AddTransient<QuotesF.QuotesViewModel>();
        s.AddTransient<PurchasesF.PurchaseListViewModel>();
        s.AddTransient<ReviewF.ReviewViewModel>();
        s.AddTransient<VersionsF.VersionsViewModel>();
        s.AddTransient<ExportF.ExportViewModel>();
        s.AddTransient<CurrencyF.CurrencyViewModel>();
        s.AddTransient<PreferencesF.PreferencesViewModel>();
        s.AddTransient<ChangeOrdersF.ChangeOrdersViewModel>();
        s.AddTransient<FloorSummaryF.FloorSummaryViewModel>();
        s.AddTransient<SolicitudesF.SolicitudesViewModel>();
        s.AddTransient<MaterialControlF.MaterialControlViewModel>();
        s.AddTransient<CostoF.CostoViewModel>();
        s.AddTransient<EstadosPagoF.EstadosPagoViewModel>();
        s.AddTransient<EppF.EppViewModel>();
        s.AddTransient<ManoDeObraF.ManoDeObraViewModel>();
        s.AddTransient<EconomicEditorF.EconomicEditorViewModel>();
        s.AddTransient<ContractsF.ContractWorkspaceViewModel>();
        s.AddTransient<ContractsF.ContractEditorViewModel>();
        s.AddTransient<ContractsF.ContractVersionsViewModel>();
        return s;
    }
}
