using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractChanges;

public partial class ClaimWorkspaceView : UserControl
{
    public ClaimWorkspaceView(ClaimWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
