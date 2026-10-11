namespace QubiCostElectrical;

/// <summary>Registro unificado de catálogos de atajos de teclado de la aplicación.</summary>
public static class KeyboardShortcutCatalogRegistry
{
    private static readonly Lazy<IReadOnlyList<KeyboardShortcutDefinition>> _allLazy = new(() =>
    {
        return ShellShortcutCatalog.All
            .Concat(ItemizadoShortcutCatalog.All)
            .Concat(CubicacionShortcutCatalog.All)
            .Concat(CubicacionScShortcutCatalog.All)
            .Concat(ApuShortcutCatalog.All)
            .Concat(PricesShortcutCatalog.All)
            .DistinctBy(d => d.Id)
            .ToList();
    });

    public static IReadOnlyList<KeyboardShortcutDefinition> All => _allLazy.Value;

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static string GetScopeLabel(string scope) => scope switch
    {
        ShellShortcutCatalog.Scope => "Global / Shell",
        ItemizadoShortcutCatalog.Scope => "Itemizado",
        CubicacionShortcutCatalog.Scope => "Cubicación",
        CubicacionScShortcutCatalog.Scope => "Cubicación SC",
        ApuShortcutCatalog.Scope => "APU",
        PricesShortcutCatalog.Scope => "Precios y Cotizaciones",
        _ => scope
    };

    public static string GetScopeIcon(string scope) => scope switch
    {
        ShellShortcutCatalog.Scope => "🌐",
        ItemizadoShortcutCatalog.Scope => "📋",
        CubicacionShortcutCatalog.Scope => "📐",
        CubicacionScShortcutCatalog.Scope => "⚡",
        ApuShortcutCatalog.Scope => "📊",
        PricesShortcutCatalog.Scope => "💰",
        _ => "⌨️"
    };
}
