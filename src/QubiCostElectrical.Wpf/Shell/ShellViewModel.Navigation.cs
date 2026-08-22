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
using ApuCatalogFeature = QubiCostElectrical.Wpf.Features.ApuCatalog;
using ApuMergeFeature = QubiCostElectrical.Wpf.Features.ApuMerge;
using EconomicEditorFeature = QubiCostElectrical.Wpf.Features.EconomicEditor;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Features.Project;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using ExportFeature = QubiCostElectrical.Wpf.Features.Export;
using ReviewFeature = QubiCostElectrical.Wpf.Features.Review;
using VersionsFeature = QubiCostElectrical.Wpf.Features.Versions;
using QubiCostElectrical.Wpf.Features.Preferences;
using QubiCostElectrical.Wpf.Features.ChangeOrders;

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
            case ShellRoute.EconomicEditor:
            {
                var vm = new EconomicEditorFeature.EconomicEditorViewModel(shell, appHost);
                return (new EconomicEditorFeature.EconomicEditorView { DataContext = vm }, vm);
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
