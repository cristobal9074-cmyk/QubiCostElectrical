using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Features.Dashboard;

namespace QubiCostElectrical.Wpf.Features.Inicio;

public partial class InicioView : UserControl
{
    public InicioView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.Load();
        };
    }

    private void OnRecentProjectsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selection is handled by WPF; no extra state is required here.
    }

    private void OnRecentProjectsMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedRecentProject();
        e.Handled = true;
    }

    private void OnRecentProjectsPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        OpenSelectedRecentProject();
        e.Handled = true;
    }

    private void OpenSelectedRecentProject()
    {
        if (DataContext is not DashboardViewModel vm)
            return;

        if (RecentProjectsList.SelectedItem is not DashboardRecentProjectVm project)
            return;

        if (string.IsNullOrWhiteSpace(project.Path))
            return;

        if (vm.OpenRecentProjectCommand.CanExecute(project.Path))
            vm.OpenRecentProjectCommand.Execute(project.Path);
    }

    private void OnProjectOptionsButtonClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement fe)
        {
            System.Windows.DependencyObject current = fe;
            while (current != null && current is not Grid { ContextMenu: not null })
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            if (current is Grid { ContextMenu: not null } parentGrid)
            {
                parentGrid.ContextMenu.PlacementTarget = fe;
                parentGrid.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                parentGrid.ContextMenu.IsOpen = true;
            }
        }
        e.Handled = true;
    }
}
