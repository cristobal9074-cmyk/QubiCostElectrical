using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.PaymentCertificates;

public partial class PaymentCertificateEditorView : UserControl
{
    public PaymentCertificateEditorView(PaymentCertificateEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
