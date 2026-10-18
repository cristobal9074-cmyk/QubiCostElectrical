using System;
using Microsoft.Extensions.DependencyInjection;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Ruta de la BD del banco de precios (compartida: banco + catálogo APU + benchmark). Se
/// resuelve una vez en arranque desde las preferencias del usuario.
/// </summary>
public sealed class PriceBankPath
{
    public string Value { get; }
    public PriceBankPath(string value) => Value = value;
}

/// <summary>
/// Registro DI de la capa de infraestructura: repos SQLite, lectores/exportadores de planillas,
/// PDF y Word, y autenticación. Requiere que <c>PriceBankPath</c> ya esté registrado.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        string Path(IServiceProvider sp) => sp.GetRequiredService<PriceBankPath>().Value;

        // Preferencias de usuario locales (JSON).
        services.AddSingleton<IUserPreferencesStore, UserPreferencesStore>();

        // Repositorio de proyecto (SQLite/JSON, pools estáticos internos).
        services.AddSingleton<IProjectRepository, ProjectRepository>();

        // Banco de precios (BD compartida).
        services.AddSingleton<IPriceBankStore>(sp => new PriceBankStore(Path(sp)));
        services.AddSingleton<IPriceBankRepository, PriceBankRepository>();

        // Catálogo APU: vive en la BD del banco de precios (catálogo compartido).
        services.AddSingleton<IApuCatalogRepository>(sp => new SqliteApuCatalogRepository(Path(sp)));
        services.AddSingleton<IApuVersionRepository>(sp => new SqliteApuVersionRepository(Path(sp)));
        services.AddSingleton<IApuTemplateStore>(sp => new ApuTemplateStore(Path(sp)));
        services.AddSingleton<IConjuntoTemplateStore>(sp => new ConjuntoTemplateStore(Path(sp)));

        // Planillas.
        services.AddSingleton<IItemizadoSpreadsheetReader, ItemizadoSpreadsheetReader>();
        services.AddSingleton<IPurchaseListExcelExporter, PurchaseListExcelExporter>();

        // Exportadores pesados (QuestPDF / ClosedXML / OpenXml). Registro normal = construcción
        // perezosa: nadie los instancia hasta el primer GetRequiredService.
        services.AddSingleton<IExcelProjectExporter, ExcelExportInfrastructure>();
        services.AddSingleton<IPdfProjectExporter, QuestPdfProjectExporter>();
        services.AddSingleton<IOfferLetterPdfExporter, QuestPdfOfferLetterExporter>();
        services.AddSingleton<IIngelsamOfferSummaryExcelExporter, IngelsamOfferSummaryExcelExporter>();
        services.AddSingleton<IPartidasAbiertasExcelExporter, PartidasAbiertasExcelExporter>();
        services.AddSingleton<IOfferSummaryPdfExporter, QuestPdfOfferSummaryExporter>();
        services.AddSingleton<IPresentationLetterDocxExporter, PresentationLetterDocxExporter>();
        services.AddSingleton<ICubicacionReportExcelExporter, CubicacionReportExcelExporter>();
        services.AddSingleton<ICubicacionReportPdfExporter, QuestPdfCubicacionReportExporter>();
        services.AddSingleton<ICubicacionSCReportExcelExporter, CubicacionSCTechnicalReportExcelExporter>();
        services.AddSingleton<IBudgetRevisionDiffPdfExporter, BudgetRevisionDiffPdfExporter>();
        services.AddSingleton<IBudgetRevisionDiffExcelExporter, BudgetRevisionDiffExcelExporter>();
        services.AddSingleton<IEstadoPagoExcelExporter, EstadoPagoExcelExporter>();
        services.AddSingleton<IEstadoPagoPdfExporter, EstadoPagoPdfExporter>();

        // Servicios ligados a la BD del proyecto abierto (contratos). Reemplaza el
        // RewireContractsIfProjectChanged manual de WpfAppHostBuilder.
        services.AddSingleton<IProjectDbContext, ProjectDbContext>();

        // Autenticación (abre su propia BD SQLite de credenciales).
        services.AddSingleton<AuthService>();
        services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());

        return services;
    }
}
