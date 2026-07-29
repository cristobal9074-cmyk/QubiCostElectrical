using System;
using System.Threading;

namespace QubiCostElectrical;

public sealed partial class AppServices
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
    public IVersionWorkflowService VersionWorkflow { get; }

    public PriceBankApplicationService PriceBank { get; }
    public IPriceBankStore PriceBankStore { get; }

    public IResourceRepository ResourceRepository { get; internal set; } = null!;
    public IPriceHistoryRepository PriceHistoryRepository { get; internal set; } = null!;
    public IExchangeRateRepository ExchangeRateRepository { get; internal set; } = null!;
    public IApuVersionRepository ApuVersionRepository { get; internal set; } = null!;
    public IApuSnapshotRepository ApuSnapshotRepository { get; internal set; } = null!;
    public IApuCatalogRepository ApuCatalogRepository { get; internal set; } = null!;
    public IApuCalculationService ApuCalculation { get; internal set; } = null!;
    public IEconomicCalculationService EconomicCalculation { get; internal set; } = null!;
    public IApuCatalogService ApuCatalog { get; internal set; } = null!;
    public IScheduleRepository ScheduleRepository { get; internal set; } = null!;
    public IWbsPlanningRepository WbsPlanningRepository { get; internal set; } = null!;
    public IActivityRepository ActivityRepository { get; internal set; } = null!;
    public ICalendarRepository CalendarRepository { get; internal set; } = null!;
    public IPlanningAuditRepository PlanningAuditRepository { get; internal set; } = null!;
    public IActivityDependencyRepository ActivityDependencyRepository { get; internal set; } = null!;
    public ICpmCalculationService CpmCalculation { get; internal set; } = null!;
    public IScheduleBaselineRepository ScheduleBaselineRepository { get; internal set; } = null!;
    public IScheduleBaselineSnapshotRepository ScheduleBaselineSnapshotRepository { get; internal set; } = null!;
    public IScheduleBaselineService ScheduleBaseline { get; internal set; } = null!;
    public IStatusDateRepository StatusDateRepository { get; internal set; } = null!;
    public IActivityProgressRepository ActivityProgressRepository { get; internal set; } = null!;
    public IProgressIntervalRepository ProgressIntervalRepository { get; internal set; } = null!;
    public IDelayReasonRepository DelayReasonRepository { get; internal set; } = null!;
    public IProgressEvidenceRepository ProgressEvidenceRepository { get; internal set; } = null!;
    public IProgressSnapshotRepository ProgressSnapshotRepository { get; internal set; } = null!;
    public IProgressTrackingService ProgressTracking { get; internal set; } = null!;
    public IPlanningPermissionChecker PlanningPermissionsChecker { get; internal set; } = null!;
    public IEarnedValueRepository EarnedValueRepository { get; internal set; } = null!;
    public IEarnedValueSnapshotRepository EarnedValueSnapshotRepository { get; internal set; } = null!;
    public IEarnedValueHistoryRepository EarnedValueHistoryRepository { get; internal set; } = null!;
    public IEarnedValueService EarnedValue { get; internal set; } = null!;
    public IDashboardSnapshotRepository DashboardSnapshotRepository { get; internal set; } = null!;
    public IDashboardAlertRepository DashboardAlertRepository { get; internal set; } = null!;
    public IDashboardPreferenceRepository DashboardPreferenceRepository { get; internal set; } = null!;
    public IProjectDashboardService ProjectDashboard { get; internal set; } = null!;

    public ISupplierRepository SupplierRepository { get; internal set; } = null!;
    public ISupplierContactRepository SupplierContactRepository { get; internal set; } = null!;
    public IPurchaseRequisitionRepository PurchaseRequisitionRepository { get; internal set; } = null!;
    public IRequestForQuotationRepository RequestForQuotationRepository { get; internal set; } = null!;
    public IQuotationResponseRepository QuotationResponseRepository { get; internal set; } = null!;
    public IProcurementDocumentRepository ProcurementDocumentRepository { get; internal set; } = null!;
    public IProcurementAuditRepository ProcurementAuditRepository { get; internal set; } = null!;
    public ISupplierService SupplierService { get; internal set; } = null!;
    public IPurchaseRequisitionService PurchaseRequisitionService { get; internal set; } = null!;
    public IRfqService RfqService { get; internal set; } = null!;
    public IProcurementPermissionChecker ProcurementPermissionsChecker { get; internal set; } = null!;
    public ISupplierOfferRepository SupplierOfferRepository { get; internal set; } = null!;
    public ITechnicalEvaluationRepository TechnicalEvaluationRepository { get; internal set; } = null!;
    public ICommercialEvaluationRepository CommercialEvaluationRepository { get; internal set; } = null!;
    public IBidComparisonRepository BidComparisonRepository { get; internal set; } = null!;
    public IAwardRepository AwardRepository { get; internal set; } = null!;
    public ISupplierOfferService SupplierOfferService { get; internal set; } = null!;
    public IBidComparisonService BidComparisonService { get; internal set; } = null!;
    public IAwardService AwardService { get; internal set; } = null!;
    public IPurchaseOrderRepository PurchaseOrderRepository { get; internal set; } = null!;
    public IPurchaseOrderService PurchaseOrderService { get; internal set; } = null!;
    public IGoodsReceiptRepository GoodsReceiptRepository { get; internal set; } = null!;
    public IGoodsReturnRepository GoodsReturnRepository { get; internal set; } = null!;
    public IProjectInventoryRepository ProjectInventoryRepository { get; internal set; } = null!;
    public IGoodsReceiptService GoodsReceiptService { get; internal set; } = null!;
    public IProcurementCommitmentSnapshotRepository ProcurementCommitmentSnapshotRepository { get; internal set; } = null!;
    public IProcurementCommitmentService ProcurementCommitmentService { get; internal set; } = null!;

    // Contract properties
    public IContractRepository ContractRepository { get; internal set; } = null!;
    public IContractVersionRepository ContractVersionRepository { get; internal set; } = null!;
    public IContractPartyRepository ContractPartyRepository { get; internal set; } = null!;
    public IContractMilestoneRepository ContractMilestoneRepository { get; internal set; } = null!;
    public IContractScopeLinkRepository ContractScopeLinkRepository { get; internal set; } = null!;
    public IContractDocumentRepository ContractDocumentRepository { get; internal set; } = null!;
    public IContractAuditRepository ContractAuditRepository { get; internal set; } = null!;
    public IContractService ContractService { get; internal set; } = null!;

    // Payment Certificate properties
    public IPaymentCertificateRepository PaymentCertificateRepository { get; internal set; } = null!;
    public IPaymentCertificateLineRepository PaymentCertificateLineRepository { get; internal set; } = null!;
    public IContractAdvanceRepository ContractAdvanceRepository { get; internal set; } = null!;
    public IAdvanceAmortizationRepository AdvanceAmortizationRepository { get; internal set; } = null!;
    public IContractRetentionRepository ContractRetentionRepository { get; internal set; } = null!;
    public IRetentionApplicationRepository RetentionApplicationRepository { get; internal set; } = null!;
    public IRetentionReleaseRepository RetentionReleaseRepository { get; internal set; } = null!;
    public IPaymentObservationRepository PaymentObservationRepository { get; internal set; } = null!;
    public IPaymentCertificateRevisionRepository PaymentCertificateRevisionRepository { get; internal set; } = null!;
    public IPaymentCertificateAuditRepository PaymentCertificateAuditRepository { get; internal set; } = null!;
    public IPaymentCertificateService PaymentCertificateService { get; internal set; } = null!;

    // Contract Change properties
    public IChangeRequestRepository ChangeRequestRepository { get; internal set; } = null!;
    public IChangeOrderRepository ChangeOrderRepository { get; internal set; } = null!;
    public IChangeOrderLineRepository ChangeOrderLineRepository { get; internal set; } = null!;
    public IExtraWorkRepository ExtraWorkRepository { get; internal set; } = null!;
    public IContractClaimRepository ContractClaimRepository { get; internal set; } = null!;
    public IContractChangeAuditRepository ContractChangeAuditRepository { get; internal set; } = null!;
    public IContractChangePermissionChecker ContractChangePermissionChecker { get; internal set; } = null!;
    public IContractChangeService ContractChangeService { get; internal set; } = null!;

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
        IVersionWorkflowService versionWorkflow,
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
        VersionWorkflow = versionWorkflow;
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
        IVersionWorkflowService versionWorkflow,
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
        VersionWorkflow = versionWorkflow;
        PriceBank = priceBank;
        PriceBankStore = priceBankStore;
    }

    public void SetPlanningServices(
        IScheduleRepository scheduleRepository,
        IWbsPlanningRepository wbsPlanningRepository,
        IActivityRepository activityRepository,
        ICalendarRepository calendarRepository,
        IPlanningAuditRepository planningAuditRepository,
        IActivityDependencyRepository activityDependencyRepository,
        ICpmCalculationService cpmCalculation,
        IScheduleBaselineRepository scheduleBaselineRepository,
        IScheduleBaselineSnapshotRepository scheduleBaselineSnapshotRepository,
        IScheduleBaselineService scheduleBaseline)
    {
        ScheduleRepository = scheduleRepository;
        WbsPlanningRepository = wbsPlanningRepository;
        ActivityRepository = activityRepository;
        CalendarRepository = calendarRepository;
        PlanningAuditRepository = planningAuditRepository;
        ActivityDependencyRepository = activityDependencyRepository;
        CpmCalculation = cpmCalculation;
        ScheduleBaselineRepository = scheduleBaselineRepository;
        ScheduleBaselineSnapshotRepository = scheduleBaselineSnapshotRepository;
        ScheduleBaseline = scheduleBaseline;
    }

    public void SetProgressServices(
        IStatusDateRepository statusDateRepository,
        IActivityProgressRepository activityProgressRepository,
        IProgressIntervalRepository progressIntervalRepository,
        IDelayReasonRepository delayReasonRepository,
        IProgressEvidenceRepository progressEvidenceRepository,
        IProgressSnapshotRepository progressSnapshotRepository,
        IProgressTrackingService progressTracking,
        IPlanningPermissionChecker planningPermissionChecker)
    {
        StatusDateRepository = statusDateRepository;
        ActivityProgressRepository = activityProgressRepository;
        ProgressIntervalRepository = progressIntervalRepository;
        DelayReasonRepository = delayReasonRepository;
        ProgressEvidenceRepository = progressEvidenceRepository;
        ProgressSnapshotRepository = progressSnapshotRepository;
        ProgressTracking = progressTracking;
        PlanningPermissionsChecker = planningPermissionChecker;
    }

    public void SetEarnedValueServices(
        IEarnedValueRepository earnedValueRepository,
        IEarnedValueSnapshotRepository earnedValueSnapshotRepository,
        IEarnedValueHistoryRepository earnedValueHistoryRepository,
        IEarnedValueService earnedValue)
    {
        EarnedValueRepository = earnedValueRepository;
        EarnedValueSnapshotRepository = earnedValueSnapshotRepository;
        EarnedValueHistoryRepository = earnedValueHistoryRepository;
        EarnedValue = earnedValue;
    }

    public void SetDashboardServices(
        IDashboardSnapshotRepository dashboardSnapshotRepository,
        IDashboardAlertRepository dashboardAlertRepository,
        IDashboardPreferenceRepository dashboardPreferenceRepository,
        IProjectDashboardService projectDashboard)
    {
        DashboardSnapshotRepository = dashboardSnapshotRepository;
        DashboardAlertRepository = dashboardAlertRepository;
        DashboardPreferenceRepository = dashboardPreferenceRepository;
        ProjectDashboard = projectDashboard;
    }

    public void SetProcurementServices(
        ISupplierRepository supplierRepository,
        ISupplierContactRepository supplierContactRepository,
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IRequestForQuotationRepository requestForQuotationRepository,
        IQuotationResponseRepository quotationResponseRepository,
        IProcurementDocumentRepository procurementDocumentRepository,
        IProcurementAuditRepository procurementAuditRepository,
        ISupplierService supplierService,
        IPurchaseRequisitionService purchaseRequisitionService,
        IRfqService rfqService,
        IProcurementPermissionChecker procurementPermissionsChecker)
    {
        SupplierRepository = supplierRepository;
        SupplierContactRepository = supplierContactRepository;
        PurchaseRequisitionRepository = purchaseRequisitionRepository;
        RequestForQuotationRepository = requestForQuotationRepository;
        QuotationResponseRepository = quotationResponseRepository;
        ProcurementDocumentRepository = procurementDocumentRepository;
        ProcurementAuditRepository = procurementAuditRepository;
        SupplierService = supplierService;
        PurchaseRequisitionService = purchaseRequisitionService;
        RfqService = rfqService;
        ProcurementPermissionsChecker = procurementPermissionsChecker;
    }

    public void SetProcurementAwardServices(
        ISupplierOfferRepository supplierOfferRepository,
        ITechnicalEvaluationRepository technicalEvaluationRepository,
        ICommercialEvaluationRepository commercialEvaluationRepository,
        IBidComparisonRepository bidComparisonRepository,
        IAwardRepository awardRepository,
        ISupplierOfferService supplierOfferService,
        IBidComparisonService bidComparisonService,
        IAwardService awardService)
    {
        SupplierOfferRepository = supplierOfferRepository;
        TechnicalEvaluationRepository = technicalEvaluationRepository;
        CommercialEvaluationRepository = commercialEvaluationRepository;
        BidComparisonRepository = bidComparisonRepository;
        AwardRepository = awardRepository;
        SupplierOfferService = supplierOfferService;
        BidComparisonService = bidComparisonService;
        AwardService = awardService;
    }

    public void SetProcurementFulfillmentServices(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderService purchaseOrderService,
        IGoodsReceiptRepository goodsReceiptRepository,
        IGoodsReturnRepository goodsReturnRepository,
        IProjectInventoryRepository projectInventoryRepository,
        IGoodsReceiptService goodsReceiptService,
        IProcurementCommitmentSnapshotRepository commitmentSnapshotRepository,
        IProcurementCommitmentService commitmentService)
    {
        PurchaseOrderRepository = purchaseOrderRepository;
        PurchaseOrderService = purchaseOrderService;
        GoodsReceiptRepository = goodsReceiptRepository;
        GoodsReturnRepository = goodsReturnRepository;
        ProjectInventoryRepository = projectInventoryRepository;
        GoodsReceiptService = goodsReceiptService;
        ProcurementCommitmentSnapshotRepository = commitmentSnapshotRepository;
        ProcurementCommitmentService = commitmentService;
    }

    public void SetContractServices(
        IContractRepository contractRepo,
        IContractVersionRepository versionRepo,
        IContractPartyRepository partyRepo,
        IContractMilestoneRepository milestoneRepo,
        IContractScopeLinkRepository scopeRepo,
        IContractDocumentRepository documentRepo,
        IContractAuditRepository auditRepo,
        IContractService contractService)
    {
        ContractRepository = contractRepo;
        ContractVersionRepository = versionRepo;
        ContractPartyRepository = partyRepo;
        ContractMilestoneRepository = milestoneRepo;
        ContractScopeLinkRepository = scopeRepo;
        ContractDocumentRepository = documentRepo;
        ContractAuditRepository = auditRepo;
        ContractService = contractService;
    }

    public void SetPaymentCertificateServices(
        IPaymentCertificateRepository paymentCertificateRepo,
        IPaymentCertificateLineRepository paymentCertificateLineRepo,
        IContractAdvanceRepository contractAdvanceRepo,
        IAdvanceAmortizationRepository advanceAmortizationRepo,
        IContractRetentionRepository contractRetentionRepo,
        IRetentionApplicationRepository retentionApplicationRepo,
        IRetentionReleaseRepository retentionReleaseRepo,
        IPaymentObservationRepository paymentObservationRepo,
        IPaymentCertificateRevisionRepository paymentCertificateRevisionRepo,
        IPaymentCertificateAuditRepository paymentCertificateAuditRepo,
        IPaymentCertificateService paymentCertificateService)
    {
        PaymentCertificateRepository = paymentCertificateRepo;
        PaymentCertificateLineRepository = paymentCertificateLineRepo;
        ContractAdvanceRepository = contractAdvanceRepo;
        AdvanceAmortizationRepository = advanceAmortizationRepo;
        ContractRetentionRepository = contractRetentionRepo;
        RetentionApplicationRepository = retentionApplicationRepo;
        RetentionReleaseRepository = retentionReleaseRepo;
        PaymentObservationRepository = paymentObservationRepo;
        PaymentCertificateRevisionRepository = paymentCertificateRevisionRepo;
        PaymentCertificateAuditRepository = paymentCertificateAuditRepo;
        PaymentCertificateService = paymentCertificateService;
    }

    public void SetContractChangeServices(
        IChangeRequestRepository changeRequestRepo,
        IChangeOrderRepository changeOrderRepo,
        IChangeOrderLineRepository changeOrderLineRepo,
        IExtraWorkRepository extraWorkRepo,
        IContractClaimRepository contractClaimRepo,
        IContractChangeAuditRepository contractChangeAuditRepo,
        IContractChangePermissionChecker permissionChecker,
        IContractChangeService contractChangeService)
    {
        ChangeRequestRepository = changeRequestRepo;
        ChangeOrderRepository = changeOrderRepo;
        ChangeOrderLineRepository = changeOrderLineRepo;
        ExtraWorkRepository = extraWorkRepo;
        ContractClaimRepository = contractClaimRepo;
        ContractChangeAuditRepository = contractChangeAuditRepo;
        ContractChangePermissionChecker = permissionChecker;
        ContractChangeService = contractChangeService;
    }

    public static AppServices CreateDefault() => new AppServicesBuilder().Build();
}
