using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Controls;

public partial class UndoRedoHudHost : UserControl
{
    public UndoRedoHudHost()
    {
        InitializeComponent();
        DataContext = UndoRedoHudCoordinator.Instance;
    }

    private void OnHudMouseEnter(object sender, MouseEventArgs e)
    {
        UndoRedoHudCoordinator.Instance.SetPaused(true);
    }

    private void OnHudMouseLeave(object sender, MouseEventArgs e)
    {
        UndoRedoHudCoordinator.Instance.SetPaused(false);
    }
}
