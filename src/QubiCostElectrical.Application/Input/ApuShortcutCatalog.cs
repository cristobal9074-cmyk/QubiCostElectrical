namespace QubiCostElectrical;

/// <summary>Catálogo de acciones y atajos de teclado para la pantalla Análisis de Precios Unitarios (APU).</summary>
public static class ApuShortcutCatalog
{
    public const string Scope = "apu";

    public const string AddMaterial = "apu.material.add";
    public const string AddRow = "apu.row.add";
    public const string SuggestionNavigate = "apu.suggestions.navigate";
    public const string SuggestionConfirm = "apu.suggestions.confirm";
    public const string FlowCascade = "apu.flow.cascade";
    public const string SuggestionDismiss = "apu.suggestions.dismiss";
    public const string Search = "apu.search";
    public const string DeleteResource = "apu.resource.delete";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(AddMaterial, Scope, "Agregar material con IntelliSense", "Ctrl+M", "Inserta una fila de material desplegando el autocompletado predictivo."),
        new(AddRow, Scope, "Nueva fila de insumo", "Insert", "Agrega una nueva línea de insumo (material, mano de obra o equipo) en el APU."),
        new(SuggestionNavigate, Scope, "Navegar sugerencias IntelliSense", "Down / Up", "Desplaza la selección arriba y abajo dentro de las sugerencias del Banco de Precios.", IsConfigurable: false),
        new(SuggestionConfirm, Scope, "Confirmar insumo y saltar a Rendimiento", "Enter / Tab", "Inserta el insumo seleccionado y traslada el cursor a la celda de rendimiento.", IsConfigurable: false),
        new(FlowCascade, Scope, "Flujo continuo de insumos (Zero Friction)", "Enter", "Al presionar Enter en Rendimiento, crea automáticamente la siguiente fila de insumo.", IsConfigurable: false),
        new(SuggestionDismiss, Scope, "Cerrar sugerencias", "Escape", "Cierra la ventana emergente de sugerencias predictivas sin alterar el texto.", IsConfigurable: false),
        new(Search, Scope, "Buscar en APU", "Ctrl+F", "Enfoca la barra de búsqueda para filtrar insumos dentro del APU."),
        new(DeleteResource, Scope, "Eliminar insumo", "Delete", "Elimina el insumo seleccionado del análisis de precio unitario.")
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}
