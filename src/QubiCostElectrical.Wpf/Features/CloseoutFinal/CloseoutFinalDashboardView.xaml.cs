using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.CloseoutFinal;

public partial class CloseoutFinalDashboardView : UserControl
{
    public CloseoutFinalDashboardView(CloseoutFinalDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
