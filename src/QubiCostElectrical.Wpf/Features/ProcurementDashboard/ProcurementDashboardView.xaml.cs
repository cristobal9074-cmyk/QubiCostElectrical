using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.ProcurementDashboard;

public partial class ProcurementDashboardView : UserControl
{
    public ProcurementDashboardView(ProcurementDashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
