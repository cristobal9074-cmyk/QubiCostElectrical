using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Contracts;

public partial class ContractWorkspaceView : UserControl
{
    public ContractWorkspaceView(ContractWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
