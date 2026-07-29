using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractAdministration;

public partial class GuaranteeWorkspaceView : UserControl
{
    public GuaranteeWorkspaceView(GuaranteeWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
