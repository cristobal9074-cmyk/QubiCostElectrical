using System;
using System.Threading;

namespace QubiCostElectrical;

public sealed class AppServicesBuilder
{
    private IBudgetCalculationService? budgetCalculation;
    private IMaterialSearchService? materialSearch;
    private IBudgetKpiService? budgetKpi;
    private ICurrencyConversionService? currencyConversion;
    private ICubicacionViewModelService? cubicacionViewModel;
    private IProyectoViewModelService? proyectoViewModel;
    private IApuViewModelService? apuViewModel;
    private IApuService? apu;
    private IPurchaseListService? purchaseList;
    private ITemplateService? template;
    private IValidationService? validation;
    private ICubicacionService? cubicacion;
    private ICubicacionTakeoffQueryService? cubicacionTakeoffQuery;
    private IItemizadoService? itemizado;
    private IItemizadoSpreadsheetReader? itemizadoSpreadsheet;
    private IMaterialSpreadsheetImporter? materialSpreadsheet;
    private ITemplateSpreadsheetService? templateSpreadsheet;
    private IPurchaseListExcelExporter? purchaseListExcel;
    private ICubicacionSCExcelExporter? cubicacionSCExcel;
    private ImportarItemizadoUseCase? importItemizado;
    private PegarItemizadoUseCase? pasteItemizado;
    private IProjectRepository? projectRepository;
    private IPartidaCompletionService? partidaCompletion;
    private IFinalReviewService? finalReview;
    private IPriceTraceabilityService? priceTraceability;
    private Lazy<IExcelProjectExporter>? excelExporter;
    private Lazy<IPdfProjectExporter>? pdfExporter;
    private Lazy<IPresentationLetterDocxExporter>? wordLetterExporter;
    private Lazy<ICubicacionReportExcelExporter>? cubicacionReportExcelExporter;
    private Lazy<ICubicacionReportPdfExporter>? cubicacionReportPdfExporter;
    private Lazy<ICubicacionSCReportExcelExporter>? cubicacionSCReportExcelExporter;
    private BudgetVarianceService? budgetVariance;
    private IVersionComparisonService? versionComparison;
    private IPriceBankStore? priceBankStore;
    private PriceBankApplicationService? priceBank;

    public AppServicesBuilder WithBudgetCalculation(IBudgetCalculationService value)
    {
        budgetCalculation = value;
        return this;
    }

    public AppServicesBuilder WithMaterialSearch(IMaterialSearchService value)
    {
        materialSearch = value;
        return this;
    }

    public AppServicesBuilder WithBudgetKpi(IBudgetKpiService value)
    {
        budgetKpi = value;
        return this;
    }

    public AppServicesBuilder WithCurrencyConversion(ICurrencyConversionService value)
    {
        currencyConversion = value;
        return this;
    }

    public AppServicesBuilder WithCubicacionViewModel(ICubicacionViewModelService value)
    {
        cubicacionViewModel = value;
        return this;
    }

    public AppServicesBuilder WithProyectoViewModel(IProyectoViewModelService value)
    {
        proyectoViewModel = value;
        return this;
    }

    public AppServicesBuilder WithApuViewModel(IApuViewModelService value)
    {
        apuViewModel = value;
        return this;
    }

    public AppServicesBuilder WithApu(IApuService value)
    {
        apu = value;
        return this;
    }

    public AppServicesBuilder WithPurchaseList(IPurchaseListService value)
    {
        purchaseList = value;
        return this;
    }

    public AppServicesBuilder WithTemplate(ITemplateService value)
    {
        template = value;
        return this;
    }

    public AppServicesBuilder WithValidation(IValidationService value)
    {
        validation = value;
        return this;
    }

    public AppServicesBuilder WithCubicacion(ICubicacionService value)
    {
        cubicacion = value;
        return this;
    }

    public AppServicesBuilder WithCubicacionTakeoffQuery(ICubicacionTakeoffQueryService value)
    {
        cubicacionTakeoffQuery = value;
        return this;
    }

    public AppServicesBuilder WithItemizado(IItemizadoService value)
    {
        itemizado = value;
        return this;
    }

    public AppServicesBuilder WithItemizadoSpreadsheet(IItemizadoSpreadsheetReader value)
    {
        itemizadoSpreadsheet = value;
        return this;
    }

    public AppServicesBuilder WithMaterialSpreadsheet(IMaterialSpreadsheetImporter value)
    {
        materialSpreadsheet = value;
        return this;
    }

    public AppServicesBuilder WithTemplateSpreadsheet(ITemplateSpreadsheetService value)
    {
        templateSpreadsheet = value;
        return this;
    }

    public AppServicesBuilder WithPurchaseListExcel(IPurchaseListExcelExporter value)
    {
        purchaseListExcel = value;
        return this;
    }

