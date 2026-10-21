using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

internal static class CubicacionCellKeyMap
{
    public static CubicacionKey From(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key switch
        {
            Key.Enter or Key.Return => CubicacionKey.Enter,
            Key.Escape => CubicacionKey.Escape,
            Key.Tab => CubicacionKey.Tab,
            Key.F2 => CubicacionKey.F2,
            Key.Up => CubicacionKey.Up,
            Key.Down => CubicacionKey.Down,
            Key.Left => CubicacionKey.Left,
            Key.Right => CubicacionKey.Right,
            Key.Delete => CubicacionKey.Delete,
            Key.Back => CubicacionKey.Backspace,
            Key.Insert => CubicacionKey.Insert,
            Key.Space => CubicacionKey.Space,
            Key.C => CubicacionKey.C,
            Key.X => CubicacionKey.X,
            Key.V => CubicacionKey.V,
            Key.S => CubicacionKey.S,
            Key.D => CubicacionKey.D,
            Key.N => CubicacionKey.N,
            Key.OemPlus or Key.Add => CubicacionKey.Plus,
            Key.OemMinus or Key.Subtract => CubicacionKey.Minus,
            Key.Oem1 => CubicacionKey.Oem1,
            Key.OemQuestion => CubicacionKey.OemQuestion,
            Key.LeftShift or Key.RightShift => CubicacionKey.Shift,
            Key.LeftCtrl or Key.RightCtrl => CubicacionKey.Control,
            Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin => CubicacionKey.Alt,
            _ => CubicacionKey.Other
        };
    }

    public static CubicacionModifiers ModifiersOf(KeyEventArgs e)
    {
        var mods = e.KeyboardDevice.Modifiers;
        var result = CubicacionModifiers.None;
        if (mods.HasFlag(ModifierKeys.Control)) result |= CubicacionModifiers.Control;
        if (mods.HasFlag(ModifierKeys.Alt)) result |= CubicacionModifiers.Alt;
        if (mods.HasFlag(ModifierKeys.Shift)) result |= CubicacionModifiers.Shift;
        return result;
    }
}
