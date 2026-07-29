using System;
using System.Threading;

namespace QubiCostElectrical;

partial class AppServicesBuilder
{
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

    public AppServicesBuilder WithVersionWorkflow(IVersionWorkflowService value)
    {
        versionWorkflow = value;
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

    public AppServicesBuilder WithResourceRepository(IResourceRepository value)
    {
        resourceRepository = value;
        return this;
    }

    public AppServicesBuilder WithPriceHistoryRepository(IPriceHistoryRepository value)
    {
        priceHistoryRepository = value;
        return this;
    }

    public AppServicesBuilder WithExchangeRateRepository(IExchangeRateRepository value)
    {
        exchangeRateRepository = value;
        return this;
    }

    public AppServicesBuilder WithApuVersionRepository(IApuVersionRepository value)
    {
        apuVersionRepository = value;
        return this;
    }

    public AppServicesBuilder WithApuSnapshotRepository(IApuSnapshotRepository value)
    {
        apuSnapshotRepository = value;
        return this;
    }

    public AppServicesBuilder WithApuCatalogRepository(IApuCatalogRepository value)
    {
        apuCatalogRepository = value;
        return this;
    }

    public AppServicesBuilder WithApuCalculation(IApuCalculationService value)
    {
        apuCalculation = value;
        return this;
    }

    public AppServicesBuilder WithEconomicCalculation(IEconomicCalculationService value)
    {
        economicCalculation = value;
        return this;
    }

    public AppServicesBuilder WithApuCatalog(IApuCatalogService value)
    {
        apuCatalog = value;
        return this;
    }

    public AppServicesBuilder WithScheduleRepository(IScheduleRepository value)
    {
        scheduleRepository = value;
        return this;
    }

    public AppServicesBuilder WithWbsPlanningRepository(IWbsPlanningRepository value)
    {
        wbsPlanningRepository = value;
        return this;
    }

    public AppServicesBuilder WithActivityRepository(IActivityRepository value)
    {
        activityRepository = value;
        return this;
    }

    public AppServicesBuilder WithCalendarRepository(ICalendarRepository value)
    {
        calendarRepository = value;
        return this;
    }

    public AppServicesBuilder WithPlanningAuditRepository(IPlanningAuditRepository value)
    {
        planningAuditRepository = value;
        return this;
    }

    public AppServicesBuilder WithActivityDependencyRepository(IActivityDependencyRepository value)
    {
        activityDependencyRepository = value;
        return this;
    }

    public AppServicesBuilder WithCpmCalculation(ICpmCalculationService value)
    {
        cpmCalculation = value;
        return this;
    }

    public AppServicesBuilder WithScheduleBaselineRepository(IScheduleBaselineRepository value)
    {
        scheduleBaselineRepository = value;
        return this;
    }

    public AppServicesBuilder WithScheduleBaselineSnapshotRepository(IScheduleBaselineSnapshotRepository value)
    {
        scheduleBaselineSnapshotRepository = value;
        return this;
    }

    public AppServicesBuilder WithScheduleBaseline(IScheduleBaselineService value)
    {
        scheduleBaseline = value;
        return this;
    }

    // Payment Certificate methods
    public AppServicesBuilder WithPaymentCertificateRepository(IPaymentCertificateRepository value)
    {
        paymentCertificateRepository = value;
        return this;
    }

    public AppServicesBuilder WithPaymentCertificateLineRepository(IPaymentCertificateLineRepository value)
    {
        paymentCertificateLineRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractAdvanceRepository(IContractAdvanceRepository value)
    {
        contractAdvanceRepository = value;
        return this;
    }

    public AppServicesBuilder WithAdvanceAmortizationRepository(IAdvanceAmortizationRepository value)
    {
        advanceAmortizationRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractRetentionRepository(IContractRetentionRepository value)
    {
        contractRetentionRepository = value;
        return this;
    }

    public AppServicesBuilder WithRetentionApplicationRepository(IRetentionApplicationRepository value)
    {
        retentionApplicationRepository = value;
        return this;
    }

    public AppServicesBuilder WithRetentionReleaseRepository(IRetentionReleaseRepository value)
    {
        retentionReleaseRepository = value;
        return this;
    }

    public AppServicesBuilder WithPaymentObservationRepository(IPaymentObservationRepository value)
    {
        paymentObservationRepository = value;
        return this;
    }

    public AppServicesBuilder WithPaymentCertificateRevisionRepository(IPaymentCertificateRevisionRepository value)
    {
        paymentCertificateRevisionRepository = value;
        return this;
    }

    public AppServicesBuilder WithPaymentCertificateAuditRepository(IPaymentCertificateAuditRepository value)
    {
        paymentCertificateAuditRepository = value;
        return this;
    }

    public AppServicesBuilder WithPaymentCertificateService(IPaymentCertificateService value)
    {
        paymentCertificateService = value;
        return this;
    }

    // ── Contract Change methods ────────────────────────────────────
    public AppServicesBuilder WithChangeRequestRepository(IChangeRequestRepository value)
    {
        changeRequestRepository = value;
        return this;
    }

    public AppServicesBuilder WithChangeOrderRepository(IChangeOrderRepository value)
    {
        changeOrderRepository = value;
        return this;
    }

    public AppServicesBuilder WithChangeOrderLineRepository(IChangeOrderLineRepository value)
    {
        changeOrderLineRepository = value;
        return this;
    }

    public AppServicesBuilder WithExtraWorkRepository(IExtraWorkRepository value)
    {
        extraWorkRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractClaimRepository(IContractClaimRepository value)
    {
        contractClaimRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractChangeAuditRepository(IContractChangeAuditRepository value)
    {
        contractChangeAuditRepository = value;
        return this;
    }

    public AppServicesBuilder WithContractChangePermissionChecker(IContractChangePermissionChecker value)
    {
        contractChangePermissionChecker = value;
        return this;
    }

    public AppServicesBuilder WithContractChangeService(IContractChangeService value)
    {
        contractChangeService = value;
        return this;
    }
}
