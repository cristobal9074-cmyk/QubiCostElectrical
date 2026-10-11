namespace QubiCostElectrical;

/// <summary>Catálogo de acciones y atajos de teclado para la pantalla Banco de Precios y Cotizaciones.</summary>
public static class PricesShortcutCatalog
{
    public const string Scope = "prices";

    public const string Search = "prices.search";
    public const string RefreshUf = "prices.refreshUf";
    public const string ImportExcel = "prices.importExcel";
    public const string NewItem = "prices.item.new";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(Search, Scope, "Búsqueda FTS5 en catálogo", "Ctrl+F", "Enfoca el buscador rápido con indexación de texto completo de materiales."),
        new(RefreshUf, Scope, "Actualizar valor UF del día", "F5", "Consulta y recalcula los precios base con el valor oficial de la UF."),
        new(ImportExcel, Scope, "Importar catálogo", "Ctrl+U", "Abre el asistente para importar listas de precios y homologar columnas."),
        new(NewItem, Scope, "Nuevo insumo en banco", "Insert", "Abre el formulario para ingresar un nuevo material, mano de obra o equipo.")
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}
