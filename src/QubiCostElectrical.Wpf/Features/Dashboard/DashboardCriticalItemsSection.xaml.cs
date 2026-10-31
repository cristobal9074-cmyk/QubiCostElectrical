using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

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

    private void OnDataGridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled)
        {
            e.Handled = true;
            var parent = VisualTreeHelperSafe.FindAncestor<ScrollViewer>(this);
            if (parent != null)
            {
                var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                parent.RaiseEvent(eventArg);
            }
        }
    }
}
