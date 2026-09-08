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
using CostoF = QubiCostElectrical.Wpf.Features.Costo;
using EppF = QubiCostElectrical.Wpf.Features.Epp;
using ManoDeObraF = QubiCostElectrical.Wpf.Features.ManoDeObra;
using ApuCatalogF = QubiCostElectrical.Wpf.Features.ApuCatalog;
using ApuMergeF = QubiCostElectrical.Wpf.Features.ApuMerge;
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
        s.AddTransient<CostoF.CostoViewModel>();
        s.AddTransient<EppF.EppViewModel>();
        s.AddTransient<ManoDeObraF.ManoDeObraViewModel>();
        s.AddTransient<ApuCatalogF.ApuCatalogViewModel>();
        s.AddTransient<ApuMergeF.ApuMergeViewModel>();
        s.AddTransient<EconomicEditorF.EconomicEditorViewModel>();
        s.AddTransient<ContractsF.ContractWorkspaceViewModel>();
        s.AddTransient<ContractsF.ContractEditorViewModel>();
        s.AddTransient<ContractsF.ContractVersionsViewModel>();
        return s;
    }
}
