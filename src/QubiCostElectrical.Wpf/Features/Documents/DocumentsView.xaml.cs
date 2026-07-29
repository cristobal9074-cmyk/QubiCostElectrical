using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Documents;

public partial class DocumentsView : UserControl
{
    public DocumentsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is DocumentsViewModel vm)
            vm.Load();
    }

    private void OnMoreActionsClick(object sender, System.Windows.RoutedEventArgs e)
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
