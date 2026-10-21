using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc.Controls;

public partial class CubicacionScTopBarControl : UserControl
{
    public CubicacionScTopBarControl()
    {
        InitializeComponent();
    }

    private void OnMoreActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }
    }
}