    public AppServicesBuilder WithCubicacionSCExcel(ICubicacionSCExcelExporter value)
    {
        cubicacionSCExcel = value;
        return this;
    }

    public AppServicesBuilder WithImportItemizado(ImportarItemizadoUseCase value)
    {
        importItemizado = value;
        return this;
    }

    public AppServicesBuilder WithPasteItemizado(PegarItemizadoUseCase value)
    {
        pasteItemizado = value;
        return this;
    }

    public AppServicesBuilder WithProjectRepository(IProjectRepository value)
    {
        projectRepository = value;
        return this;
    }

    public AppServicesBuilder WithPartidaCompletion(IPartidaCompletionService value)
    {
        partidaCompletion = value;
        return this;
    }

    public AppServicesBuilder WithFinalReview(IFinalReviewService value)
    {
        finalReview = value;
        return this;
    }

    public AppServicesBuilder WithPriceTraceability(IPriceTraceabilityService value)
    {
        priceTraceability = value;
        return this;
    }

    public AppServicesBuilder WithExcelExporter(IExcelProjectExporter value)
    {
        excelExporter = Lazy(value);
        return this;
    }

    public AppServicesBuilder WithPdfExporter(IPdfProjectExporter value)
    {
        pdfExporter = Lazy(value);
        return this;
    }

    public AppServicesBuilder WithWordLetterExporter(IPresentationLetterDocxExporter value)
    {
        wordLetterExporter = Lazy(value);
        return this;
    }

    public AppServicesBuilder WithCubicacionReportExcelExporter(ICubicacionReportExcelExporter value)
    {
        cubicacionReportExcelExporter = Lazy(value);
        return this;
    }

    public AppServicesBuilder WithCubicacionReportPdfExporter(ICubicacionReportPdfExporter value)
    {
        cubicacionReportPdfExporter = Lazy(value);
        return this;
    }

    public AppServicesBuilder WithCubicacionSCReportExcelExporter(ICubicacionSCReportExcelExporter value)
    {
        cubicacionSCReportExcelExporter = Lazy(value);
        return this;
    }

    public AppServicesBuilder WithBudgetVariance(BudgetVarianceService value)
    {
        budgetVariance = value;
        return this;
    }

    public AppServicesBuilder WithVersionComparison(IVersionComparisonService value)
    {
        versionComparison = value;
        return this;
    }

    public AppServicesBuilder WithPriceBankStore(IPriceBankStore value)
    {
        priceBankStore = value;
        return this;
    }

    public AppServicesBuilder WithPriceBank(PriceBankApplicationService value)
    {
        priceBank = value;
        return this;
    }

