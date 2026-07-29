using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Closeout;

public partial class CloseoutWorkspaceView : UserControl
{
    public CloseoutWorkspaceView(CloseoutWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
