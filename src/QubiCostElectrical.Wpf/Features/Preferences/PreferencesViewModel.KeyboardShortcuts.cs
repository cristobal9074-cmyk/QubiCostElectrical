using System.Collections.ObjectModel;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Cubicacion;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Preferences;

public sealed partial class PreferencesViewModel
{
    private static readonly HashSet<string> ShellReservedGestures = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alt+1", "Alt+2", "Alt+3", "Alt+Shift+3", "Alt+4", "Alt+5", "Alt+6", "Alt+7", "Alt+8", "Alt+9", "Alt+0",
        "Alt+P", "Alt+O", "Alt+M", "Alt+C", "Ctrl+K", "F1"
    };
    public ObservableCollection<KeyboardShortcutPreferenceRowVm> CubicacionShortcuts { get; } = new();
    public ObservableCollection<KeyboardShortcutPreferenceRowVm> ShellShortcuts { get; } = new();

    private KeyboardShortcutPreferenceRowVm? activeCaptureRow;
    private string shortcutCaptureMessage = "";

    public string ShortcutCaptureMessage
    {
        get => shortcutCaptureMessage;
        private set => SetProperty(ref shortcutCaptureMessage, value);
    }

    public bool IsShortcutCaptureActive => activeCaptureRow != null;

    private void LoadShortcutsFromDraft()
    {
        CubicacionShortcuts.Clear();
        foreach (var def in CubicacionShortcutCatalog.All)
        {
            var effective = KeyboardShortcutPreferences.GetEffective(def.Id, _draft);
            CubicacionShortcuts.Add(new KeyboardShortcutPreferenceRowVm(
                def.Id, def.Label, def.Description, def.DefaultGesture)
            {
                EffectiveGesture = effective
            });
        }

        ShellShortcuts.Clear();
        foreach (var def in ShellShortcutCatalog.All)
        {
            var effective = KeyboardShortcutPreferences.GetEffective(def.Id, _draft);
            ShellShortcuts.Add(new KeyboardShortcutPreferenceRowVm(
                def.Id, def.Label, def.Description, def.DefaultGesture)
            {
                EffectiveGesture = effective
            });
        }
    }

    private void SyncShortcutsToDraft()
    {
        _draft.KeyboardShortcuts ??= new Dictionary<string, string>(StringComparer.Ordinal);
        _draft.KeyboardShortcuts.Clear();

        foreach (var row in CubicacionShortcuts.Concat(ShellShortcuts))
        {
            if (string.Equals(row.EffectiveGesture, row.DefaultGesture, StringComparison.OrdinalIgnoreCase))
                continue;
            _draft.KeyboardShortcuts[row.ActionId] = row.EffectiveGesture;
        }
    }

    public void BeginShortcutCapture(KeyboardShortcutPreferenceRowVm row)
    {
        activeCaptureRow = row;
        foreach (var item in CubicacionShortcuts.Concat(ShellShortcuts))
            item.IsCapturing = ReferenceEquals(item, row);
        ShortcutCaptureMessage = $"Capturando atajo para «{row.Label}». Presione la combinación deseada (Esc cancela).";
        OnPropertyChanged(nameof(IsShortcutCaptureActive));
    }

    public void CancelShortcutCapture()
    {
        activeCaptureRow = null;
        foreach (var item in CubicacionShortcuts.Concat(ShellShortcuts))
            item.IsCapturing = false;
        ShortcutCaptureMessage = "";
        OnPropertyChanged(nameof(IsShortcutCaptureActive));
    }

    public bool HandleShortcutCaptureKey(KeyEventArgs e)
    {
        if (activeCaptureRow == null)
            return false;

        if (e.Key == Key.Escape)
        {
            CancelShortcutCapture();
            e.Handled = true;
            return true;
        }

        if (!WpfKeyboardShortcutExtensions.TryCapture(e, out var gestureText) || gestureText == null)
        {
            e.Handled = true;
            return true;
        }

        if (!TryApplyCapturedShortcut(activeCaptureRow, gestureText))
        {
            var shellConflict = ShellReservedGestures.Contains(gestureText);
            WpfDialogService.Warning(
                shellConflict ? "Atajo reservado del shell" : "Atajo duplicado",
                shellConflict
                    ? "Esa combinación está reservada para la navegación global (Alt+1…9, Alt+P, etc.). Elija otra."
                    : "Esa combinación ya está asignada a otra acción de cubicación. Elija otra.");
            e.Handled = true;
            return true;
        }

        ShortcutCaptureMessage = "";
        OnPropertyChanged(nameof(IsShortcutCaptureActive));
        e.Handled = true;
        return true;
    }

    public bool TryApplyCapturedShortcut(KeyboardShortcutPreferenceRowVm row, string gestureText)
    {
        if (!KeyboardShortcutGesture.TryParse(gestureText, out _))
            return false;

        if (ShellReservedGestures.Contains(gestureText))
            return false;

        foreach (var other in CubicacionShortcuts.Concat(ShellShortcuts))
        {
            if (ReferenceEquals(other, row)) continue;
            if (string.Equals(other.EffectiveGesture, gestureText, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        row.EffectiveGesture = gestureText;
        row.IsCapturing = false;
        activeCaptureRow = null;
        HasPendingChanges = true;
        return true;
    }

    public void ResetShortcut(KeyboardShortcutPreferenceRowVm row)
    {
        row.ResetToDefault();
        HasPendingChanges = true;
    }

    public void ResetAllCubicacionShortcuts()
    {
        foreach (var row in CubicacionShortcuts)
            row.ResetToDefault();
        HasPendingChanges = true;
    }

    public void ResetAllShellShortcuts()
    {
        foreach (var row in ShellShortcuts)
            row.ResetToDefault();
        HasPendingChanges = true;
    }
}
