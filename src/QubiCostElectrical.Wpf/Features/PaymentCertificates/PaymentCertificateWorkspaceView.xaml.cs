using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.PaymentCertificates;

public partial class PaymentCertificateWorkspaceView : UserControl
{
    public PaymentCertificateWorkspaceView(PaymentCertificateWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
