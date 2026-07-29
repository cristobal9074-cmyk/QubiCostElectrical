using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.EstadoPago;

public partial class EstadoPagoView : UserControl
{
    public EstadoPagoView(EstadoPagoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
