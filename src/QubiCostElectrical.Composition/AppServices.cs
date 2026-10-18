using System;
using Microsoft.Extensions.DependencyInjection;

namespace QubiCostElectrical;

/// <summary>
/// Fachada histórica de servicios. Durante la migración a DI es un envoltorio fino sobre el
/// <see cref="IServiceProvider"/>: cada propiedad resuelve el servicio del contenedor. Los
/// consumidores (`host.Services.X`) siguen funcionando sin cambios hasta que se migren a
/// inyección por constructor. Se eliminará cuando no queden usos.
/// </summary>
public sealed class AppServices
{
    private readonly IServiceProvider _sp;

    public AppServices(IServiceProvider serviceProvider)
        => _sp = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public IServiceProvider ServiceProvider => _sp;

    public IBudgetCalculationService BudgetCalculation => _sp.GetRequiredService<IBudgetCalculationService>();
    public IMaterialSearchService MaterialSearch => _sp.GetRequiredService<IMaterialSearchService>();
    public ICurrencyConversionService CurrencyConversion => _sp.GetRequiredService<ICurrencyConversionService>();
    public IApuService Apu => _sp.GetRequiredService<IApuService>();
    public IPurchaseListService PurchaseList => _sp.GetRequiredService<IPurchaseListService>();
    public IMaterialControlService MaterialControl => _sp.GetRequiredService<IMaterialControlService>();
    public IValidationService Validation => _sp.GetRequiredService<IValidationService>();
    public ICubicacionService Cubicacion => _sp.GetRequiredService<ICubicacionService>();
    public ICubicacionTakeoffQueryService CubicacionTakeoffQuery => _sp.GetRequiredService<ICubicacionTakeoffQueryService>();
    public IItemizadoService Itemizado => _sp.GetRequiredService<IItemizadoService>();
    public IItemizadoSpreadsheetReader ItemizadoSpreadsheet => _sp.GetRequiredService<IItemizadoSpreadsheetReader>();
    public IPurchaseListExcelExporter PurchaseListExcel => _sp.GetRequiredService<IPurchaseListExcelExporter>();
    public ImportarItemizadoUseCase ImportItemizado => _sp.GetRequiredService<ImportarItemizadoUseCase>();
    public PegarItemizadoUseCase PasteItemizado => _sp.GetRequiredService<PegarItemizadoUseCase>();
    public IProjectRepository ProjectRepository => _sp.GetRequiredService<IProjectRepository>();
    public IPartidaCompletionService PartidaCompletion => _sp.GetRequiredService<IPartidaCompletionService>();
    public IFinalReviewService FinalReview => _sp.GetRequiredService<IFinalReviewService>();
    public IPriceTraceabilityService PriceTraceability => _sp.GetRequiredService<IPriceTraceabilityService>();
    public IExcelProjectExporter ExcelExporter => _sp.GetRequiredService<IExcelProjectExporter>();
    public IPdfProjectExporter PdfExporter => _sp.GetRequiredService<IPdfProjectExporter>();
    public IOfferLetterPdfExporter OfferLetterPdfExporter => _sp.GetRequiredService<IOfferLetterPdfExporter>();
    public IIngelsamOfferSummaryExcelExporter IngelsamOfferSummaryExcelExporter => _sp.GetRequiredService<IIngelsamOfferSummaryExcelExporter>();
    public IPartidasAbiertasExcelExporter PartidasAbiertasExcelExporter => _sp.GetRequiredService<IPartidasAbiertasExcelExporter>();
    public IOfferSummaryPdfExporter OfferSummaryPdfExporter => _sp.GetRequiredService<IOfferSummaryPdfExporter>();
    public IPresentationLetterDocxExporter WordLetterExporter => _sp.GetRequiredService<IPresentationLetterDocxExporter>();
    public ICubicacionReportExcelExporter CubicacionReportExcelExporter => _sp.GetRequiredService<ICubicacionReportExcelExporter>();
    public ICubicacionReportPdfExporter CubicacionReportPdfExporter => _sp.GetRequiredService<ICubicacionReportPdfExporter>();
    public ICubicacionSCReportExcelExporter CubicacionSCReportExcelExporter => _sp.GetRequiredService<ICubicacionSCReportExcelExporter>();
    public IBudgetRevisionDiffPdfExporter RevisionDiffPdfExporter => _sp.GetRequiredService<IBudgetRevisionDiffPdfExporter>();
    public IBudgetRevisionDiffExcelExporter RevisionDiffExcelExporter => _sp.GetRequiredService<IBudgetRevisionDiffExcelExporter>();
    public IVersionComparisonService VersionComparison => _sp.GetRequiredService<IVersionComparisonService>();
    public PriceBankApplicationService PriceBank => _sp.GetRequiredService<PriceBankApplicationService>();
    public IPriceBankStore PriceBankStore => _sp.GetRequiredService<IPriceBankStore>();
    public IUserPreferencesStore UserPreferencesStore => _sp.GetRequiredService<IUserPreferencesStore>();

    public IApuCatalogRepository ApuCatalogRepository => _sp.GetRequiredService<IApuCatalogRepository>();
    public IApuVersionRepository ApuVersionRepository => _sp.GetRequiredService<IApuVersionRepository>();
    public IApuTemplateStore ApuTemplateStore => _sp.GetRequiredService<IApuTemplateStore>();
    public IConjuntoTemplateStore ConjuntoTemplateStore => _sp.GetRequiredService<IConjuntoTemplateStore>();
    public IEconomicCalculationService EconomicCalculation => _sp.GetRequiredService<IEconomicCalculationService>();

    public IProjectDbContext ProjectDb => _sp.GetRequiredService<IProjectDbContext>();

    public static AppServices CreateDefault() => new AppServicesBuilder().Build();
}
