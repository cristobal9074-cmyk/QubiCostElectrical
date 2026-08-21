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

    private void OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        if (e.EditAction == DataGridEditAction.Cancel) return;
        // Prioridad Normal: con Background, ediciones seguidas dejaban refrescos
        // acumulados en cola hasta que algo forzaba al dispatcher a vaciarla.
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => Vm.OnRowEdited());
    }
}
