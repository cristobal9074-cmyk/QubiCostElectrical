using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Rfq;

public sealed class RfqEditorViewModel
{
    public RfqEditorViewModel(IShellNavigationHost shell, WpfAppHost host) =>
        Workspace = new RfqWorkspaceViewModel(shell, host);

    public RfqWorkspaceViewModel Workspace { get; }
}
