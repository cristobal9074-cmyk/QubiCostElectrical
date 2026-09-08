using System;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Contrato estándar de pantalla de workspace: empty / loading / edición.
/// Ver <c>docs/UI_SCREEN_CONTRACT.md</c>.
/// </summary>
public abstract class WorkspacePageViewModelBase : SessionAwareViewModelBase
{
    private bool showEmptyState;
    private bool isLoading;
    private bool canEdit = true;

    protected WorkspacePageViewModelBase(WpfAppHost host)
        : base(host)
    {
        Host = host;
        Session = host.Session;
        Workspace = host.ProjectWorkspace;
    }

    protected WorkspacePageViewModelBase(BudgetProjectSession session, IProjectWorkspaceService workspace)
        : base(session)
    {
        Host = null!;
        Session = session;
        Workspace = workspace;
    }

    /// <summary>Fachada histórica. null en ViewModels ya migrados a inyección directa.</summary>
    protected WpfAppHost Host { get; }
    protected BudgetProjectSession Session { get; }
    protected IProjectWorkspaceService Workspace { get; }

    public bool ShowEmptyState
    {
        get => showEmptyState;
        protected set
        {
            if (!SetProperty(ref showEmptyState, value))
                return;
            OnWorkspaceGateChanged();
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        protected set => SetProperty(ref isLoading, value);
    }

    public bool CanEdit
    {
        get => canEdit;
        protected set => SetProperty(ref canEdit, value);
    }

    /// <summary>Sincroniza empty + RO tras abrir/cerrar proyecto o cambio de lock.</summary>
    protected virtual void SyncWorkspaceGate()
    {
        ShowEmptyState = !Workspace.HasOpenProject;
        CanEdit = !ShowEmptyState && !Session.IsReadOnly;
        OnWorkspaceGateChanged();
    }

    protected virtual void OnWorkspaceGateChanged()
    {
    }
}
