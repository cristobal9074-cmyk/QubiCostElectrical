using Microsoft.Extensions.DependencyInjection;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class WpfAppHost
{
  public static WpfAppHost Current { get; private set; } = null!;

  /// <summary>Contenedor DI de la aplicación. Se fija en <see cref="Initialize"/>.</summary>
  public static System.IServiceProvider? Provider { get; private set; }

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

  public static FileLogger? Logger { get; internal set; }

  /// <summary>Usado por AddWpfServices; también fija <see cref="Current"/>.</summary>
  internal static WpfAppHost Create(
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
    Current = new WpfAppHost(
        session, services, projectWorkspace, documents, projectEdit,
        dashboard, itemizado, cubicacion, export, itemizadoWorkspace,
        fileDialog, fileOpener, auth, projectLock, audit, permissions);
    return Current;
  }

  public static WpfAppHost Initialize(string? logsFolderOverride = null, string? priceBankPathOverride = null, AuthService? authOverride = null)
  {
    Logger = new FileLogger(new LogConfiguration(logsFolderOverride));
    var provider = CompositionRoot.Build(priceBankPathOverride, authOverride);
    Provider = provider;
    return provider.GetRequiredService<WpfAppHost>();
  }
}
