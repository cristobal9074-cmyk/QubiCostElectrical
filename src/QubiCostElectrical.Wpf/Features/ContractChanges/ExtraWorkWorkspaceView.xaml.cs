using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractChanges;

public partial class ExtraWorkWorkspaceView : UserControl
{
    public ExtraWorkWorkspaceView(ExtraWorkWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
