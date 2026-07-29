using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using QubiCostElectrical;
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
using QubiCostElectrical.Wpf.Features.Costo;
using QubiCostElectrical.Wpf.Features.Epp;
using QubiCostElectrical.Wpf.Features.ManoDeObra;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Features.Project;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using ExportFeature = QubiCostElectrical.Wpf.Features.Export;
using ReviewFeature = QubiCostElectrical.Wpf.Features.Review;
using VersionsFeature = QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Features.Preferences;
using QubiCostElectrical.Wpf.Features.ChangeOrders;
using EconomicEditorFeature = QubiCostElectrical.Wpf.Features.EconomicEditor;
using ScenarioWorkspaceFeature = QubiCostElectrical.Wpf.Features.ScenarioWorkspace;
using ApuCatalogFeature = QubiCostElectrical.Wpf.Features.ApuCatalog;
using ApuMergeFeature = QubiCostElectrical.Wpf.Features.ApuMerge;
using BaselineManagerFeature = QubiCostElectrical.Wpf.Features.BaselineManager;
using ProgressWorkspaceFeature = QubiCostElectrical.Wpf.Features.ProgressWorkspace;
using EarnedValueFeature = QubiCostElectrical.Wpf.Features.EarnedValueWorkspace;
using ExecutiveDashboardFeature = QubiCostElectrical.Wpf.Features.ExecutiveDashboard;
using OperationalDashboardFeature = QubiCostElectrical.Wpf.Features.OperationalDashboard;
using SuppliersFeature = QubiCostElectrical.Wpf.Features.Suppliers;
using PurchaseRequisitionsFeature = QubiCostElectrical.Wpf.Features.PurchaseRequisitions;
using RfqFeature = QubiCostElectrical.Wpf.Features.Rfq;
using QuotationsFeature = QubiCostElectrical.Wpf.Features.Quotations;
using BidComparisonFeature = QubiCostElectrical.Wpf.Features.BidComparison;
using TechnicalEvaluationFeature = QubiCostElectrical.Wpf.Features.TechnicalEvaluation;
using AwardsFeature = QubiCostElectrical.Wpf.Features.Awards;
using QubiCostElectrical.Wpf.Features.ContractChanges;
using QubiCostElectrical.Wpf.Features.ContractAdministration;
using QubiCostElectrical.Wpf.Features.Closeout;
using QubiCostElectrical.Wpf.Features.CloseoutFinal;

namespace QubiCostElectrical.Wpf.Shell;

partial class ShellViewModel
{
    private readonly Dictionary<ShellRoute, CachedPage> pageCache = new();

    private sealed record CachedPage(UserControl View, object? ViewModel);

    public void ClearPageCache()
    {
        foreach (var entry in pageCache.Values)
        {
            if (entry.ViewModel is IDisposable disposable)
                disposable.Dispose();
        }

        pageCache.Clear();
        currentView = null;
        currentPageVm = null;
    }

    public bool RequestNavigate(ShellRoute route, bool force = false)
        => RequestNavigate(route, null, force);

    public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
    {
        if (!force && currentPageVm is IPageWithPendingChanges pending && pending.HasPendingChanges)
        {
            _ = SaveProjectAsync();
            pending.TryDiscardChanges();
        }

        if (context is TakeoffNavigationContext takeoff)
            host.Session.PendingTakeoffNavigation = takeoff;

        if (route == currentRoute && currentView != null)
        {
            ApplyNavigationContext(context, currentView);
            if (currentPageVm is IRefreshablePage refreshableOnSameRoute)
                refreshableOnSameRoute.Refresh();
            return true;
        }

        if (pageCache.TryGetValue(route, out var cached))
        {
            CurrentRoute = route;
            CurrentView = cached.View;
            currentPageVm = cached.ViewModel;
            if (currentPageVm is IRefreshablePage refreshable)
                refreshable.Refresh();
            RefreshContext();
            ApplyNavigationContext(context, cached.View);
            return true;
        }

        CurrentRoute = route;
        var (view, vm) = CreateView(route, this, host);
        pageCache[route] = new CachedPage(view, vm);
        CurrentView = view;
        currentPageVm = vm;
        RefreshContext();
        ApplyNavigationContext(context, view);
        return true;
    }

