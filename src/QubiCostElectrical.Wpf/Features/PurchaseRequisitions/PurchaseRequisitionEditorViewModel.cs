using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.PurchaseRequisitions;

public sealed class PurchaseRequisitionEditorViewModel
{
    public PurchaseRequisitionEditorViewModel(IShellNavigationHost shell, WpfAppHost host) =>
        Workspace = new PurchaseRequisitionWorkspaceViewModel(shell, host);

    public PurchaseRequisitionWorkspaceViewModel Workspace { get; }
}
