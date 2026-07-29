using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.PaymentCertificates;

public partial class AdvanceRetentionWorkspaceView : UserControl
{
    public AdvanceRetentionWorkspaceView(AdvanceRetentionWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