    private static void ApplyNavigationContext(WorkspaceNavigationContext? context, UserControl view)
    {
        switch (context)
        {
            case ApuFeature.ApuNavContext apuCtx when view is ApuFeature.ApuView apuView:
                apuView.ApplyNavContext(apuCtx);
                break;
            case PriceBankNavigationContext pbCtx when view is PriceBankView pbView:
                pbView.ApplyNavContext(pbCtx);
                break;
            case TakeoffNavigationContext takeoffCtx when view is CubicacionFeature.CubicacionView takeoffView:
                if (takeoffView.DataContext is CubicacionFeature.CubicacionViewModel vm)
                {
                    var target = vm.Partidas.FirstOrDefault(p =>
                        string.Equals(p.Id, takeoffCtx.ItemId, StringComparison.OrdinalIgnoreCase));
                    if (target != null)
                        vm.SelectPartida(target);
                }
                break;
        }
    }

    private static (UserControl View, object? ViewModel) CreateView(ShellRoute route, IShellNavigationHost shell, WpfAppHost appHost)
    {
        switch (route)
        {
            case ShellRoute.Dashboard:
            {
                var vm = new DashboardViewModel(shell, appHost);
                // Inicio y Resumen son vistas independientes: el diseñador y el usuario
                // nunca cargan ambas pantallas dentro del mismo XAML.
                UserControl view = appHost.ProjectWorkspace.HasOpenProject
                    ? new DashboardView { DataContext = vm }
                    : new InicioView { DataContext = vm };
                return (view, vm);
            }
            case ShellRoute.Project:
            {
                var vm = new ProjectViewModel(shell, appHost);
                return (new ProjectView { DataContext = vm }, vm);
            }
            case ShellRoute.Documents:
            {
                var vm = new DocumentsViewModel(shell, appHost);
                return (new DocumentsView { DataContext = vm }, vm);
            }
            case ShellRoute.Itemized:
            {
                var vm = new ItemizadoViewModel(shell, appHost);
                return (new ItemizadoView { DataContext = vm }, vm);
            }
            case ShellRoute.Takeoff:
            {
                var vm = new CubicacionFeature.CubicacionViewModel(shell, appHost);
                return (new CubicacionFeature.CubicacionView(vm), vm);
            }
            case ShellRoute.CubicacionSc:
            {
                var vm = new CubicacionScFeature.CubicacionScViewModel(shell, appHost);
                return (new CubicacionScFeature.CubicacionScView(vm), vm);
            }
            case ShellRoute.Apu:
            {
                var vm = new ApuFeature.ApuWorkspaceVm(shell, appHost);
                return (new ApuFeature.ApuView(vm), vm);
            }
            case ShellRoute.Templates:
            {
                var vm = new TemplatesFeature.PlantillasVm(shell, appHost);
                return (new TemplatesFeature.PlantillasView(vm), vm);
            }
            case ShellRoute.Prices:
            {
                var vm = new PriceBankViewModel(appHost);
                return (new PriceBankView(vm), vm);
            }
            case ShellRoute.Quotes:
            {
                var vm = new QuotesFeature.QuotesViewModel(shell, appHost);
                return (new QuotesFeature.QuotesView { DataContext = vm }, vm);
            }
            case ShellRoute.Purchases:
            {
                var vm = new PurchasesFeature.PurchaseListViewModel(shell, appHost);
                return (new PurchasesFeature.PurchaseListView { DataContext = vm }, vm);
            }
            case ShellRoute.Review:
            {
                var vm = new ReviewFeature.ReviewViewModel(shell, appHost);
                return (new ReviewFeature.ReviewView { DataContext = vm }, vm);
            }
            case ShellRoute.Versions:
            {
                var vm = new VersionsFeature.VersionsViewModel(shell, appHost);
                return (new VersionsFeature.VersionsView { DataContext = vm }, vm);
            }
            case ShellRoute.OfferDocuments:
            {
                var vm = new ExportFeature.ExportViewModel(appHost);
                return (new ExportFeature.ExportView { DataContext = vm }, vm);
            }
            case ShellRoute.Currency:
            {
                var vm = new CurrencyViewModel(appHost);
                return (new CurrencyView { DataContext = vm }, vm);
            }
            case ShellRoute.Preferences:
            {
                var vm = new PreferencesViewModel(appHost);
                return (new PreferencesView(appHost) { DataContext = vm }, vm);
            }
            case ShellRoute.ChangeOrders:
            {
                var vm = new ChangeOrdersViewModel(appHost);
                return (new ChangeOrdersView { DataContext = vm }, vm);
            }
            case ShellRoute.FloorSummary:
            {
                var vm = new FloorSummaryViewModel(shell, appHost);
                return (new FloorSummaryView { DataContext = vm }, vm);
            }
            case ShellRoute.Solicitudes:
            {
                var vm = new SolicitudesViewModel(shell, appHost);
                return (new SolicitudesView { DataContext = vm }, vm);
            }
            case ShellRoute.Costo:
            {
                var vm = new CostoViewModel(shell, appHost);
                return (new CostoView { DataContext = vm }, vm);
            }
            case ShellRoute.Epp:
            {
                var vm = new EppViewModel(shell, appHost);
                return (new EppView { DataContext = vm }, vm);
            }
            case ShellRoute.ManoDeObra:
            {
                var vm = new ManoDeObraViewModel(shell, appHost);
                return (new ManoDeObraView { DataContext = vm }, vm);
            }
            case ShellRoute.EconomicEditor:
            {
                var vm = new EconomicEditorFeature.EconomicEditorViewModel(shell, appHost);
                return (new EconomicEditorFeature.EconomicEditorView { DataContext = vm }, vm);
            }
            case ShellRoute.ScenarioWorkspace:
            {
                var vm = new ScenarioWorkspaceFeature.ScenarioWorkspaceViewModel(shell, appHost);
                return (new ScenarioWorkspaceFeature.ScenarioWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ApuCatalog:
            {
                var vm = new ApuCatalogFeature.ApuCatalogViewModel(shell, appHost);
                return (new ApuCatalogFeature.ApuCatalogView { DataContext = vm }, vm);
            }
            case ShellRoute.ApuMerge:
            {
                var vm = new ApuMergeFeature.ApuMergeViewModel(shell, appHost);
                return (new ApuMergeFeature.ApuMergeView { DataContext = vm }, vm);
            }
            case ShellRoute.PlanningBaselines:
            {
                var vm = new BaselineManagerFeature.BaselineManagerViewModel(shell, appHost);
                return (new BaselineManagerFeature.BaselineManagerView { DataContext = vm }, vm);
            }
            case ShellRoute.PlanningProgress:
            {
                var vm = new ProgressWorkspaceFeature.ProgressWorkspaceViewModel(shell, appHost);
                return (new ProgressWorkspaceFeature.ProgressWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.PlanningEarnedValue:
            {
                var vm = new EarnedValueFeature.EarnedValueWorkspaceViewModel(shell, appHost);
                return (new EarnedValueFeature.EarnedValueWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.PlanningExecutiveDashboard:
            {
                var vm = new ExecutiveDashboardFeature.ExecutiveDashboardViewModel(shell, appHost);
                return (new ExecutiveDashboardFeature.ExecutiveDashboardView { DataContext = vm }, vm);
            }
            case ShellRoute.PlanningOperationalDashboard:
            {
                var vm = new OperationalDashboardFeature.OperationalDashboardViewModel(shell, appHost);
                return (new OperationalDashboardFeature.OperationalDashboardView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementSuppliers:
            {
                var vm = new SuppliersFeature.SupplierWorkspaceViewModel(shell, appHost);
                return (new SuppliersFeature.SupplierWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementRequisitions:
            {
                var vm = new PurchaseRequisitionsFeature.PurchaseRequisitionWorkspaceViewModel(shell, appHost);
                return (new PurchaseRequisitionsFeature.PurchaseRequisitionWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementRfq:
            {
                var vm = new RfqFeature.RfqWorkspaceViewModel(shell, appHost);
                return (new RfqFeature.RfqWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementQuotations:
            {
                var vm = new QuotationsFeature.QuotationWorkspaceViewModel(shell, appHost);
                return (new QuotationsFeature.QuotationWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementBidComparison:
            {
                var vm = new BidComparisonFeature.BidComparisonWorkspaceViewModel(shell, appHost);
                return (new BidComparisonFeature.BidComparisonWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementTechnicalEvaluation:
            {
                var vm = new TechnicalEvaluationFeature.TechnicalEvaluationWorkspaceViewModel(shell, appHost);
                return (new TechnicalEvaluationFeature.TechnicalEvaluationWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementAwards:
            {
                var vm = new AwardsFeature.AwardWorkspaceViewModel(shell, appHost);
                return (new AwardsFeature.AwardWorkspaceView { DataContext = vm }, vm);
            }
            case ShellRoute.ProcurementPurchaseOrders:
            {
                var vm = new QubiCostElectrical.Wpf.Features.PurchaseOrders.PurchaseOrderWorkspaceViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.PurchaseOrders.PurchaseOrderWorkspaceView(vm), vm);
            }
            case ShellRoute.ProcurementGoodsReceipts:
            {
                var vm = new QubiCostElectrical.Wpf.Features.GoodsReceipts.GoodsReceiptWorkspaceViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.GoodsReceipts.GoodsReceiptWorkspaceView(vm), vm);
            }
            case ShellRoute.ProcurementDashboard:
            {
                var vm = new QubiCostElectrical.Wpf.Features.ProcurementDashboard.ProcurementDashboardViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.ProcurementDashboard.ProcurementDashboardView(vm), vm);
            }
            case ShellRoute.ContractWorkspace:
            {
                var vm = new QubiCostElectrical.Wpf.Features.Contracts.ContractWorkspaceViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.Contracts.ContractWorkspaceView(vm), vm);
            }
            case ShellRoute.ContractVersions:
            {
                // Navigate with a dummy or active contract ID
                var contractId = appHost.Session.CurrentProjectPath != null ? ContractId.New() : ContractId.New();
                var vm = new QubiCostElectrical.Wpf.Features.Contracts.ContractVersionsViewModel(shell, appHost, contractId);
                return (new QubiCostElectrical.Wpf.Features.Contracts.ContractVersionsView(vm), vm);
            }
            case ShellRoute.EstadoPago:
            {
                var vm = new QubiCostElectrical.Wpf.Features.EstadoPago.EstadoPagoViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.EstadoPago.EstadoPagoView(vm), vm);
            }
            case ShellRoute.PaymentCertificateWorkspace:
            {
                var vm = new QubiCostElectrical.Wpf.Features.PaymentCertificates.PaymentCertificateWorkspaceViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.PaymentCertificates.PaymentCertificateWorkspaceView(vm), vm);
            }
            case ShellRoute.PaymentCertificateEditor:
            {
                var cert = new PaymentCertificate();
                var vm = new QubiCostElectrical.Wpf.Features.PaymentCertificates.PaymentCertificateEditorViewModel(shell, appHost, cert);
                return (new QubiCostElectrical.Wpf.Features.PaymentCertificates.PaymentCertificateEditorView(vm), vm);
            }
            case ShellRoute.ValuationReviewWorkspace:
            {
                var vm = new QubiCostElectrical.Wpf.Features.PaymentCertificates.ValuationReviewWorkspaceViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.PaymentCertificates.ValuationReviewWorkspaceView(vm), vm);
            }
            case ShellRoute.AdvanceRetentionWorkspace:
            {
                var vm = new QubiCostElectrical.Wpf.Features.PaymentCertificates.AdvanceRetentionWorkspaceViewModel(shell, appHost);
                return (new QubiCostElectrical.Wpf.Features.PaymentCertificates.AdvanceRetentionWorkspaceView(vm), vm);
            }
            case ShellRoute.ChangeRequestWorkspace:
            {
                var vm = new ChangeRequestWorkspaceViewModel(shell, appHost);
                return (new ChangeRequestWorkspaceView(vm), vm);
            }
            case ShellRoute.ExtraWorkWorkspace:
            {
                var vm = new ExtraWorkWorkspaceViewModel(shell, appHost);
                return (new ExtraWorkWorkspaceView(vm), vm);
            }
            case ShellRoute.ClaimWorkspace:
            {
                var vm = new ClaimWorkspaceViewModel(shell, appHost);
                return (new ClaimWorkspaceView(vm), vm);
            }
            case ShellRoute.AdjustmentWorkspace:
            {
                var vm = new AdjustmentWorkspaceViewModel(shell, appHost);
                return (new AdjustmentWorkspaceView(vm), vm);
            }
            case ShellRoute.GuaranteeWorkspace:
            {
                var vm = new GuaranteeWorkspaceViewModel(shell, appHost);
                return (new GuaranteeWorkspaceView(vm), vm);
            }
            case ShellRoute.PenaltyWorkspace:
            {
                var vm = new PenaltyWorkspaceViewModel(shell, appHost);
                return (new PenaltyWorkspaceView(vm), vm);
            }
            case ShellRoute.InvoiceWorkspace:
            {
                var vm = new InvoiceWorkspaceViewModel(shell, appHost);
                return (new InvoiceWorkspaceView(vm), vm);
            }
            case ShellRoute.ContractDashboard:
            {
                var vm = new QubiCostElectrical.Wpf.Features.ContractDashboard.ContractDashboardViewModel(shell, appHost);
                var view = new QubiCostElectrical.Wpf.Features.ContractDashboard.ContractDashboardView(vm);
                return (view, vm);
            }
            case ShellRoute.CloseoutWorkspace:
            {
                var vm = new CloseoutWorkspaceViewModel(shell, appHost);
                return (new CloseoutWorkspaceView(vm), vm);
            }
            case ShellRoute.CloseoutReception:
            {
                var vm = new CloseoutReceptionViewModel(shell, appHost);
                return (new CloseoutReceptionView(vm), vm);
            }
            case ShellRoute.CloseoutHandover:
            {
                var vm = new CloseoutHandoverViewModel(shell, appHost);
                return (new CloseoutHandoverView(vm), vm);
            }
            case ShellRoute.CloseoutLessons:
            {
                var vm = new CloseoutLessonsViewModel(shell, appHost);
                return (new CloseoutLessonsView(vm), vm);
            }
            case ShellRoute.CloseoutFinalDashboard:
            {
                var vm = new CloseoutFinalDashboardViewModel(shell, appHost);
                return (new CloseoutFinalDashboardView(vm), vm);
            }
            case ShellRoute.CloseoutArchive:
            {
                var vm = new CloseoutArchiveViewModel(shell, appHost);
                return (new CloseoutArchiveView(vm), vm);
            }
            default:
                throw new InvalidOperationException($"La ruta {route} no tiene vista registrada.");
        }
    }

    private Task NavigateAsync(ShellRoute route)
    {
        Navigate(route);
        return Task.CompletedTask;
    }
}
