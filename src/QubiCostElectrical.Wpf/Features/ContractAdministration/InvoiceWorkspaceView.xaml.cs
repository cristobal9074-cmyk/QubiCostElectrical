using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ContractAdministration;

public partial class InvoiceWorkspaceView : UserControl
{
    public InvoiceWorkspaceView(InvoiceWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
