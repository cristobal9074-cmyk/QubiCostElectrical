using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

public enum ToastLevel
{
    Info,
    Warning,
    Error,
    Success
}

public sealed class ToastMessage
{
    public ToastMessage(string title, string message, ToastLevel level)
    {
        Id = Guid.NewGuid();
        Title = title;
        Message = message;
        Level = level;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Message { get; }
    public ToastLevel Level { get; }
    public DateTime CreatedAt { get; }
}

public sealed class ShellNotificationService
{
    public static ShellNotificationService Instance { get; } = new();

    public ObservableCollection<ToastMessage> Active { get; } = new();

    public void ShowInfo(string title, string message) => Enqueue(title, message, ToastLevel.Info);

    public void ShowSuccess(string title, string message) => Enqueue(title, message, ToastLevel.Success);

    public void ShowWarning(string title, string message) => Enqueue(title, message, ToastLevel.Warning);

    public void ShowError(string title, string message) => Enqueue(title, message, ToastLevel.Error);

    private void Enqueue(string title, string message, ToastLevel level)
    {
        var toast = new ToastMessage(title, message, level);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
            return;

        dispatcher.Invoke(() =>
        {
            Active.Add(toast);
            while (Active.Count > 4)
                Active.RemoveAt(0);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Active.Remove(toast);
            };
            timer.Start();
        });
    }
}
