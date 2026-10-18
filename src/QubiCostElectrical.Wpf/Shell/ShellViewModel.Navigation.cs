using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

partial class ShellViewModel
{
    private ShellNavigationCoordinator navigation = null!;
    private readonly ShellNavigationHistory navigationHistory = new();
    private ShellLifecyclePhase currentPhase = ShellLifecyclePhase.Presupuesto;

    public ShellLifecyclePhase CurrentPhase
    {
        get => currentPhase;
        set
        {
            if (SetProperty(ref currentPhase, value))
            {
                OnPropertyChanged(nameof(IsPresupuestoPhase));
                OnPropertyChanged(nameof(IsPlanificacionPhase));
                OnPropertyChanged(nameof(IsEjecucionPhase));
            }
        }
    }

    public bool IsPresupuestoPhase => CurrentPhase == ShellLifecyclePhase.Presupuesto;
    public bool IsPlanificacionPhase => CurrentPhase == ShellLifecyclePhase.Planificacion;
    public bool IsEjecucionPhase => CurrentPhase == ShellLifecyclePhase.Ejecucion;

    [RelayCommand]
    public void SelectPhase(ShellLifecyclePhase phase)
    {
        CurrentPhase = phase;
        var phaseOfCurrent = ShellRouteNavigation.GetLifecyclePhase(CurrentRoute, phase);
        if (CurrentRoute != ShellRoute.Dashboard && phaseOfCurrent != phase)
        {
            var targetRoute = phase switch
            {
                ShellLifecyclePhase.Planificacion => ShellRoute.Purchases,
                ShellLifecyclePhase.Ejecucion => ShellRoute.MaterialControl,
                _ => ShellRoute.Takeoff
            };
            Navigate(targetRoute);
        }
    }

    public bool CanNavigateBack => navigationHistory.CanNavigateBack;
    public bool CanNavigateForward => navigationHistory.CanNavigateForward;

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    public void NavigateBack()
    {
        if (navigationHistory.TryNavigateBack(CurrentRoute, out var targetRoute))
        {
            RequestNavigate(targetRoute, force: false);
            UpdateNavigationHistoryCommands();
        }
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    public void NavigateForward()
    {
        if (navigationHistory.TryNavigateForward(CurrentRoute, out var targetRoute))
        {
            RequestNavigate(targetRoute, force: false);
            UpdateNavigationHistoryCommands();
        }
    }

    private void UpdateNavigationHistoryCommands()
    {
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
        NavigateBackCommand.NotifyCanExecuteChanged();
        NavigateForwardCommand.NotifyCanExecuteChanged();
    }

    public void ClearPageCache()
    {
        navigationHistory.Clear();
        UpdateNavigationHistoryCommands();
        navigation.ClearCacheKeepingSurface(() =>
        {
            currentView = null;
            currentPageVm = null;
        });
    }

    /// <summary>
    /// Solo pruebas: registra una página en caché con un <see cref="UserControl"/> vacío
    /// (sin XAML/tema) para ejercitar <see cref="RequestNavigate"/> y
    /// <see cref="TryLeaveCurrentPage"/> sin construir vistas reales.
    /// </summary>
    internal void SeedCachedPageForTests(ShellRoute route, object? viewModel)
        => navigation.SeedForTests(route, viewModel);

    /// <summary>
    /// Solo pruebas: activa una página previamente sembrada sin pasar por leave/create.
    /// </summary>
    internal void ActivateSeededPageForTests(ShellRoute route)
    {
        if (!navigation.TryActivateSeededForTests(route, out var view, out var viewModel))
            throw new InvalidOperationException($"No hay página sembrada para {route}.");

        CurrentRoute = route;
        CurrentView = view;
        currentPageVm = viewModel;
    }

    public bool RequestNavigate(ShellRoute route, bool force = false)
        => RequestNavigate(route, null, force);

    public bool RequestNavigate(ShellRoute route, WorkspaceNavigationContext? context, bool force = false)
    {
        if (!force && !NavVisibilityPolicy.IsNavigable(route))
        {
            ShellNotificationService.Instance.ShowInfo(
                "Ciclo de obra",
                "Esta pantalla forma parte del ciclo de obra (próximamente). Volviendo al resumen.");
            if (currentRoute == ShellRoute.Dashboard)
                return true;
            return RequestNavigate(ShellRoute.Dashboard, null, force: false);
        }

        var previousRoute = currentRoute;
        var routeState = currentRoute;
        var viewState = currentView;
        var vmState = currentPageVm;
        var ok = navigation.RequestNavigate(
            ref routeState,
            ref viewState,
            ref vmState,
            this,
            route,
            context,
            force);
        if (!ok)
            return false;

        navigationHistory.RecordNavigation(routeState, previousRoute);
        CurrentRoute = routeState;
        CurrentView = viewState;
        currentPageVm = vmState;
        CurrentPhase = ShellRouteNavigation.GetLifecyclePhase(routeState, CurrentPhase);
        UpdateNavigationHistoryCommands();
        return true;
    }

    /// <summary>
    /// Al cambiar de pestaña no se pregunta. Se aplica el draft de la página a la sesión
    /// y se dispara guardado silencioso del .qce (flujo tipo Excel/Bluebeam).
    /// El diálogo Guardar/Descartar queda solo para cerrar/abrir otro proyecto.
    /// </summary>
    internal bool TryLeaveCurrentPage()
        => navigation.TryLeaveCurrentPage(currentPageVm);

    private async Task PersistProjectSilentlyAsync(string reason)
    {
        // Mantenido por compatibilidad / cierres explícitos; leave-page usa RequestPersistSoon.
        if (!projectWorkspace.HasOpenProject || session.IsReadOnly || !session.IsDirty)
            return;

        try
        {
            var user = CollaborationSessionBridge.CurrentUserContext();
            session.ProjectInfo.UsuarioNombre = string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.Email
                : user.DisplayName;
            session.ProjectInfo.UsuarioRol = permissions.RoleDisplayName;

            await projectWorkspace.SaveAsync().ConfigureAwait(true);
            AutoSaveService.DeleteAutoSave(session.CurrentProjectPath);
            autoSave.MarkSaved();
            RefreshChrome();
            AppLog.Info("ProjectWorkspace", $"Guardado silencioso ({reason}) OK.");
        }
        catch (Exception ex)
        {
            AppLog.Error("ProjectWorkspace", $"Falló el guardado silencioso ({reason})", ex);
            ShellNotificationService.Instance.ShowWarning(
                "Sin guardar en disco",
                OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private Task NavigateAsync(ShellRoute route)
    {
        Navigate(route);
        return Task.CompletedTask;
    }
}
