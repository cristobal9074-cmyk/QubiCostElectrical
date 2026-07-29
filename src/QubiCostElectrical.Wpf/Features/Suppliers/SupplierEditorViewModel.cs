using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Suppliers;

public sealed class SupplierEditorViewModel
{
    public SupplierEditorViewModel(IShellNavigationHost shell, WpfAppHost host) =>
        Workspace = new SupplierWorkspaceViewModel(shell, host);

    public SupplierWorkspaceViewModel Workspace { get; }
}
