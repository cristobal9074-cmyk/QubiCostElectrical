namespace QubiCostElectrical;

public static class KeyboardShortcutPreferences
{
    public static string GetEffective(string actionId, UserPreferences prefs)
    {
        if (prefs.KeyboardShortcuts.TryGetValue(actionId, out var custom)
            && !string.IsNullOrWhiteSpace(custom)
            && KeyboardShortcutGesture.TryParse(custom, out _))
        {
            return custom.Trim();
        }

        return CubicacionShortcutCatalog.Find(actionId)?.DefaultGesture
            ?? ShellShortcutCatalog.Find(actionId)?.DefaultGesture
            ?? "";
    }

    public static Dictionary<string, string> BuildEffectiveMap(UserPreferences prefs)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var def in CubicacionShortcutCatalog.All)
            map[def.Id] = GetEffective(def.Id, prefs);
        foreach (var def in ShellShortcutCatalog.All)
            map[def.Id] = GetEffective(def.Id, prefs);
        return map;
    }

    public static void Sanitize(UserPreferences prefs)
    {
        prefs.KeyboardShortcuts ??= new Dictionary<string, string>(StringComparer.Ordinal);

        var cleaned = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (actionId, gestureText) in prefs.KeyboardShortcuts)
        {
            if (!CubicacionShortcutCatalog.IsKnown(actionId) && !ShellShortcutCatalog.IsKnown(actionId))
                continue;
            if (string.IsNullOrWhiteSpace(gestureText))
                continue;
            if (!KeyboardShortcutGesture.TryParse(gestureText, out _))
                continue;
            cleaned[actionId] = gestureText.Trim();
        }

        prefs.KeyboardShortcuts = cleaned;

        // Migrar atajos legacy que chocaban con grilla (Ctrl+A seleccionar todo, Ctrl+D duplicar fila).
        MigrateLegacyConflictingGestures(prefs);
    }

    private static void MigrateLegacyConflictingGestures(UserPreferences prefs)
    {
        if (prefs.KeyboardShortcuts.TryGetValue(CubicacionShortcutCatalog.SectorMatrix, out var matrix)
            && string.Equals(matrix, "Ctrl+A", StringComparison.OrdinalIgnoreCase))
            prefs.KeyboardShortcuts.Remove(CubicacionShortcutCatalog.SectorMatrix);

        if (prefs.KeyboardShortcuts.TryGetValue(CubicacionShortcutCatalog.ResourceDetail, out var detail)
            && string.Equals(detail, "Ctrl+D", StringComparison.OrdinalIgnoreCase))
            prefs.KeyboardShortcuts.Remove(CubicacionShortcutCatalog.ResourceDetail);
    }

    public static void ResetToDefaults(UserPreferences prefs)
    {
        prefs.KeyboardShortcuts = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static void SetOverride(UserPreferences prefs, string actionId, string gestureText)
    {
        prefs.KeyboardShortcuts ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(gestureText))
        {
            prefs.KeyboardShortcuts.Remove(actionId);
            return;
        }

        if (!KeyboardShortcutGesture.TryParse(gestureText, out _))
            throw new ArgumentException($"Atajo no válido: {gestureText}", nameof(gestureText));

        prefs.KeyboardShortcuts[actionId] = gestureText.Trim();
    }
}
