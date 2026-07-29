using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.PurchaseOrders;

public partial class PurchaseOrderWorkspaceView : UserControl
{
    public PurchaseOrderWorkspaceView(PurchaseOrderWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
