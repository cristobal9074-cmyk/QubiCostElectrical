namespace QubiCostElectrical;

/// <summary>Atajos de shell / itemizado configurables (además de Cubicación).</summary>
public static class ShellShortcutCatalog
{
    public const string Scope = "shell";

    public const string Save = "shell.save";
    public const string Undo = "shell.undo";
    public const string Redo = "shell.redo";
    public const string Cubicar = "itemizado.cubicar";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(Save, Scope, "Guardar proyecto", "Ctrl+S", "Guarda el .qce activo."),
        new(Undo, Scope, "Deshacer", "Ctrl+Z", "Deshace el último cambio del proyecto."),
        new(Redo, Scope, "Rehacer", "Ctrl+Y", "Rehace el último cambio deshecho."),
        new(Cubicar, "itemizado", "Cubicar selección", "Ctrl+Shift+C", "Abre cubicación de las partidas marcadas en itemizado.")
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}
