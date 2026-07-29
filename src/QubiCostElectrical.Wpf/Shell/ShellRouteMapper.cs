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
