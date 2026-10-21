using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public partial class DashboardCriticalItemsSection : UserControl
{
    public DashboardCriticalItemsSection()
    {
        InitializeComponent();
    }

    private void OnTopPartidaDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is DashboardPartidaVm partida &&
            DataContext is DashboardViewModel vm)
        {
            if (vm.GoToPartidaCommand.CanExecute(partida))
                vm.GoToPartidaCommand.Execute(partida);
        }
    }
}
