using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionScInspectorControl : UserControl
{
    private CubicacionScInspectorCoordinator? Coordinator => DataContext as CubicacionScInspectorCoordinator;

    public CubicacionScInspectorControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CubicacionScInspectorCoordinator oldCoord)
        {
            oldCoord.SelectedMaterialChanged -= OnSelectedMaterialChanged;
        }
        if (e.NewValue is CubicacionScInspectorCoordinator newCoord)
        {
            newCoord.SelectedMaterialChanged += OnSelectedMaterialChanged;
        }
    }

    private void OnSelectedMaterialChanged(ScInspectorMaterialVm? mat)
    {
        if (mat == null) return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var container = FindContainerForMaterial(this, mat);
            container?.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static FrameworkElement? FindContainerForMaterial(DependencyObject parent, ScInspectorMaterialVm target)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && ReferenceEquals(fe.DataContext, target))
                return fe;
            var desc = FindContainerForMaterial(child, target);
            if (desc != null) return desc;
        }
        return null;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Focus();
                if (Coordinator?.SelectedMaterial == null)
                {
                    Coordinator?.SelectNextMaterial();
                }
                OnSelectedMaterialChanged(Coordinator?.SelectedMaterial);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        if (Coordinator != null)
        {
            Coordinator.FilterSearchText = "";
        }
        Focus();
    }

    private void OnInspectorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Coordinator == null) return;

        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(Coordinator.FilterSearchText))
            {
                Coordinator.FilterSearchText = "";
                e.Handled = true;
                return;
            }

            Coordinator.CloseInspectorCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            Coordinator.SelectNextMaterial();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            Coordinator.SelectPreviousMaterial();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            Coordinator.TransferSelectedMaterial();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left)
        {
            if (!SearchBox.IsKeyboardFocused || SearchBox.SelectionStart == 0)
            {
                Coordinator.SelectPreviousSystem();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Right)
        {
            if (!SearchBox.IsKeyboardFocused || SearchBox.SelectionStart >= SearchBox.Text.Length)
            {
                Coordinator.SelectNextSystem();
                e.Handled = true;
                return;
            }
        }
    }
}
