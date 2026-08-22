using System;
using System.Threading;

namespace QubiCostElectrical;

public sealed class AppServices
{
    public IBudgetCalculationService BudgetCalculation { get; }
    public IMaterialSearchService MaterialSearch { get; }
    public IBudgetKpiService BudgetKpi { get; }
    public ICurrencyConversionService CurrencyConversion { get; }
    public ICubicacionViewModelService CubicacionViewModel { get; }
    public IProyectoViewModelService ProyectoViewModel { get; }
    public IApuViewModelService ApuViewModel { get; }
    public IApuService Apu { get; }
    public IPurchaseListService PurchaseList { get; }
    public ITemplateService Template { get; }
    public IValidationService Validation { get; }
    public ICubicacionService Cubicacion { get; }
    public ICubicacionTakeoffQueryService CubicacionTakeoffQuery { get; }
    public IItemizadoService Itemizado { get; }
    public IItemizadoSpreadsheetReader ItemizadoSpreadsheet { get; } = null!;
    public IMaterialSpreadsheetImporter MaterialSpreadsheet { get; } = null!;
    public ITemplateSpreadsheetService TemplateSpreadsheet { get; } = null!;
    public IPurchaseListExcelExporter PurchaseListExcel { get; } = null!;
    public ICubicacionSCExcelExporter CubicacionSCExcel { get; } = null!;
    public ImportarItemizadoUseCase ImportItemizado { get; } = null!;
    public PegarItemizadoUseCase PasteItemizado { get; } = null!;
    public IProjectRepository ProjectRepository { get; }
    public IPartidaCompletionService PartidaCompletion { get; }
    public IFinalReviewService FinalReview { get; }
    private readonly Lazy<IExcelProjectExporter> excelExporter;
    private readonly Lazy<IPdfProjectExporter> pdfExporter;
    private readonly Lazy<IPresentationLetterDocxExporter> wordLetterExporter;
    private readonly Lazy<ICubicacionReportExcelExporter> cubicacionReportExcelExporter;
    private readonly Lazy<ICubicacionReportPdfExporter> cubicacionReportPdfExporter;
    private readonly Lazy<ICubicacionSCReportExcelExporter> cubicacionSCReportExcelExporter;

    public IPriceTraceabilityService PriceTraceability { get; }
    public IExcelProjectExporter ExcelExporter => excelExporter.Value;
    public IPdfProjectExporter PdfExporter => pdfExporter.Value;
    public IPresentationLetterDocxExporter WordLetterExporter => wordLetterExporter.Value;
    public ICubicacionReportExcelExporter CubicacionReportExcelExporter => cubicacionReportExcelExporter.Value;
    public ICubicacionReportPdfExporter CubicacionReportPdfExporter => cubicacionReportPdfExporter.Value;
    public ICubicacionSCReportExcelExporter CubicacionSCReportExcelExporter => cubicacionSCReportExcelExporter.Value;
    public BudgetVarianceService BudgetVariance { get; }
    public IVersionComparisonService VersionComparison { get; }

    public PriceBankApplicationService PriceBank { get; }
    public IPriceBankStore PriceBankStore { get; }

    public IApuCatalogRepository ApuCatalogRepository { get; set; } = null!;
    public IApuCatalogService ApuCatalog { get; set; } = null!;
    public IApuVersionRepository ApuVersionRepository { get; set; } = null!;

    public AppServices(
        IBudgetCalculationService budgetCalculation,
        IMaterialSearchService materialSearch,
        IBudgetKpiService budgetKpi,
        ICurrencyConversionService currencyConversion,
        ICubicacionViewModelService cubicacionViewModel,
        IProyectoViewModelService proyectoViewModel,
        IApuViewModelService apuViewModel,
        IApuService apu,
        IPurchaseListService purchaseList,
        ITemplateService template,
        IValidationService validation,
        ICubicacionService cubicacion,
        ICubicacionTakeoffQueryService cubicacionTakeoffQuery,
        IItemizadoService itemizado,
        IProjectRepository projectRepository,
        IPartidaCompletionService partidaCompletion,
        IFinalReviewService finalReview,
        IPriceTraceabilityService priceTraceability,
        IExcelProjectExporter excelExporter,
        IPdfProjectExporter pdfExporter,
        IPresentationLetterDocxExporter wordLetterExporter,
        ICubicacionReportExcelExporter cubicacionReportExcelExporter,
        ICubicacionReportPdfExporter cubicacionReportPdfExporter,
        ICubicacionSCReportExcelExporter cubicacionSCReportExcelExporter,
        BudgetVarianceService budgetVariance,
        IVersionComparisonService versionComparison,
        PriceBankApplicationService priceBank,
        IPriceBankStore priceBankStore)
    {
        BudgetCalculation = budgetCalculation;
        MaterialSearch = materialSearch;
        BudgetKpi = budgetKpi;
        CurrencyConversion = currencyConversion;
        CubicacionViewModel = cubicacionViewModel;
        ProyectoViewModel = proyectoViewModel;
        ApuViewModel = apuViewModel;
        Apu = apu;
        PurchaseList = purchaseList;
        Template = template;
        Validation = validation;
        Cubicacion = cubicacion;
        CubicacionTakeoffQuery = cubicacionTakeoffQuery;
        Itemizado = itemizado;
        ProjectRepository = projectRepository;
        PartidaCompletion = partidaCompletion;
        FinalReview = finalReview;
        PriceTraceability = priceTraceability;
        this.excelExporter = new Lazy<IExcelProjectExporter>(() => excelExporter, LazyThreadSafetyMode.ExecutionAndPublication);
        this.pdfExporter = new Lazy<IPdfProjectExporter>(() => pdfExporter, LazyThreadSafetyMode.ExecutionAndPublication);
        this.wordLetterExporter = new Lazy<IPresentationLetterDocxExporter>(() => wordLetterExporter, LazyThreadSafetyMode.ExecutionAndPublication);
        this.cubicacionReportExcelExporter = new Lazy<ICubicacionReportExcelExporter>(() => cubicacionReportExcelExporter, LazyThreadSafetyMode.ExecutionAndPublication);
        this.cubicacionReportPdfExporter = new Lazy<ICubicacionReportPdfExporter>(() => cubicacionReportPdfExporter, LazyThreadSafetyMode.ExecutionAndPublication);
        this.cubicacionSCReportExcelExporter = new Lazy<ICubicacionSCReportExcelExporter>(() => cubicacionSCReportExcelExporter, LazyThreadSafetyMode.ExecutionAndPublication);
        BudgetVariance = budgetVariance;
        VersionComparison = versionComparison;
        PriceBank = priceBank;
        PriceBankStore = priceBankStore;
    }

