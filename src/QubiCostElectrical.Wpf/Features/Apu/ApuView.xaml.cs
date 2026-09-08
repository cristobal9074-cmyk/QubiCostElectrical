using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Apu;

public partial class ApuView : UserControl
{
    public ApuView(ApuWorkspaceVm vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    internal ApuWorkspaceVm Vm => (ApuWorkspaceVm)DataContext;

    public void ApplyNavContext(ApuNavContext ctx)
    {
        Vm.NavigateToItem(ctx.ItemId);
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

    private void OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel)
        {
            Vm.ClearPendingUndoForEdit();
            return;
        }
        // Prioridad Normal: con Background, ediciones seguidas dejaban refrescos
        // acumulados en cola hasta que algo forzaba al dispatcher a vaciarla.
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            Vm.OnRowEdited();
            Vm.ClearPendingUndoForEdit();
        });
    }

    private void OnUserControlKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.G)
        {
            if (Vm.OpenQuickJumpCommand.CanExecute(null))
            {
                Vm.OpenQuickJumpCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
