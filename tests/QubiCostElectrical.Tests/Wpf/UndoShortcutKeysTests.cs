using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class UndoShortcutKeysTests
{
    [Theory]
    [InlineData(Key.Z, ModifierKeys.Control, UndoShortcutKeys.Kind.Undo)]
    [InlineData(Key.Y, ModifierKeys.Control, UndoShortcutKeys.Kind.Redo)]
    [InlineData(Key.Z, ModifierKeys.Control | ModifierKeys.Shift, UndoShortcutKeys.Kind.Redo)]
    [InlineData(Key.Z, ModifierKeys.None, UndoShortcutKeys.Kind.None)]
    [InlineData(Key.Y, ModifierKeys.Control | ModifierKeys.Shift, UndoShortcutKeys.Kind.None)]
    [InlineData(Key.Z, ModifierKeys.Control | ModifierKeys.Alt, UndoShortcutKeys.Kind.None)]
    public void Match_CtrlZ_Undoes_And_CtrlY_Redoes(Key key, ModifierKeys modifiers, UndoShortcutKeys.Kind expected)
        => Assert.Equal(expected, UndoShortcutKeys.Match(key, modifiers));
}
