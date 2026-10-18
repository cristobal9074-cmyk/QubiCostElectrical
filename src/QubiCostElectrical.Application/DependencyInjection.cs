using Microsoft.Extensions.DependencyInjection;

namespace QubiCostElectrical;

/// <summary>
/// Registro DI de la capa de aplicación: servicios de caso de uso, presenters y la sesión del
/// proyecto abierto. Todo es singleton (una app de escritorio = una instancia); el estado por
/// proyecto vive en <see cref="BudgetProjectSession"/>, que se rellena in-place.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Estado del proyecto abierto + colaboración.
        services.AddSingleton<BudgetProjectSession>();
        services.AddSingleton<ProjectCollaborationLockService>();
        services.AddSingleton<ProjectAuditRecorder>();
        services.AddSingleton<IPermissionService, UserRolePermissionService>();

        // Servicios de cálculo / consulta / edición.
        services.AddSingleton<IBudgetCalculationService, BudgetCalculationService>();
        services.AddSingleton<IMaterialSearchService, MaterialSearchService>();
        services.AddSingleton<ICurrencyConversionService, CurrencyConversionService>();
        services.AddSingleton<IApuService, ApuService>();
        services.AddSingleton<IPurchaseListService, PurchaseListService>();
        services.AddSingleton<IMaterialControlService, MaterialControlService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<ICubicacionService, CubicacionService>();
        services.AddSingleton<ICubicacionTakeoffQueryService, CubicacionTakeoffQueryService>();
        services.AddSingleton<IItemizadoService, ItemizadoService>();
        services.AddSingleton<IPartidaCompletionService, PartidaCompletionService>();
        services.AddSingleton<IFinalReviewService, FinalReviewService>();
        services.AddSingleton<IPriceTraceabilityService, PriceTraceabilityService>();
        services.AddSingleton<IVersionComparisonService, BudgetVersionComparisonService>();
        services.AddSingleton<IEconomicCalculationService, EconomicCalculationService>();
        services.AddSingleton<DashboardExecutiveService>();
        services.AddSingleton<CubicacionCompositionService>();
        services.AddSingleton<PriceBankApplicationService>();

        // Use cases.
        services.AddSingleton<ImportarItemizadoUseCase>();
        services.AddSingleton<PegarItemizadoUseCase>();

        // Workspaces (dependen de la sesión + repos).
        services.AddSingleton<IProjectWorkspaceService, ProjectWorkspaceService>();
        services.AddSingleton<IProjectEditWorkspaceService, ProjectEditWorkspaceService>();
        services.AddSingleton<IDocumentsWorkspaceService, DocumentsWorkspaceService>();
        services.AddSingleton<IItemizadoWorkspaceService, ItemizadoWorkspaceService>();

        // Presenters + sus bundles (records; MEDI resuelve el ctor por posición).
        services.AddSingleton<DashboardPresenterServices>();
        services.AddSingleton<CubicacionPresenterServices>();
        services.AddSingleton<ExportPresenterServices>();
        services.AddSingleton<ItemizadoPresenterServices>();
        services.AddSingleton<DashboardPresenter>();
        services.AddSingleton<CubicacionPresenter>();
        services.AddSingleton<ExportPresenter>();
        services.AddSingleton<ItemizadoPresenter>();

        return services;
    }
}
