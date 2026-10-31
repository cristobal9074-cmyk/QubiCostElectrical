using Microsoft.Extensions.DependencyInjection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.EstadosPago;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf;

/// <summary>
/// Registro DI de la capa de presentación WPF: servicios de diálogo/archivo, undo, y la fachada
/// <see cref="WpfAppHost"/> (transitoria, hasta que los ViewModels se inyecten por constructor).
/// </summary>
public static class WpfServiceCollectionExtensions
{
    public static IServiceCollection AddWpfServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IFileOpenService, WpfFileOpenService>();
        services.AddSingleton<WpfUndoService>();
        services.AddSingleton<EstadosPagoExportCoordinator>();
        services.AddSingleton<EstadosPagoWorkflowCoordinator>();
        services.AddViewModels();

        // Fachada histórica: agrega presenters/workspaces/auth/lock/audit resueltos del contenedor.
        services.AddSingleton<WpfAppHost>(sp => WpfAppHost.Create(
            sp.GetRequiredService<BudgetProjectSession>(),
            new AppServices(sp),
            sp.GetRequiredService<IProjectWorkspaceService>(),
            sp.GetRequiredService<IDocumentsWorkspaceService>(),
            sp.GetRequiredService<IProjectEditWorkspaceService>(),
            sp.GetRequiredService<DashboardPresenter>(),
            sp.GetRequiredService<ItemizadoPresenter>(),
            sp.GetRequiredService<CubicacionPresenter>(),
            sp.GetRequiredService<ExportPresenter>(),
            sp.GetRequiredService<IItemizadoWorkspaceService>(),
            sp.GetRequiredService<IFileDialogService>(),
            sp.GetRequiredService<IFileOpenService>(),
            sp.GetRequiredService<IAuthService>(),
            sp.GetRequiredService<ProjectCollaborationLockService>(),
            sp.GetRequiredService<ProjectAuditRecorder>(),
            sp.GetRequiredService<IPermissionService>(),
            sp.GetRequiredService<WpfUndoService>()));

        return services;
    }
}
