using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using QubiCostElectrical;

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
    private readonly Action dismiss;

    public ToastMessage(string title, string message, ToastLevel level, Action dismiss, string? actionLabel = null, Action? action = null)
    {
        Id = Guid.NewGuid();
        Title = title;
        Message = message;
        Level = level;
        CreatedAt = DateTime.UtcNow;
        this.dismiss = dismiss;
        ActionLabel = actionLabel;
        Action = action;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Message { get; }
    public ToastLevel Level { get; }
    public DateTime CreatedAt { get; }
    public string? ActionLabel { get; }
    public Action? Action { get; }
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionLabel) && Action != null;

    public string IconGlyph => Level switch
    {
        ToastLevel.Success => "✓",
        ToastLevel.Warning => "!",
        ToastLevel.Error => "×",
        _ => "i"
    };

    public void Dismiss() => dismiss();
}

public sealed class ShellNotificationService
{
    public static ShellNotificationService Instance { get; } = new();
    public static Action? RequestUndo { get; set; }

    private readonly Dictionary<Guid, DispatcherTimer> timers = new();

    public ObservableCollection<ToastMessage> Active { get; } = new();

    public void ShowInfo(string title, string message) => Enqueue(title, message, ToastLevel.Info);
    public void ShowInfo(string title, string message, string? actionLabel = null, Action? action = null) => Enqueue(title, message, ToastLevel.Info, actionLabel, action);

    public void ShowSuccess(string title, string message) => Enqueue(title, message, ToastLevel.Success);
    public void ShowSuccess(string title, string message, string? actionLabel = null, Action? action = null) => Enqueue(title, message, ToastLevel.Success, actionLabel, action);

    public void ShowWarning(string title, string message) => Enqueue(title, message, ToastLevel.Warning);
    public void ShowWarning(string title, string message, string? actionLabel = null, Action? action = null) => Enqueue(title, message, ToastLevel.Warning, actionLabel, action);

    public void ShowError(string title, string message) => Enqueue(title, message, ToastLevel.Error);
    public void ShowError(string title, string message, string? actionLabel = null, Action? action = null) => Enqueue(title, message, ToastLevel.Error, actionLabel, action);

    public void Dismiss(ToastMessage toast)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            AppLog.Warning("Notificaciones", "Toast no descartado: dispatcher de aplicación no disponible.");
            return;
        }

        dispatcher.Invoke(() =>
        {
            if (timers.Remove(toast.Id, out var timer))
                timer.Stop();
            Active.Remove(toast);
        });
    }

    public void SetPaused(ToastMessage toast, bool paused)
    {
        if (!timers.TryGetValue(toast.Id, out var timer))
            return;

        timer.IsEnabled = !paused;
    }

    private void Enqueue(string title, string message, ToastLevel level, string? actionLabel = null, Action? action = null)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            AppLog.Warning("Notificaciones", $"Toast descartado (sin dispatcher): {title} — {message}");
            return;
        }

        dispatcher.Invoke(() =>
        {
            ToastMessage? toast = null;
            toast = new ToastMessage(title, message, level, () =>
            {
                if (toast != null)
                    Dismiss(toast);
            }, actionLabel, action);

            Active.Add(toast);
            while (Active.Count > 4)
            {
                var oldest = Active[0];
                Dismiss(oldest);
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(toast.HasAction ? 8 : 4) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Dismiss(toast);
            };
            timers[toast.Id] = timer;
            timer.Start();
        });
    }
}
