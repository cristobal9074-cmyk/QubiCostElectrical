namespace QubiCostElectrical;

/// <summary>Catálogo de acciones y atajos de teclado para la pantalla Cubicación SC (Tableros y Circuitos).</summary>
public static class CubicacionScShortcutCatalog
{
    public const string Scope = "cubicacion_sc";

    public const string AddTablero = "cubicacionSc.tablero.add";
    public const string AddCircuito = "cubicacionSc.circuito.add";
    public const string Recalculate = "cubicacionSc.recalculate";
    public const string DeleteElement = "cubicacionSc.delete";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(AddTablero, Scope, "Agregar tablero", "Ctrl+T", "Agrega un nuevo tablero general o de distribución al proyecto."),
        new(AddCircuito, Scope, "Agregar circuito", "Ctrl+Shift+T", "Agrega un nuevo circuito alimentador o de carga al tablero activo."),
        new(Recalculate, Scope, "Recalcular alimentadores y cargas", "F5", "Ejecuta el recálculo de corrientes, caídas de tensión y protecciones."),
        new(DeleteElement, Scope, "Eliminar tablero/circuito", "Delete", "Elimina el circuito o tablero seleccionado en la jerarquía SC.")
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}
