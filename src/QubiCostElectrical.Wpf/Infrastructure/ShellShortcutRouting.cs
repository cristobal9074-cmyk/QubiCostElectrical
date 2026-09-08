using System.Windows.Input;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static class ShellShortcutRouting
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
}
