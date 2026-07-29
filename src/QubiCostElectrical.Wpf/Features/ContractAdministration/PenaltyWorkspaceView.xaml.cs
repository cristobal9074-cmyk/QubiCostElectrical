using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractAdministration;

public partial class PenaltyWorkspaceView : UserControl
{
    public PenaltyWorkspaceView(PenaltyWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
