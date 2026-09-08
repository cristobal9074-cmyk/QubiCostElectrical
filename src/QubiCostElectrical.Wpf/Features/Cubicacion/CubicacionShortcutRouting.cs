using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public static class CubicacionShortcutRouting
{
    private static UserPreferences? cachedPrefs;

    public static void InvalidateCache() => cachedPrefs = null;

    private static UserPreferences GetPreferences() =>
        cachedPrefs ??= UserPreferencesStore.Load();

    public static bool Matches(KeyEventArgs e, params string[] actionIds)
    {
        if (actionIds.Length == 0)
            return false;

        var prefs = GetPreferences();
        foreach (var actionId in actionIds)
        {
            var gestureText = KeyboardShortcutPreferences.GetEffective(actionId, prefs);
            if (KeyboardShortcutGesture.TryParse(gestureText, out var gesture)
                && gesture != null
                && gesture.Matches(e))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryHandle(KeyEventArgs e, CubicacionViewModel vm)
    {
        var prefs = GetPreferences();
        foreach (var def in CubicacionShortcutCatalog.All)
        {
            var gestureText = KeyboardShortcutPreferences.GetEffective(def.Id, prefs);
            if (!KeyboardShortcutGesture.TryParse(gestureText, out var gesture) || gesture == null)
                continue;
            if (!gesture.Matches(e))
                continue;
            if (vm.TryExecuteShortcut(def.Id))
                return true;
        }

        return false;
    }
}
