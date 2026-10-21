using QubiCostElectrical;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Shell;

public interface IPageWithPendingChanges
{
    bool HasPendingChanges { get; }
    bool TryDiscardChanges();
}

public interface IPageWithCommitChanges
{
    bool TryApplyChanges();
}

/// <summary>
/// La pantalla tiene un editor que todavía no escribió al modelo.
/// False cancela navegación, guardado y cierre, y deja el foco en esa celda.
/// </summary>
public interface IPageWithOpenEditor
{
    bool TryCommitOpenEditor();
    string OpenEditorError { get; }
}

public static class ShellRouteMapper
{
    public static ShellRoute FromReviewTarget(WorkspaceReviewTarget target) => target switch
    {
        WorkspaceReviewTarget.Project => ShellRoute.Project,
        WorkspaceReviewTarget.Documents => ShellRoute.Documents,
        WorkspaceReviewTarget.Itemized => ShellRoute.Itemized,
        WorkspaceReviewTarget.Takeoff => ShellRoute.Takeoff,
        WorkspaceReviewTarget.Apu => ShellRoute.Apu,
        WorkspaceReviewTarget.Prices => ShellRoute.Prices,
        _ => ShellRoute.Review
    };
}
