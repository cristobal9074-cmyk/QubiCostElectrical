using System;
using System.Linq;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Features.Prices;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using ApuFeature = QubiCostElectrical.Wpf.Features.Apu;
using CubicacionFeature = QubiCostElectrical.Wpf.Features.Cubicacion;
using ContractsFeature = QubiCostElectrical.Wpf.Features.Contracts;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Orquesta navegación: leave → cache hit/miss → factory → context.
/// El shell sigue dueño de chrome/comandos.
/// </summary>
internal sealed class ShellNavigationCoordinator
{
    private readonly ShellPageCache pageCache;
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly AutoSaveService autoSave;
    private readonly Func<bool> tryApplyCurrentPageChanges;
    private readonly Func<bool> isShellReadOnly;
    private readonly Action refreshContext;

    public ShellNavigationCoordinator(
        ShellPageCache pageCache,
        BudgetProjectSession session,
        IProjectWorkspaceService projectWorkspace,
        AutoSaveService autoSave,
        Func<bool> tryApplyCurrentPageChanges,
        Func<bool> isShellReadOnly,
        Action refreshContext)
    {
        this.pageCache = pageCache;
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.autoSave = autoSave;
        this.tryApplyCurrentPageChanges = tryApplyCurrentPageChanges;
        this.isShellReadOnly = isShellReadOnly;
        this.refreshContext = refreshContext;
    }

    public ShellPageCache Cache => pageCache;

    public void ClearCacheKeepingSurface(Action clearCurrentSurface)
    {
        pageCache.Clear();
        clearCurrentSurface();
    }

    public void SeedForTests(ShellRoute route, object? viewModel) => pageCache.Seed(route, viewModel);

    public bool TryActivateSeededForTests(
        ShellRoute route,
        out UserControl view,
        out object? viewModel)
    {
        if (!pageCache.TryGet(route, out view, out viewModel))
            return false;
        SetSessionListening(viewModel, true);
        return true;
    }

    public bool RequestNavigate(
        ref ShellRoute currentRoute,
        ref UserControl? currentView,
        ref object? currentPageVm,
        IShellNavigationHost shell,
        ShellRoute route,
        WorkspaceNavigationContext? context,
        bool force)
    {
        if (!force && !TryLeaveCurrentPage(currentPageVm))
            return false;

        if (context is TakeoffNavigationContext takeoff)
            session.PendingTakeoffNavigation = takeoff;

        if (route == currentRoute && currentView != null)
        {
            ApplyNavigationContext(context, currentView);
            if (currentPageVm is IRefreshablePage refreshableOnSameRoute)
                refreshableOnSameRoute.Refresh();
            return true;
        }

        SetSessionListening(currentPageVm, false);

        if (pageCache.TryGet(route, out var cachedView, out var cachedVm))
        {
            currentRoute = route;
            currentView = cachedView;
            currentPageVm = cachedVm;
            SetSessionListening(currentPageVm, true);
            // SessionAware/Cub ya flushean pending al reactivar — no Refresh() doble.
            if (currentPageVm is not ISessionChangeAware && currentPageVm is IRefreshablePage refreshable)
                refreshable.Refresh();
            refreshContext();
            ApplyNavigationContext(context, cachedView);
            return true;
        }

        currentRoute = route;
        var (view, vm) = ShellPageFactory.Create(route,
            WpfAppHost.Provider ?? throw new InvalidOperationException("Contenedor DI no inicializado."),
            shell);
        pageCache.Set(route, view, vm);
        currentView = view;
        currentPageVm = vm;
        SetSessionListening(currentPageVm, true);
        refreshContext();
        ApplyNavigationContext(context, view);
        return true;
    }

    public bool TryLeaveCurrentPage(object? currentPageVm)
    {
        if (!tryApplyCurrentPageChanges())
        {
            WpfDialogService.Warning(
                "Cambios pendientes",
                "No se pudieron aplicar los cambios de esta pantalla. Corrija el error antes de salir.");
            return false;
        }

        if (!projectWorkspace.HasOpenProject)
            return true;

        if (isShellReadOnly() || session.IsReadOnly)
            return true;

        if (!session.IsDirty)
            return true;

        autoSave.RequestPersistSoon();
        return true;
    }

    private static void SetSessionListening(object? pageVm, bool active)
    {
        if (pageVm is ISessionChangeAware aware)
            aware.SetSessionChangeActive(active);
    }

    internal static void ApplyNavigationContext(WorkspaceNavigationContext? context, UserControl view)
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
            case ContractsFeature.ContractEditorNavContext contractEditorCtx when view is ContractsFeature.ContractEditorView contractEditorView:
                contractEditorView.ApplyNavContext(contractEditorCtx);
                break;
            case ContractsFeature.ContractVersionsNavContext contractVersionsCtx when view is ContractsFeature.ContractVersionsView contractVersionsView:
                contractVersionsView.ApplyNavContext(contractVersionsCtx);
                break;
        }
    }
}
