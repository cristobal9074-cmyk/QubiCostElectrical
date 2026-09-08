using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Controls;

public partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
        ToastItems.ItemsSource = ShellNotificationService.Instance.Active;
    }

    private void OnDismissToastClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ToastMessage toast })
            ShellNotificationService.Instance.Dismiss(toast);
    }

    private void OnToastActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ToastMessage toast })
            return;

        try
        {
            toast.Action?.Invoke();
        }
        finally
        {
            ShellNotificationService.Instance.Dismiss(toast);
        }
    }

    private void OnToastMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToastMessage toast })
            ShellNotificationService.Instance.SetPaused(toast, paused: true);
    }

    private void OnToastMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ToastMessage toast })
            ShellNotificationService.Instance.SetPaused(toast, paused: false);
    }
}
