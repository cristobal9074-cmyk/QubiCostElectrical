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

    [Fact]
    public void CatalogRegistry_ContainsAllScopesAndUniqueActionIds()
    {
        var all = KeyboardShortcutCatalogRegistry.All;
        Assert.NotEmpty(all);

        // Sin IDs duplicados
        var duplicateIds = all.GroupBy(d => d.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(duplicateIds);

        // Contiene todos los módulos
        var scopes = all.Select(d => d.Scope).Distinct().ToHashSet();
        Assert.Contains(ShellShortcutCatalog.Scope, scopes);
        Assert.Contains(ItemizadoShortcutCatalog.Scope, scopes);
        Assert.Contains(CubicacionShortcutCatalog.Scope, scopes);
        Assert.Contains(CubicacionScShortcutCatalog.Scope, scopes);
        Assert.Contains(ApuShortcutCatalog.Scope, scopes);
        Assert.Contains(PricesShortcutCatalog.Scope, scopes);

        // Verificamos etiquetas e iconos de cada módulo
        foreach (var scope in scopes)
        {
            var label = KeyboardShortcutCatalogRegistry.GetScopeLabel(scope);
            var icon = KeyboardShortcutCatalogRegistry.GetScopeIcon(scope);
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.False(string.IsNullOrWhiteSpace(icon));
        }
    }

    [Fact]
    public void Preferences_SanitizePreservesNewCatalogActions()
    {
        var prefs = new UserPreferences
        {
            KeyboardShortcuts =
            {
                [ItemizadoShortcutCatalog.AddPartida] = "Insert",
                [ApuShortcutCatalog.AddMaterial] = "Ctrl+M",
                [CubicacionScShortcutCatalog.AddTablero] = "Ctrl+T",
                [PricesShortcutCatalog.Search] = "Ctrl+F",
                ["invalid.shortcut.id"] = "Ctrl+Shift+X"
            }
        };

        KeyboardShortcutPreferences.Sanitize(prefs);

        Assert.Equal("Insert", prefs.KeyboardShortcuts[ItemizadoShortcutCatalog.AddPartida]);
        Assert.Equal("Ctrl+M", prefs.KeyboardShortcuts[ApuShortcutCatalog.AddMaterial]);
        Assert.Equal("Ctrl+T", prefs.KeyboardShortcuts[CubicacionScShortcutCatalog.AddTablero]);
        Assert.Equal("Ctrl+F", prefs.KeyboardShortcuts[PricesShortcutCatalog.Search]);
        Assert.False(prefs.KeyboardShortcuts.ContainsKey("invalid.shortcut.id"));
    }
}
