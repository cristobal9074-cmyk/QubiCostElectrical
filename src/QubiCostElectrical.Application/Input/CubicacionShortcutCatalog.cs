namespace QubiCostElectrical;

/// <summary>Catálogo de acciones de Cubicación con atajos por defecto (restaurados de v1.0).</summary>
public static class CubicacionShortcutCatalog
{
    public const string Scope = "cubicacion";

    public const string PartidaPrevious = "cubicacion.partida.previous";
    public const string PartidaNext = "cubicacion.partida.next";
    public const string RowMoveUp = "cubicacion.row.moveUp";
    public const string RowMoveDown = "cubicacion.row.moveDown";
    public const string PrecioLista = "cubicacion.precioLista";
    public const string ScTomar = "cubicacion.scTomar";
    public const string Itemizado = "cubicacion.itemizado";
    public const string Plantilla = "cubicacion.plantilla";
    public const string SectorMatrix = "cubicacion.sectorMatrix";
    public const string ResourceDetail = "cubicacion.resourceDetail";
    public const string NoCubicar = "cubicacion.noCubicar";
    public const string Observacion = "cubicacion.observacion";
    public const string QuickJump = "cubicacion.quickJump";
    public const string FillDown = "cubicacion.fillDown";
    public const string FillRight = "cubicacion.fillRight";
    public const string RepeatMeasurement = "cubicacion.repeatMeasurement";
    public const string AddPartida = "cubicacion.partida.add";
    public const string DeletePartida = "cubicacion.partida.delete";
    public const string BatchReplace = "cubicacion.batchReplace";
    public const string FamilyBatchReplace = "cubicacion.familyBatchReplace";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(PartidaPrevious, Scope, "Partida anterior", "Alt+Up", "Selecciona la partida previa en el panel izquierdo."),
        new(PartidaNext, Scope, "Partida siguiente", "Alt+Down", "Selecciona la partida siguiente en el panel izquierdo."),
        new(AddPartida, Scope, "Agregar partida", "Ctrl+Plus", "Agrega una nueva partida debajo de la seleccionada."),
        new(DeletePartida, Scope, "Eliminar partida", "Ctrl+Minus", "Elimina la partida seleccionada del presupuesto."),
        new(RowMoveUp, Scope, "Subir recurso", "Alt+Shift+Up", "Mueve la fila seleccionada hacia arriba."),
        new(RowMoveDown, Scope, "Bajar recurso", "Alt+Shift+Down", "Mueve la fila seleccionada hacia abajo."),
        new(PrecioLista, Scope, "Precio lista", "Alt+V", "Abre el resumen de precios de lista del proyecto."),
        new(ScTomar, Scope, "Tomar de SC", "Alt+L", "Toma cantidades desde servicios comunes."),
        new(Itemizado, Scope, "Ir a partida (itemizado)", "Alt+S", "Abre el navegador de partidas del itemizado."),
        new(Plantilla, Scope, "Cargar plantilla", "Alt+Z", "Aplica conjunto eléctrico a la partida activa."),
        new(SectorMatrix, Scope, "Alternar vista casillas/detalle", "Alt+A", "Conmuta entre la matriz de sectores y la vista detalle."),
        new(ResourceDetail, Scope, "Vista detalle", "Alt+D", "Muestra la grilla en modo detalle."),
        new(NoCubicar, Scope, "Excluir / incluir partida", "Ctrl+E",
            "Excluye o vuelve a incluir la partida en cubicación (tachado, grilla bloqueada)."),
        new(Observacion, Scope, "Escribir observación", "Ctrl+Shift+E",
            "Salta al campo de observación de la partida activa para escribir sin usar el mouse."),
        new(QuickJump, Scope, "Lanzador rápido de partidas", "Ctrl+G",
            "Abre el buscador Spotlight para saltar instantáneamente a cualquier partida."),
        new(FillDown, Scope, "Rellenar hacia abajo (Fill Down)", "Ctrl+D",
            "Copia la medición de la fila superior a las filas seleccionadas."),
        new(FillRight, Scope, "Rellenar a la derecha (Fill Right)", "Ctrl+R",
            "Copia la medición del sector izquierdo a los sectores seleccionados a la derecha."),
        new(RepeatMeasurement, Scope, "Repetir última medición", "F4",
            "Inserta y aplica la última medición/fórmula ingresada en la celda activa."),
        new(BatchReplace, Scope, "Buscar y reemplazar material", "Ctrl+H",
            "Busca y reemplaza un material en todas las partidas del proyecto preservando metrajes y fórmulas."),
        new(FamilyBatchReplace, Scope, "Cambio de línea o familia masivo", "Ctrl+Shift+H",
            "Reemplaza masivamente familias o líneas completas de materiales (Ingeniería de Valor) con auto-mapeo del banco de precios.")
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}

public sealed record KeyboardShortcutDefinition(
    string Id,
    string Scope,
    string Label,
    string DefaultGesture,
    string? Description = null,
    bool IsConfigurable = true);
