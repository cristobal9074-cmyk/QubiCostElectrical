using System.Windows.Input;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static class WpfKeyboardShortcutExtensions
{
    public static bool Matches(this KeyboardShortcutGesture gesture, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var mods = Keyboard.Modifiers;

        var alt = mods.HasFlag(ModifierKeys.Alt);
        var ctrl = mods.HasFlag(ModifierKeys.Control);
        var shift = mods.HasFlag(ModifierKeys.Shift);

        if (gesture.Alt != alt || gesture.Ctrl != ctrl || gesture.Shift != shift)
            return false;

        return string.Equals(gesture.Key, key.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryCapture(KeyEventArgs e, out string? gestureText)
    {
        gestureText = null;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.System)
            return false;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
            return false;

        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None && key is not (Key.F1 or Key.F2 or Key.F3 or Key.F4 or Key.F5 or Key.F6 or Key.F7 or Key.F8 or Key.F9 or Key.F10 or Key.F11 or Key.F12))
            return false;

        var parts = new List<string>(4);
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(key.ToString());

        gestureText = string.Join('+', parts);
        return KeyboardShortcutGesture.TryParse(gestureText, out _);
    }
}
