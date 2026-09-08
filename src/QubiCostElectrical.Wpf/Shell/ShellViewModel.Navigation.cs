using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

partial class ShellViewModel
{
    private ShellNavigationCoordinator navigation = null!;

    public void ClearPageCache()
        => navigation.ClearCacheKeepingSurface(() =>
        {
            currentView = null;
            currentPageVm = null;
        });

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

        CurrentRoute = routeState;
        CurrentView = viewState;
        currentPageVm = vmState;
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
