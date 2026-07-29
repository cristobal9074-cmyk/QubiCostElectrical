using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractAdministration;

public partial class AdjustmentWorkspaceView : UserControl
{
    public AdjustmentWorkspaceView(AdjustmentWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
