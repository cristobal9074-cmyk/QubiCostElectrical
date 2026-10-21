using System;
using System.IO;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Shell;

/// <summary>
/// Operaciones de ciclo de vida del proyecto abiertas (sin diálogos de UI).
/// El shell sigue dueño de confirmaciones y navegación post-cierre.
/// </summary>
internal sealed class ShellProjectLifecycle
{
    private readonly BudgetProjectSession session;
    private readonly IProjectWorkspaceService projectWorkspace;
    private readonly WpfUndoService undo;
    private readonly IBudgetCalculationService budgetCalculation;
    private readonly IProjectEditWorkspaceService projectEdit;
    private readonly IDocumentsWorkspaceService documents;
    private readonly ProjectCollaborationLockService projectLock;
    private readonly ProjectAuditRecorder audit;
    private readonly IPermissionService permissions;
    private readonly AutoSaveService autoSave;
    private readonly IUserPreferencesStore preferencesStore;
    private readonly Action clearPageCache;

    public ShellProjectLifecycle(
        BudgetProjectSession session,
        IProjectWorkspaceService projectWorkspace,
        WpfUndoService undo,
        IBudgetCalculationService budgetCalculation,
        IProjectEditWorkspaceService projectEdit,
        IDocumentsWorkspaceService documents,
        ProjectCollaborationLockService projectLock,
        ProjectAuditRecorder audit,
        IPermissionService permissions,
        AutoSaveService autoSave,
        Action clearPageCache,
        IUserPreferencesStore? preferencesStore = null)
    {
        this.session = session;
        this.projectWorkspace = projectWorkspace;
        this.undo = undo;
        this.budgetCalculation = budgetCalculation;
        this.projectEdit = projectEdit;
        this.documents = documents;
        this.projectLock = projectLock;
        this.audit = audit;
        this.permissions = permissions;
        this.autoSave = autoSave;
        this.clearPageCache = clearPageCache;
        this.preferencesStore = preferencesStore ?? WpfAppHost.Current?.UserPreferencesStore ?? new InMemoryUserPreferencesStore();
    }

    public void PrepareWorkspaceForNewOrOpen()
    {
        undo.Clear();
        budgetCalculation.ResetIncrementalCache();
        clearPageCache();
    }

    public async Task PersistOpenProjectAsync()
    {
        await projectWorkspace.SaveAsync().ConfigureAwait(true);
        AutoSaveService.DeleteAutoSave(session.CurrentProjectPath);
        autoSave.MarkSaved();
    }

    public void AfterProjectMaterialized(string? recentPath = null, Action? addRecent = null)
    {
        projectEdit.Discard();
        documents.DiscardChanges();
        projectLock.ReleaseLock();
        audit.SetUser(CollaborationSessionBridge.CurrentUserContext());
        audit.Enable();
        if (projectLock.TryAcquireLock(session.CurrentProjectPath, CollaborationSessionBridge.CurrentUserContext()))
            audit.RecordLifecycle("BloqueoAdquirido", recentPath ?? session.CurrentProjectPath);
        autoSave.Start();
        RoleSessionSync.Apply(session, permissions.CurrentRole);
        addRecent?.Invoke();
    }

    /// <summary>
    /// Registra el evento de cierre en la auditoría ANTES de persistir el .qce.
    /// Debe llamarse justo antes de <see cref="PersistOpenProjectAsync"/>: la entrada
    /// se añade al buffer en memoria del proyecto y se escribe con el snapshot.
    /// </summary>
    public void RecordCloseBeforePersist()
        => audit.RecordLifecycle("Cerrar", session.CurrentProjectPath);

    /// <summary>
    /// Limpia la sesión en memoria tras haber persistido el cierre. Ya no registra
    /// eventos de auditoría: lo que se grabe aquí se descarta con <c>NewProject()</c>.
    /// </summary>
    public void TearDownOpenProject()
    {
        autoSave.Stop();
        audit.Disable();
        projectLock.ReleaseLock();
        session.SetLockReadOnly(false);
        undo.Clear();
        // La caché incremental de cálculos vive en memoria entre proyectos: sin este
        // reset el primer recálculo del proyecto siguiente servía resultados del anterior.
        budgetCalculation.ResetIncrementalCache();
        clearPageCache();
        var prefs = preferencesStore.Load();
        session.NewProject(Environment.UserName, defaultGastosGeneralesPct: prefs.DefaultGastosGeneralesPct, defaultUtilidadPct: prefs.DefaultUtilidadPct, defaultMermasPct: prefs.DefaultMermasPct, defaultHerramientasPct: prefs.DefaultHerramientasPct, defaultMoneda: prefs.DefaultMoneda);
        projectEdit.Discard();
        documents.DiscardChanges();
        RoleSessionSync.Apply(session, permissions.CurrentRole);
    }

    public void ReleaseLockForAppExit()
    {
        autoSave.Stop();
        projectLock.ReleaseLock();
    }
}
