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
    public IBudgetKpiService BudgetKpi => _sp.GetRequiredService<IBudgetKpiService>();
    public ICurrencyConversionService CurrencyConversion => _sp.GetRequiredService<ICurrencyConversionService>();
    public ICubicacionViewModelService CubicacionViewModel => _sp.GetRequiredService<ICubicacionViewModelService>();
    public IProyectoViewModelService ProyectoViewModel => _sp.GetRequiredService<IProyectoViewModelService>();
    public IApuViewModelService ApuViewModel => _sp.GetRequiredService<IApuViewModelService>();
    public IApuService Apu => _sp.GetRequiredService<IApuService>();
    public IPurchaseListService PurchaseList => _sp.GetRequiredService<IPurchaseListService>();
    public ITemplateService Template => _sp.GetRequiredService<ITemplateService>();
    public IValidationService Validation => _sp.GetRequiredService<IValidationService>();
    public ICubicacionService Cubicacion => _sp.GetRequiredService<ICubicacionService>();
    public ICubicacionTakeoffQueryService CubicacionTakeoffQuery => _sp.GetRequiredService<ICubicacionTakeoffQueryService>();
    public IItemizadoService Itemizado => _sp.GetRequiredService<IItemizadoService>();
    public IItemizadoSpreadsheetReader ItemizadoSpreadsheet => _sp.GetRequiredService<IItemizadoSpreadsheetReader>();
    public IMaterialSpreadsheetImporter MaterialSpreadsheet => _sp.GetRequiredService<IMaterialSpreadsheetImporter>();
    public ITemplateSpreadsheetService TemplateSpreadsheet => _sp.GetRequiredService<ITemplateSpreadsheetService>();
    public IPurchaseListExcelExporter PurchaseListExcel => _sp.GetRequiredService<IPurchaseListExcelExporter>();
    public ICubicacionSCExcelExporter CubicacionSCExcel => _sp.GetRequiredService<ICubicacionSCExcelExporter>();
    public ImportarItemizadoUseCase ImportItemizado => _sp.GetRequiredService<ImportarItemizadoUseCase>();
    public PegarItemizadoUseCase PasteItemizado => _sp.GetRequiredService<PegarItemizadoUseCase>();
    public IProjectRepository ProjectRepository => _sp.GetRequiredService<IProjectRepository>();
    public IPartidaCompletionService PartidaCompletion => _sp.GetRequiredService<IPartidaCompletionService>();
    public IFinalReviewService FinalReview => _sp.GetRequiredService<IFinalReviewService>();
    public IPriceTraceabilityService PriceTraceability => _sp.GetRequiredService<IPriceTraceabilityService>();
    public IExcelProjectExporter ExcelExporter => _sp.GetRequiredService<IExcelProjectExporter>();
    public IPdfProjectExporter PdfExporter => _sp.GetRequiredService<IPdfProjectExporter>();
    public IPresentationLetterDocxExporter WordLetterExporter => _sp.GetRequiredService<IPresentationLetterDocxExporter>();
    public ICubicacionReportExcelExporter CubicacionReportExcelExporter => _sp.GetRequiredService<ICubicacionReportExcelExporter>();
    public ICubicacionReportPdfExporter CubicacionReportPdfExporter => _sp.GetRequiredService<ICubicacionReportPdfExporter>();
    public ICubicacionSCReportExcelExporter CubicacionSCReportExcelExporter => _sp.GetRequiredService<ICubicacionSCReportExcelExporter>();
    public BudgetVarianceService BudgetVariance => _sp.GetRequiredService<BudgetVarianceService>();
    public IVersionComparisonService VersionComparison => _sp.GetRequiredService<IVersionComparisonService>();
    public PriceBankApplicationService PriceBank => _sp.GetRequiredService<PriceBankApplicationService>();
    public IPriceBankStore PriceBankStore => _sp.GetRequiredService<IPriceBankStore>();

    public IApuCatalogRepository ApuCatalogRepository => _sp.GetRequiredService<IApuCatalogRepository>();
    public IApuCatalogService ApuCatalog => _sp.GetRequiredService<IApuCatalogService>();
    public IApuVersionRepository ApuVersionRepository => _sp.GetRequiredService<IApuVersionRepository>();
    public IProjectBenchmarkRepository BenchmarkRepository => _sp.GetRequiredService<IProjectBenchmarkRepository>();
    public IBenchmarkService Benchmark => _sp.GetRequiredService<IBenchmarkService>();
    public IEconomicCalculationService EconomicCalculation => _sp.GetRequiredService<IEconomicCalculationService>();

    public IProjectDbContext ProjectDb => _sp.GetRequiredService<IProjectDbContext>();

    // Contratos: BD del proyecto abierto. null cuando no hay proyecto.
    public IContractService ContractService => _sp.GetRequiredService<IProjectDbContext>().Contracts!;

    public static AppServices CreateDefault() => new AppServicesBuilder().Build();
}
