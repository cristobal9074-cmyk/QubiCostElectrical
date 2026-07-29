using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.GoodsReceipts;

public partial class GoodsReceiptWorkspaceView : UserControl
{
    public GoodsReceiptWorkspaceView(GoodsReceiptWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
