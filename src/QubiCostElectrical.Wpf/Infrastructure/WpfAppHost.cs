using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;
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
    services.ApuCatalogRepository = new SqliteApuCatalogRepository(priceBankPath);
    services.ApuVersionRepository = new SqliteApuVersionRepository(priceBankPath);
    services.ApuCatalog = new ApuCatalogService(services.ApuVersionRepository, new SqliteApuSnapshotRepository(priceBankPath), services.ApuCatalogRepository);
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
}
