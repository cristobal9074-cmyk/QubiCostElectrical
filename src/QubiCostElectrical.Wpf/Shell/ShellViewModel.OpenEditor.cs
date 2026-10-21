namespace QubiCostElectrical.Wpf.Shell;

public sealed partial class ShellViewModel
{
    private bool TryCommitOpenEditor()
        => currentPageVm is not IPageWithOpenEditor open || open.TryCommitOpenEditor();

    private string PageApplyFailureMessage(string fallback)
        => currentPageVm is IPageWithOpenEditor open && !string.IsNullOrWhiteSpace(open.OpenEditorError)
            ? open.OpenEditorError
            : fallback;

    private bool TryApplyCurrentPageChanges()
    {
        if (!TryCommitOpenEditor())
            return false;

        if (currentPageVm is IPageWithPendingChanges pending &&
            pending.HasPendingChanges &&
            currentPageVm is IPageWithCommitChanges commitPage)
            return commitPage.TryApplyChanges();

        return true;
    }
}
