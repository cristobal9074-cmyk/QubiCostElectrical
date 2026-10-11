namespace QubiCostElectrical;

/// <summary>Catálogo de acciones y atajos de teclado para la pantalla Itemizado.</summary>
public static class ItemizadoShortcutCatalog
{
    public const string Scope = "itemizado";

    public const string AddPartida = "itemizado.partida.add";
    public const string AddPartidaAlt = "itemizado.partida.addAlt";
    public const string DeletePartida = "itemizado.partida.delete";
    public const string Indent = "itemizado.indent";
    public const string Outdent = "itemizado.outdent";
    public const string PasteExcel = "itemizado.pasteExcel";
    public const string Cubicar = "itemizado.cubicar";
    public const string Search = "itemizado.search";
    public const string EditCell = "itemizado.editCell";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(AddPartida, Scope, "Agregar partida", "Insert", "Inserta una nueva partida o capítulo en la estructura del presupuesto."),
        new(AddPartidaAlt, Scope, "Agregar partida (alternativo)", "Ctrl+Plus", "Atajo alternativo para agregar partida debajo de la actual."),
        new(DeletePartida, Scope, "Eliminar partida", "Delete", "Elimina la partida o capítulo seleccionado del presupuesto."),
        new(Indent, Scope, "Convertir en subpartida (indentar)", "Tab", "Aumenta la jerarquía convirtiendo el ítem en subpartida.", IsConfigurable: false),
        new(Outdent, Scope, "Disminuir nivel (outdent)", "Shift+Tab", "Disminuye la jerarquía hacia el capítulo padre superior.", IsConfigurable: false),
        new(PasteExcel, Scope, "Pegar filas", "Ctrl+V", "Importa y estructura automáticamente partidas copiadas del portapapeles.", IsConfigurable: false),
        new(Cubicar, Scope, "Cubicar selección", "Ctrl+Shift+C", "Abre directamente la pantalla de cubicación con las partidas seleccionadas."),
        new(Search, Scope, "Buscar en itemizado", "Ctrl+F", "Enfoca la caja de búsqueda para filtrar partidas por código o nombre."),
        new(EditCell, Scope, "Editar celda", "F2", "Activa el modo de edición de la celda activa en el itemizado.", IsConfigurable: false)
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}
