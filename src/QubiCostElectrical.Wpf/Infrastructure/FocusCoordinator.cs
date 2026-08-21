using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Centraliza el foco inicial y su restauración en vistas de trabajo.
/// </summary>
public static class FocusCoordinator
{
    public static void FocusWhenLoaded(FrameworkElement owner, Control target)
    {
        if (owner.IsLoaded)
        {
            QueueFocus(target);
            return;
        }

        RoutedEventHandler? handler = null;
        handler = (_, _) =>
        {
            owner.Loaded -= handler;
            QueueFocus(target);
        };
        owner.Loaded += handler;
    }

    public static void Restore(Control target)
    {
        if (!target.IsVisible || !target.IsEnabled)
            return;

        QueueFocus(target);
    }

    private static void QueueFocus(Control target)
    {
        target.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (!target.IsVisible || !target.IsEnabled)
                return;

            target.Focus();
            if (target is TextBox textBox)
                textBox.SelectAll();
            Keyboard.Focus(target);
        }));
    }
}
