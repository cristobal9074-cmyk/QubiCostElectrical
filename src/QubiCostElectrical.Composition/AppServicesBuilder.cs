using System;
using System.Threading;

namespace QubiCostElectrical;

public sealed partial class AppServicesBuilder
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
    private IVersionWorkflowService? versionWorkflow;
    private IPriceBankStore? priceBankStore;
    private PriceBankApplicationService? priceBank;
    private IResourceRepository? resourceRepository;
    private IPriceHistoryRepository? priceHistoryRepository;
    private IExchangeRateRepository? exchangeRateRepository;
    private IApuVersionRepository? apuVersionRepository;
    private IApuSnapshotRepository? apuSnapshotRepository;
    private IApuCatalogRepository? apuCatalogRepository;
    private IApuCalculationService? apuCalculation;
    private IEconomicCalculationService? economicCalculation;
    private IApuCatalogService? apuCatalog;
    private IScheduleRepository? scheduleRepository;
    private IWbsPlanningRepository? wbsPlanningRepository;
    private IActivityRepository? activityRepository;
    private ICalendarRepository? calendarRepository;
    private IPlanningAuditRepository? planningAuditRepository;
    private IActivityDependencyRepository? activityDependencyRepository;
    private ICpmCalculationService? cpmCalculation;
    private IScheduleBaselineRepository? scheduleBaselineRepository;
    private IScheduleBaselineSnapshotRepository? scheduleBaselineSnapshotRepository;
    private IScheduleBaselineService? scheduleBaseline;

    // Payment Certificate fields
    private IPaymentCertificateRepository? paymentCertificateRepository;
    private IPaymentCertificateLineRepository? paymentCertificateLineRepository;
    private IContractAdvanceRepository? contractAdvanceRepository;
    private IAdvanceAmortizationRepository? advanceAmortizationRepository;
    private IContractRetentionRepository? contractRetentionRepository;
    private IRetentionApplicationRepository? retentionApplicationRepository;
    private IRetentionReleaseRepository? retentionReleaseRepository;
    private IPaymentObservationRepository? paymentObservationRepository;
    private IPaymentCertificateRevisionRepository? paymentCertificateRevisionRepository;
    private IPaymentCertificateAuditRepository? paymentCertificateAuditRepository;
    private IPaymentCertificateService? paymentCertificateService;

    // Contract Change fields
    private IChangeRequestRepository? changeRequestRepository;
    private IChangeOrderRepository? changeOrderRepository;
    private IChangeOrderLineRepository? changeOrderLineRepository;
    private IExtraWorkRepository? extraWorkRepository;
    private IContractClaimRepository? contractClaimRepository;
    private IContractChangeAuditRepository? contractChangeAuditRepository;
    private IContractChangePermissionChecker? contractChangePermissionChecker;
    private IContractChangeService? contractChangeService;

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
        var resolvedVersionWorkflow = versionWorkflow ?? new VersionWorkflowService();
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

        var svc = new AppServices(
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
            resolvedVersionWorkflow,
            resolvedPriceBank,
            resolvedPriceBankStore);

        if (resourceRepository != null) svc.ResourceRepository = resourceRepository;
        if (priceHistoryRepository != null) svc.PriceHistoryRepository = priceHistoryRepository;
        if (exchangeRateRepository != null) svc.ExchangeRateRepository = exchangeRateRepository;
        if (apuVersionRepository != null) svc.ApuVersionRepository = apuVersionRepository;
        if (apuSnapshotRepository != null) svc.ApuSnapshotRepository = apuSnapshotRepository;
        if (apuCatalogRepository != null) svc.ApuCatalogRepository = apuCatalogRepository;
        if (apuCalculation != null) svc.ApuCalculation = apuCalculation;
        if (economicCalculation != null) svc.EconomicCalculation = economicCalculation;
        if (apuCatalog != null) svc.ApuCatalog = apuCatalog;
        if (scheduleRepository != null) svc.ScheduleRepository = scheduleRepository;
        if (wbsPlanningRepository != null) svc.WbsPlanningRepository = wbsPlanningRepository;
        if (activityRepository != null) svc.ActivityRepository = activityRepository;
        if (calendarRepository != null) svc.CalendarRepository = calendarRepository;
        if (planningAuditRepository != null) svc.PlanningAuditRepository = planningAuditRepository;
        if (activityDependencyRepository != null) svc.ActivityDependencyRepository = activityDependencyRepository;
        if (cpmCalculation != null) svc.CpmCalculation = cpmCalculation;
        if (scheduleBaselineRepository != null) svc.ScheduleBaselineRepository = scheduleBaselineRepository;
        if (scheduleBaselineSnapshotRepository != null) svc.ScheduleBaselineSnapshotRepository = scheduleBaselineSnapshotRepository;
        if (scheduleBaseline != null) svc.ScheduleBaseline = scheduleBaseline;

        if (paymentCertificateRepository != null) svc.PaymentCertificateRepository = paymentCertificateRepository;
        if (paymentCertificateLineRepository != null) svc.PaymentCertificateLineRepository = paymentCertificateLineRepository;
        if (contractAdvanceRepository != null) svc.ContractAdvanceRepository = contractAdvanceRepository;
        if (advanceAmortizationRepository != null) svc.AdvanceAmortizationRepository = advanceAmortizationRepository;
        if (contractRetentionRepository != null) svc.ContractRetentionRepository = contractRetentionRepository;
        if (retentionApplicationRepository != null) svc.RetentionApplicationRepository = retentionApplicationRepository;
        if (retentionReleaseRepository != null) svc.RetentionReleaseRepository = retentionReleaseRepository;
        if (paymentObservationRepository != null) svc.PaymentObservationRepository = paymentObservationRepository;
        if (paymentCertificateRevisionRepository != null) svc.PaymentCertificateRevisionRepository = paymentCertificateRevisionRepository;
        if (paymentCertificateAuditRepository != null) svc.PaymentCertificateAuditRepository = paymentCertificateAuditRepository;
        if (paymentCertificateService != null) svc.PaymentCertificateService = paymentCertificateService;

        if (changeRequestRepository != null) svc.ChangeRequestRepository = changeRequestRepository;
        if (changeOrderRepository != null) svc.ChangeOrderRepository = changeOrderRepository;
        if (changeOrderLineRepository != null) svc.ChangeOrderLineRepository = changeOrderLineRepository;
        if (extraWorkRepository != null) svc.ExtraWorkRepository = extraWorkRepository;
        if (contractClaimRepository != null) svc.ContractClaimRepository = contractClaimRepository;
        if (contractChangeAuditRepository != null) svc.ContractChangeAuditRepository = contractChangeAuditRepository;
        if (contractChangePermissionChecker != null) svc.ContractChangePermissionChecker = contractChangePermissionChecker;
        if (contractChangeService != null) svc.ContractChangeService = contractChangeService;

        WireContractAdministrationServices(svc);
        WireContractDashboardServices(svc);
        WireCloseoutServices(svc);
        WireCloseoutFinalServices(svc);

        return svc;
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
