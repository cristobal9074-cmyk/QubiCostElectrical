using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Coordinador de estado y ciclo de vida del HUD flotante de Deshacer/Rehacer.
/// Gestiona la visibilidad del pill no invasivo, temporizador de auto-descarte,
/// pausa en hover y la acción de reversión en 1 clic.
/// </summary>
public sealed class UndoRedoHudCoordinator : INotifyPropertyChanged
{
    public static UndoRedoHudCoordinator Instance { get; } = new();

    private readonly DispatcherTimer timer;
    private bool isVisible;
    private string title = "";
    private string description = "";
    private string glyph = "↩";
    private string actionLabel = "Rehacer";
    private string actionToolTip = "Rehacer la acción (Ctrl+Y)";
    private bool isUndo = true;
    private Action? revertAction;

    public UndoRedoHudCoordinator()
    {
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        timer.Tick += (_, _) => Dismiss();

        RevertCommand = new RelayCommand(ExecuteRevert, () => revertAction != null);
        DismissCommand = new RelayCommand(Dismiss);
    }

    public bool IsVisible
    {
        get => isVisible;
        private set => SetProperty(ref isVisible, value);
    }

    public string Title
    {
        get => title;
        private set => SetProperty(ref title, value);
    }

    public string Description
    {
        get => description;
        private set => SetProperty(ref description, value);
    }

    public string Glyph
    {
        get => glyph;
        private set => SetProperty(ref glyph, value);
    }

    public string ActionLabel
    {
        get => actionLabel;
        private set => SetProperty(ref actionLabel, value);
    }

    public string ActionToolTip
    {
        get => actionToolTip;
        private set => SetProperty(ref actionToolTip, value);
    }

    public string ActionHint => isUndo ? "Ctrl+Y" : "Ctrl+Z";

    public bool IsUndo
    {
        get => isUndo;
        private set
        {
            if (SetProperty(ref isUndo, value))
                OnPropertyChanged(nameof(ActionHint));
        }
    }

    public ICommand RevertCommand { get; }
    public ICommand DismissCommand { get; }

    /// <summary>
    /// Muestra el HUD indicando que se ha deshecho una acción, con opción de Rehacer en 1 clic.
    /// </summary>
    public void NotifyUndo(string actionDescription, Action? onRevert = null)
    {
        ShowHud(
            isUndoAction: true,
            titleText: "Deshecho",
            glyphText: "↩",
            descText: actionDescription,
            actionText: "Rehacer",
            tooltipText: "Rehacer la acción (Ctrl+Y)",
            onRevert: onRevert);
    }

    /// <summary>
    /// Muestra el HUD indicando que se ha rehecho una acción, con opción de Deshacer en 1 clic.
    /// </summary>
    public void NotifyRedo(string actionDescription, Action? onRevert = null)
    {
        ShowHud(
            isUndoAction: false,
            titleText: "Rehecho",
            glyphText: "↪",
            descText: actionDescription,
            actionText: "Deshacer",
            tooltipText: "Deshacer la acción (Ctrl+Z)",
            onRevert: onRevert);
    }

    public void SetPaused(bool paused)
    {
        if (!IsVisible) return;
        if (paused)
        {
            timer.Stop();
        }
        else
        {
            timer.Interval = TimeSpan.FromSeconds(2.5);
            timer.Start();
        }
    }

    public void Dismiss()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Dismiss);
            return;
        }

        timer.Stop();
        revertAction = null;
        IsVisible = false;
        ((RelayCommand)RevertCommand).RaiseCanExecuteChanged();
    }

    private void ShowHud(bool isUndoAction, string titleText, string glyphText, string descText, string actionText, string tooltipText, Action? onRevert)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ShowHud(isUndoAction, titleText, glyphText, descText, actionText, tooltipText, onRevert));
            return;
        }

        timer.Stop();

        IsUndo = isUndoAction;
        Title = titleText;
        Glyph = glyphText;
        Description = string.IsNullOrWhiteSpace(descText) ? "Edición" : descText.Trim();
        ActionLabel = actionText;
        ActionToolTip = tooltipText;
        revertAction = onRevert;

        IsVisible = true;
        ((RelayCommand)RevertCommand).RaiseCanExecuteChanged();

        timer.Interval = TimeSpan.FromSeconds(4.5);
        timer.Start();
    }

    private void ExecuteRevert()
    {
        var action = revertAction;
        Dismiss();
        action?.Invoke();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
