using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class WpfAppHost
{
  public static WpfAppHost Current { get; private set; } = null!;

  public BudgetProjectSession Session { get; }
  public AppServices Services { get; }
  public IProjectWorkspaceService ProjectWorkspace { get; }
  public IDocumentsWorkspaceService Documents { get; }
  public IProjectEditWorkspaceService ProjectEdit { get; }
  public DashboardPresenter Dashboard { get; }
  public ItemizadoPresenter Itemizado { get; }
  public CubicacionPresenter Cubicacion { get; }
  public ExportPresenter Export { get; }
  public IItemizadoWorkspaceService ItemizadoWorkspace { get; }
  public ICurrencyConversionService Currency { get; }
  public IFileDialogService FileDialog { get; }
  public IFileOpenService FileOpener { get; }
  public WpfUndoService UndoService { get; }
  public IAuthService Auth { get; }
  public ProjectCollaborationLockService ProjectLock { get; }
  public ProjectAuditRecorder Audit { get; }
  public IPermissionService Permissions { get; }

  private WpfAppHost(
      BudgetProjectSession session,
      AppServices services,
      IProjectWorkspaceService projectWorkspace,
      IDocumentsWorkspaceService documents,
      IProjectEditWorkspaceService projectEdit,
      DashboardPresenter dashboard,
      ItemizadoPresenter itemizado,
      CubicacionPresenter cubicacion,
      ExportPresenter export,
      IItemizadoWorkspaceService itemizadoWorkspace,
      IFileDialogService fileDialog,
      IFileOpenService fileOpener,
      IAuthService auth,
      ProjectCollaborationLockService projectLock,
      ProjectAuditRecorder audit,
      IPermissionService permissions)
  {
    Session = session;
    Services = services;
    ProjectWorkspace = projectWorkspace;
    Documents = documents;
    ProjectEdit = projectEdit;
    Dashboard = dashboard;
    Itemizado = itemizado;
    Cubicacion = cubicacion;
    Export = export;
    ItemizadoWorkspace = itemizadoWorkspace;
    Currency = services.CurrencyConversion;
    FileDialog = fileDialog;
    FileOpener = fileOpener;
    UndoService = new WpfUndoService();
    Auth = auth;
    ProjectLock = projectLock;
    Audit = audit;
    Permissions = permissions;
  }

  public static FileLogger? Logger { get; private set; }

  public static WpfAppHost Initialize(string? logsFolderOverride = null, string? priceBankPathOverride = null, AuthService? authOverride = null)
  {
    Logger = new FileLogger(new LogConfiguration(logsFolderOverride));
    var prefs = UserPreferencesStore.Load();
    var priceBankPath = priceBankPathOverride ?? ProductPaths.ResolvePriceBankDatabaseFile(prefs.PriceBankDatabasePath);
    var services = new AppServicesBuilder()
        .WithPriceBankStore(new PriceBankStore(priceBankPath))
        .Build();
    var session = new BudgetProjectSession();
    var projectWorkspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);
    var documents = new DocumentsWorkspaceService(session);
    var projectEdit = new ProjectEditWorkspaceService(session);
    var dashboardServices = new DashboardPresenterServices(
        services.BudgetCalculation,
        services.PartidaCompletion,
        services.FinalReview,
        new DashboardExecutiveService());
    var dashboard = new DashboardPresenter(dashboardServices, session);
    var itemizado = ItemizadoPresenterFactory.CreatePresenter(services, session);
    var cubicacion = PresenterFactory.CreateCubicacionPresenter(services, session);
    var export = PresenterFactory.CreateExportPresenter(services, session);
    var itemizadoWorkspace = new ItemizadoWorkspaceService();
    var fileDialog = new WpfFileDialogService();
    var fileOpener = new WpfFileOpenService();
    var auth = authOverride ?? new AuthService();
    var permissions = new UserRolePermissionService();
    var projectLock = new ProjectCollaborationLockService();
    var audit = new ProjectAuditRecorder(session);
    SessionManager.BindPermissions(permissions);
    Current = new WpfAppHost(session, services, projectWorkspace, documents, projectEdit, dashboard, itemizado, cubicacion, export, itemizadoWorkspace, fileDialog, fileOpener, auth, projectLock, audit, permissions);
    return Current;
  }

  public void InitializePlanningRepositories(string projectPath)
  {
      if (string.IsNullOrWhiteSpace(projectPath)) return;
      var dbPath = Services.ProjectRepository.GetSQLiteDatabasePathForProject(projectPath, AppDomain.CurrentDomain.BaseDirectory, "Project");
      
      var schedRepo = new SqliteScheduleRepository(dbPath);
      var wbsRepo = new SqliteWbsPlanningRepository(dbPath);
      var actRepo = new SqliteActivityRepository(dbPath);
      var calRepo = new SqliteCalendarRepository(dbPath);
      var auditRepo = new SqlitePlanningAuditRepository(dbPath);
      var depRepo = new SqliteActivityDependencyRepository(dbPath);
      var calCalc = new CalendarCalculationService();
      
      var cpm = new CpmCalculationService(
          schedRepo,
          actRepo,
          depRepo,
          calRepo,
          calCalc,
          auditRepo
      );

      var baselineRepo = new SqliteScheduleBaselineRepository(dbPath);
      var snapshotRepo = new SqliteScheduleBaselineSnapshotRepository(dbPath);
      var baselineService = new ScheduleBaselineService(
          baselineRepo,
          snapshotRepo,
          schedRepo,
          actRepo,
          depRepo,
          calRepo,
          auditRepo
      );

      Services.SetPlanningServices(schedRepo, wbsRepo, actRepo, calRepo, auditRepo, depRepo, cpm, baselineRepo, snapshotRepo, baselineService);

      var statusDateRepo = new SqliteStatusDateRepository(dbPath);
      var progressRepo = new SqliteActivityProgressRepository(dbPath);
      var intervalRepo = new SqliteProgressIntervalRepository(dbPath);
      var delayRepo = new SqliteDelayReasonRepository(dbPath);
      var evidenceRepo = new SqliteProgressEvidenceRepository(dbPath);
      var progressSnapRepo = new SqliteProgressSnapshotRepository(dbPath);
      var permissionChecker = new AllowAllPlanningPermissionChecker();
      var progressService = new ProgressTrackingService(
          statusDateRepo,
          progressRepo,
          intervalRepo,
          delayRepo,
          evidenceRepo,
          progressSnapRepo,
          schedRepo,
          actRepo,
          depRepo,
          auditRepo,
          permissionChecker,
          ProgressPolicy.Default,
          cpm);
      Services.SetProgressServices(
          statusDateRepo, progressRepo, intervalRepo, delayRepo, evidenceRepo, progressSnapRepo, progressService, permissionChecker);

      var evmRepo = new SqliteEarnedValueRepository(dbPath);
      var evmSnapRepo = new SqliteEarnedValueSnapshotRepository(dbPath);
      var evmHistRepo = new SqliteEarnedValueHistoryRepository(dbPath);
      var evmService = new EarnedValueService(
          evmRepo, evmSnapRepo, evmHistRepo,
          schedRepo, actRepo, progressRepo, statusDateRepo, baselineRepo,
          auditRepo, permissionChecker, progressService);
      Services.SetEarnedValueServices(evmRepo, evmSnapRepo, evmHistRepo, evmService);

      var dashSnapRepo = new SqliteDashboardSnapshotRepository(dbPath);
      var dashAlertRepo = new SqliteDashboardAlertRepository(dbPath);
      var dashPrefRepo = new SqliteDashboardPreferenceRepository(dbPath);
      var dashService = new ProjectDashboardService(
          schedRepo, actRepo, wbsRepo, statusDateRepo, progressRepo, baselineRepo,
          evmService, dashSnapRepo, dashAlertRepo, dashPrefRepo, auditRepo, permissionChecker, progressService);
      Services.SetDashboardServices(dashSnapRepo, dashAlertRepo, dashPrefRepo, dashService);

      var procPerm = new AllowAllProcurementPermissionChecker();
      var supplierRepo = new SqliteSupplierRepository(dbPath);
      var contactRepo = new SqliteSupplierContactRepository(dbPath);
      var prRepo = new SqlitePurchaseRequisitionRepository(dbPath);
      var rfqRepo = new SqliteRequestForQuotationRepository(dbPath);
      var qrRepo = new SqliteQuotationResponseRepository(dbPath);
      var procDocRepo = new SqliteProcurementDocumentRepository(dbPath);
      var procAuditRepo = new SqliteProcurementAuditRepository(dbPath);
      var supplierSvc = new SupplierService(supplierRepo, contactRepo, procAuditRepo, procPerm);
      var prSvc = new PurchaseRequisitionService(prRepo, procAuditRepo, procPerm);
      var rfqSvc = new RfqService(rfqRepo, prRepo, supplierRepo, contactRepo, qrRepo, procAuditRepo, procPerm);
      Services.SetProcurementServices(
          supplierRepo, contactRepo, prRepo, rfqRepo, qrRepo, procDocRepo, procAuditRepo,
          supplierSvc, prSvc, rfqSvc, procPerm);

      var offerRepo = new SqliteSupplierOfferRepository(dbPath);
      var techRepo = new SqliteTechnicalEvaluationRepository(dbPath);
      var comRepo = new SqliteCommercialEvaluationRepository(dbPath);
      var cmpRepo = new SqliteBidComparisonRepository(dbPath);
      var awardRepo = new SqliteAwardRepository(dbPath);
      var offerSvc = new SupplierOfferService(offerRepo, techRepo, comRepo, rfqRepo, procAuditRepo, procPerm);
      var cmpSvc = new BidComparisonService(cmpRepo, rfqRepo, offerRepo, techRepo, comRepo, procAuditRepo, procPerm);
      var awardSvc = new AwardService(awardRepo, cmpRepo, offerRepo, procAuditRepo, procPerm);
      Services.SetProcurementAwardServices(offerRepo, techRepo, comRepo, cmpRepo, awardRepo, offerSvc, cmpSvc, awardSvc);

      var poRepo = new SqlitePurchaseOrderRepository(dbPath);
      var grRepo = new SqliteGoodsReceiptRepository(dbPath);
      var retRepo = new SqliteGoodsReturnRepository(dbPath);
      var invRepo = new SqliteProjectInventoryRepository(dbPath);
      var commitSnapRepo = new SqliteProcurementCommitmentSnapshotRepository(dbPath);
      var poSvc = new PurchaseOrderService(poRepo, awardRepo, procAuditRepo, procPerm);
      var grSvc = new GoodsReceiptService(grRepo, retRepo, invRepo, poRepo, procAuditRepo, procPerm);
      var commitSvc = new ProcurementCommitmentService(
          prRepo, offerRepo, awardRepo, poRepo, grRepo, rfqRepo, commitSnapRepo, procAuditRepo, procPerm);
      Services.SetProcurementFulfillmentServices(poRepo, poSvc, grRepo, retRepo, invRepo, grSvc, commitSnapRepo, commitSvc);

      var coRepo = new SqliteProjectCloseoutRepository(dbPath);
      var coChecklistRepo = new SqliteCloseoutChecklistRepository(dbPath);
      var coPunchListRepo = new SqlitePunchListRepository(dbPath);
      var coNcRepo = new SqliteCloseoutNonConformanceStore(dbPath);
      var coDocRepo = new SqliteCloseoutDocumentRepository(dbPath);
      var coEvidenceRepo = new SqliteCloseoutEvidenceRepository(dbPath);
      var coSnapshotRepo = new SqliteCloseoutSnapshotRepository(dbPath);
      var coAlertRepo = new SqliteCloseoutAlertRepository(dbPath);
      var coAudit = new SqliteCloseoutAuditRepository(dbPath);
      var coPerm = new CloseoutPermissionChecker(Permissions.CurrentRole);
      Services.SetCloseoutServices(coRepo, coChecklistRepo, coPunchListRepo, coNcRepo, coDocRepo, coEvidenceRepo, coSnapshotRepo, coAlertRepo, coAudit, coPerm, null!, null!, null!, null!, null!, null!);

      var coReception = new SqliteProjectReceptionStore(dbPath);
      var coRecord = new SqliteReceptionRecordStore(dbPath);
      var coAsBuilt = new SqliteAsBuiltStore(dbPath);
      var coGuarantee = new SqliteGuaranteeReleaseStore(dbPath);
      var coRetention = new SqliteRetentionReleaseStore(dbPath);
      var coInventory = new SqliteInventoryCloseoutStore(dbPath);
      var coLesson = new SqliteLessonLearnedStore(dbPath);
      var coFinal = new SqliteFinalCloseoutStore(dbPath);
      var coFinalPerm = new CloseoutFinalPermissionChecker(Permissions.CurrentRole);
      var coReceptionSvc = new ProjectReceptionService(coReception);
      var coHandoverSvc = new HandoverService(coRecord, coAsBuilt);
      var coReleaseSvc = new CloseoutReleaseService(coGuarantee, coRetention);
      var coInventorySvc = new InventoryCloseoutService(coInventory);
      var coLessonSvc = new LessonLearnedService(coLesson);
      var coFinalSvc = new FinalCloseoutService(coRepo, coFinal, coAudit);
      var coArchiveSvc = new ProjectArchiveService(coFinal);
      Services.SetCloseoutFinalServices(coReception, coRecord, coAsBuilt, coGuarantee, coRetention, coInventory, coLesson, coFinal, coFinal, coFinalPerm, coReceptionSvc, coHandoverSvc, coReleaseSvc, coInventorySvc, coLessonSvc, coFinalSvc, coArchiveSvc);
  }
}
