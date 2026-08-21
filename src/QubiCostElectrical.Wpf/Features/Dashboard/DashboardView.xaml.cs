using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.Load();
        };
    }
}