    internal AppServices(
        IBudgetCalculationService budgetCalculation,
        IMaterialSearchService materialSearch,
        IBudgetKpiService budgetKpi,
        ICurrencyConversionService currencyConversion,
        ICubicacionViewModelService cubicacionViewModel,
        IProyectoViewModelService proyectoViewModel,
        IApuViewModelService apuViewModel,
        IApuService apu,
        IPurchaseListService purchaseList,
        ITemplateService template,
        IValidationService validation,
        ICubicacionService cubicacion,
        ICubicacionTakeoffQueryService cubicacionTakeoffQuery,
        IItemizadoService itemizado,
        IItemizadoSpreadsheetReader itemizadoSpreadsheet,
        IMaterialSpreadsheetImporter materialSpreadsheet,
        ITemplateSpreadsheetService templateSpreadsheet,
        IPurchaseListExcelExporter purchaseListExcel,
        ICubicacionSCExcelExporter cubicacionSCExcel,
        ImportarItemizadoUseCase importItemizado,
        PegarItemizadoUseCase pasteItemizado,
        IProjectRepository projectRepository,
        IPartidaCompletionService partidaCompletion,
        IFinalReviewService finalReview,
        IPriceTraceabilityService priceTraceability,
        Lazy<IExcelProjectExporter> excelExporter,
        Lazy<IPdfProjectExporter> pdfExporter,
        Lazy<IPresentationLetterDocxExporter> wordLetterExporter,
        Lazy<ICubicacionReportExcelExporter> cubicacionReportExcelExporter,
        Lazy<ICubicacionReportPdfExporter> cubicacionReportPdfExporter,
        Lazy<ICubicacionSCReportExcelExporter> cubicacionSCReportExcelExporter,
        BudgetVarianceService budgetVariance,
        IVersionComparisonService versionComparison,
        PriceBankApplicationService priceBank,
        IPriceBankStore priceBankStore)
    {
        BudgetCalculation = budgetCalculation;
        MaterialSearch = materialSearch;
        BudgetKpi = budgetKpi;
        CurrencyConversion = currencyConversion;
        CubicacionViewModel = cubicacionViewModel;
        ProyectoViewModel = proyectoViewModel;
        ApuViewModel = apuViewModel;
        Apu = apu;
        PurchaseList = purchaseList;
        Template = template;
        Validation = validation;
        Cubicacion = cubicacion;
        CubicacionTakeoffQuery = cubicacionTakeoffQuery;
        Itemizado = itemizado;
        ItemizadoSpreadsheet = itemizadoSpreadsheet;
        MaterialSpreadsheet = materialSpreadsheet;
        TemplateSpreadsheet = templateSpreadsheet;
        PurchaseListExcel = purchaseListExcel;
        CubicacionSCExcel = cubicacionSCExcel;
        ImportItemizado = importItemizado;
        PasteItemizado = pasteItemizado;
        ProjectRepository = projectRepository;
        PartidaCompletion = partidaCompletion;
        FinalReview = finalReview;
        PriceTraceability = priceTraceability;
        this.excelExporter = excelExporter;
        this.pdfExporter = pdfExporter;
        this.wordLetterExporter = wordLetterExporter;
        this.cubicacionReportExcelExporter = cubicacionReportExcelExporter;
        this.cubicacionReportPdfExporter = cubicacionReportPdfExporter;
        this.cubicacionSCReportExcelExporter = cubicacionSCReportExcelExporter;
        BudgetVariance = budgetVariance;
        VersionComparison = versionComparison;
        PriceBank = priceBank;
        PriceBankStore = priceBankStore;
    }

    public static AppServices CreateDefault() => new AppServicesBuilder().Build();
}