    public AppServices Build()
    {
        var resolvedBudgetCalculation = budgetCalculation ?? new BudgetCalculationService();
        var resolvedMaterialSearch = materialSearch ?? new MaterialSearchService();
        var resolvedCurrencyConversion = currencyConversion ?? new CurrencyConversionService();
        var resolvedCubicacionViewModel = cubicacionViewModel ?? new CubicacionViewModelService(resolvedBudgetCalculation);
        var resolvedProyectoViewModel = proyectoViewModel ?? new ProyectoViewModelService(resolvedBudgetCalculation, resolvedCubicacionViewModel);
        var resolvedApuViewModel = apuViewModel ?? new ApuViewModelService(resolvedCubicacionViewModel);
        var resolvedApu = apu ?? new ApuService();
        var resolvedBudgetKpi = budgetKpi ?? new BudgetKpiService(resolvedBudgetCalculation);
        var resolvedPurchaseList = purchaseList ?? new PurchaseListService();
        var resolvedTemplate = template ?? new TemplateService();
        var resolvedValidation = validation ?? new ValidationService();
        var resolvedCubicacion = cubicacion ?? new CubicacionService();
        var resolvedCubicacionTakeoffQuery = cubicacionTakeoffQuery ?? new CubicacionTakeoffQueryService();
        var resolvedItemizado = itemizado ?? new ItemizadoService();
        var resolvedItemizadoSpreadsheet = itemizadoSpreadsheet ?? new ItemizadoSpreadsheetReader();
        var resolvedMaterialSpreadsheet = materialSpreadsheet ?? new MaterialSpreadsheetImporter(resolvedItemizadoSpreadsheet);
        var resolvedTemplateSpreadsheet = templateSpreadsheet ?? new TemplateSpreadsheetService();
        var resolvedPurchaseListExcel = purchaseListExcel ?? new PurchaseListExcelExporter();
        var resolvedCubicacionSCExcel = cubicacionSCExcel ?? new CubicacionSCExcelExporter();
        var resolvedImportItemizado = importItemizado ?? new ImportarItemizadoUseCase();
        var resolvedPasteItemizado = pasteItemizado ?? new PegarItemizadoUseCase();
        var resolvedProjectRepository = projectRepository ?? new ProjectRepository();
        var resolvedPartidaCompletion = partidaCompletion ?? new PartidaCompletionService();
        var resolvedFinalReview = finalReview ?? new FinalReviewService();
        var resolvedPriceTraceability = priceTraceability ?? new PriceTraceabilityService();
        var resolvedBudgetVariance = budgetVariance ?? new BudgetVarianceService();
        var resolvedVersionComparison = versionComparison ?? new BudgetVersionComparisonService();
        var resolvedPriceBankStore = priceBankStore ?? new PriceBankStore();
        TrySeedPriceBank(resolvedPriceBankStore);
        var resolvedPriceBank = priceBank ?? new PriceBankApplicationService(new PriceBankRepository(resolvedPriceBankStore));

        var resolvedExcelExporter = excelExporter ?? new Lazy<IExcelProjectExporter>(
            () => new ExcelExportInfrastructure(resolvedBudgetCalculation, resolvedFinalReview, resolvedPriceTraceability, resolvedCurrencyConversion),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var resolvedPdfExporter = pdfExporter ?? new Lazy<IPdfProjectExporter>(
            () => new QuestPdfProjectExporter(resolvedBudgetCalculation, resolvedCurrencyConversion),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var resolvedWordLetterExporter = wordLetterExporter ?? new Lazy<IPresentationLetterDocxExporter>(
            () => new PresentationLetterDocxExporter(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var resolvedCubicacionReportExcelExporter = cubicacionReportExcelExporter ?? new Lazy<ICubicacionReportExcelExporter>(
            () => new CubicacionReportExcelExporter(resolvedCurrencyConversion),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var resolvedCubicacionReportPdfExporter = cubicacionReportPdfExporter ?? new Lazy<ICubicacionReportPdfExporter>(
            () => new QuestPdfCubicacionReportExporter(resolvedCurrencyConversion),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var resolvedCubicacionSCReportExcelExporter = cubicacionSCReportExcelExporter ?? new Lazy<ICubicacionSCReportExcelExporter>(
            () => new CubicacionSCTechnicalReportExcelExporter(resolvedCurrencyConversion),
            LazyThreadSafetyMode.ExecutionAndPublication);

        return new AppServices(
            resolvedBudgetCalculation,
            resolvedMaterialSearch,
            resolvedBudgetKpi,
            resolvedCurrencyConversion,
            resolvedCubicacionViewModel,
            resolvedProyectoViewModel,
            resolvedApuViewModel,
            resolvedApu,
            resolvedPurchaseList,
            resolvedTemplate,
            resolvedValidation,
            resolvedCubicacion,
            resolvedCubicacionTakeoffQuery,
            resolvedItemizado,
            resolvedItemizadoSpreadsheet,
            resolvedMaterialSpreadsheet,
            resolvedTemplateSpreadsheet,
            resolvedPurchaseListExcel,
            resolvedCubicacionSCExcel,
            resolvedImportItemizado,
            resolvedPasteItemizado,
            resolvedProjectRepository,
            resolvedPartidaCompletion,
            resolvedFinalReview,
            resolvedPriceTraceability,
            resolvedExcelExporter,
            resolvedPdfExporter,
            resolvedWordLetterExporter,
            resolvedCubicacionReportExcelExporter,
            resolvedCubicacionReportPdfExporter,
            resolvedCubicacionSCReportExcelExporter,
            resolvedBudgetVariance,
            resolvedVersionComparison,
            resolvedPriceBank,
            resolvedPriceBankStore);
    }

    private static Lazy<T> Lazy<T>(T value)
        => new(() => value, LazyThreadSafetyMode.ExecutionAndPublication);

    private static void TrySeedPriceBank(IPriceBankStore store)
    {
        try
        {
            string? seedFile = PriceBankSeeder.FindSeedFile(AppContext.BaseDirectory);
            if (seedFile != null)
            {
                var entries = PriceBankSeeder.ParseFile(seedFile);
                int inserted = store.SeedIfEmpty(entries);
                if (inserted > 0)
                    AppLog.Info("BancoPrecios", $"Banco de precios sembrado con {inserted} materiales desde '{System.IO.Path.GetFileName(seedFile)}'.");
                return;
            }

            var embedded = PriceBankSeeder.GetEmbeddedEntries();
            if (embedded.Count > 0)
            {
                int inserted = store.SeedIfEmpty(embedded);
                if (inserted > 0)
                    AppLog.Info("BancoPrecios", $"Banco de precios sembrado con {inserted} materiales desde banco inicial embebido.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", "No se pudo sembrar el banco de precios inicial.", ex);
        }
    }
}
