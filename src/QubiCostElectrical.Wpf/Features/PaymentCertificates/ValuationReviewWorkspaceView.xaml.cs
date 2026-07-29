using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.PaymentCertificates;

public partial class ValuationReviewWorkspaceView : UserControl
{
    public ValuationReviewWorkspaceView(ValuationReviewWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
