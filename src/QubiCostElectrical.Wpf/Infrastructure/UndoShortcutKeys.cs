using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>Ctrl+Z deshace. Ctrl+Y y Ctrl+Shift+Z rehacen. Incluye la tecla real si llega como tecla de sistema.</summary>
public static class UndoShortcutKeys
{
    public enum Kind { None, Undo, Redo }

    public static Kind Match(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed)
            key = e.ImeProcessedKey;
        return Match(key, e.KeyboardDevice.Modifiers);
    }

    public static Kind Match(Key key, ModifierKeys modifiers)
    {
        bool ctrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        bool alt = (modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        if (!ctrl || alt)
            return Kind.None;
        if (key == Key.Z && !shift)
            return Kind.Undo;
        if ((key == Key.Y && !shift) || (key == Key.Z && shift))
            return Kind.Redo;
        return Kind.None;
    }
}
