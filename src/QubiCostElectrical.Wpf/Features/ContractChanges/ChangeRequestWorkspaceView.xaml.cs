using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractChanges;

public partial class ChangeRequestWorkspaceView : UserControl
{
    public ChangeRequestWorkspaceView(ChangeRequestWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
