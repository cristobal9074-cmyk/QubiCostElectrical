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
    public IPriceBankStore PriceBankStore => Services.PriceBankStore;
    public IUserPreferencesStore UserPreferencesStore => Services.UserPreferencesStore;
    public IBudgetCalculationService BudgetCalculation => Services.BudgetCalculation;
    public IFinalReviewService FinalReview => Services.FinalReview;
    public IEconomicCalculationService EconomicCalculation => Services.EconomicCalculation;
    public IProjectDbContext ProjectDb => Services.ProjectDb;
    public IMaterialControlService MaterialControl => Services.MaterialControl;
    public IPurchaseListService PurchaseList => Services.PurchaseList;
    public IPurchaseListExcelExporter PurchaseListExcel => Services.PurchaseListExcel;
    public IMaterialSearchService MaterialSearch => Services.MaterialSearch;
    public IApuService Apu => Services.Apu;
    public ICubicacionService CubicacionService => Services.Cubicacion;
    public ICubicacionTakeoffQueryService CubicacionTakeoffQuery => Services.CubicacionTakeoffQuery;
    public ICubicacionSCReportExcelExporter CubicacionSCReportExcelExporter => Services.CubicacionSCReportExcelExporter;
    public ICubicacionReportExcelExporter CubicacionReportExcelExporter => Services.CubicacionReportExcelExporter;
    public ICubicacionReportPdfExporter CubicacionReportPdfExporter => Services.CubicacionReportPdfExporter;
    public IProjectRepository ProjectRepository => Services.ProjectRepository;
    public IVersionComparisonService VersionComparison => Services.VersionComparison;
    public IBudgetRevisionDiffPdfExporter RevisionDiffPdfExporter => Services.RevisionDiffPdfExporter;
    public IBudgetRevisionDiffExcelExporter RevisionDiffExcelExporter => Services.RevisionDiffExcelExporter;
    public IItemizadoSpreadsheetReader ItemizadoSpreadsheet => Services.ItemizadoSpreadsheet;
    public ImportarItemizadoUseCase ImportItemizado => Services.ImportItemizado;
    public IPdfProjectExporter PdfExporter => Services.PdfExporter;
    public IOfferLetterPdfExporter OfferLetterPdfExporter => Services.OfferLetterPdfExporter;
    public IExcelProjectExporter ExcelExporter => Services.ExcelExporter;
    public IPresentationLetterDocxExporter WordLetterExporter => Services.WordLetterExporter;
    public PriceBankApplicationService PriceBank => Services.PriceBank;

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

    public static WpfAppHost Initialize(string? logsFolderOverride = null, string? priceBankPathOverride = null, IAuthService? authOverride = null)
    {
        Logger = new FileLogger(new LogConfiguration(logsFolderOverride));
        var provider = CompositionRoot.Build(priceBankPathOverride, authOverride);
        Provider = provider;
        return provider.GetRequiredService<WpfAppHost>();
    }
}
