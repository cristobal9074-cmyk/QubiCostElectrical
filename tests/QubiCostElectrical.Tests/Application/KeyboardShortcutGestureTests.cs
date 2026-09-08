using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Application;

public sealed class KeyboardShortcutGestureTests
{
    [Theory]
    [InlineData("Alt+L", true, false, false, "L")]
    [InlineData("Ctrl+Shift+Up", false, true, true, "Up")]
    [InlineData("Alt+Down", true, false, false, "Down")]
    public void TryParse_ParsesKnownGestures(string input, bool alt, bool ctrl, bool shift, string key)
    {
        Assert.True(KeyboardShortcutGesture.TryParse(input, out var gesture));
        Assert.NotNull(gesture);
        Assert.Equal(alt, gesture!.Alt);
        Assert.Equal(ctrl, gesture.Ctrl);
        Assert.Equal(shift, gesture.Shift);
        Assert.Equal(key, gesture.Key);
    }

    [Fact]
    public void ToStorageString_RoundTripsDisplay()
    {
        Assert.True(KeyboardShortcutGesture.TryParse("Alt+V", out var gesture));
        Assert.Equal("Alt+V", gesture!.ToStorageString());
        Assert.Equal("Alt + V", gesture.ToDisplayString());
    }

    [Fact]
    public void Preferences_SanitizeRemovesUnknownActions()
    {
        var prefs = new UserPreferences
        {
            KeyboardShortcuts =
            {
                [CubicacionShortcutCatalog.PrecioLista] = "Alt+P",
                ["unknown.action"] = "Alt+X",
                [CubicacionShortcutCatalog.Itemizado] = "Alt+"
            }
        };

        KeyboardShortcutPreferences.Sanitize(prefs);

        Assert.Equal("Alt+P", prefs.KeyboardShortcuts[CubicacionShortcutCatalog.PrecioLista]);
        Assert.False(prefs.KeyboardShortcuts.ContainsKey("unknown.action"));
        Assert.False(prefs.KeyboardShortcuts.ContainsKey(CubicacionShortcutCatalog.Itemizado));
    }
}
