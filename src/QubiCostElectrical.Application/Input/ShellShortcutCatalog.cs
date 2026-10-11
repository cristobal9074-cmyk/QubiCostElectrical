namespace QubiCostElectrical;

/// <summary>Atajos de shell y navegación global del sistema.</summary>
public static class ShellShortcutCatalog
{
    public const string Scope = "shell";

    public const string Save = "shell.save";
    public const string Undo = "shell.undo";
    public const string Redo = "shell.redo";
    public const string CommandPalette = "shell.commandPalette";
    public const string QuickJump = "shell.quickJump";
    public const string Help = "shell.help";

    // Navegación global del shell (rutas rápidas)
    public const string NavInicio = "shell.nav.inicio";
    public const string NavDashboard = "shell.nav.dashboard";
    public const string NavProject = "shell.nav.project";
    public const string NavItemizado = "shell.nav.itemizado";
    public const string NavCubicacion = "shell.nav.cubicacion";
    public const string NavCubicacionSc = "shell.nav.cubicacionSc";
    public const string NavApu = "shell.nav.apu";
    public const string NavPrecios = "shell.nav.precios";
    public const string NavCotizaciones = "shell.nav.cotizaciones";
    public const string NavCompras = "shell.nav.compras";
    public const string NavCosto = "shell.nav.costo";
    public const string NavOferta = "shell.nav.oferta";
    public const string NavPreferencias = "shell.nav.preferencias";

    // Compatibilidad
    public const string Cubicar = "itemizado.cubicar";

    public static IReadOnlyList<KeyboardShortcutDefinition> All { get; } =
    [
        new(Save, Scope, "Guardar proyecto", "Ctrl+S", "Guarda los cambios en el archivo .qce activo."),
        new(Undo, Scope, "Deshacer", "Ctrl+Z", "Deshace la última acción o cambio en el proyecto."),
        new(Redo, Scope, "Rehacer", "Ctrl+Y", "Rehace la última acción deshecha."),
        new(CommandPalette, Scope, "Paleta de comandos", "Ctrl+K", "Abre la paleta de comandos inteligentes con búsqueda por lenguaje natural.", IsConfigurable: false),
        new(QuickJump, Scope, "Spotlight buscador rápido", "Ctrl+J", "Buscador flotante para saltar directamente a cualquier módulo, partida o circuito.", IsConfigurable: false),
        new(Help, Scope, "Atajos y Ayuda", "F1", "Abre la ventana informativa de atajos y ayuda del sistema.", IsConfigurable: false),
        new(NavInicio, Scope, "Ir a Inicio / Proyectos", "Alt+1", "Navega a la pantalla de bienvenida y selector de proyectos.", IsConfigurable: false),
        new(NavDashboard, Scope, "Ir a Cockpit / Dashboard", "Alt+2", "Navega al panel financiero y métricas de costos.", IsConfigurable: false),
        new(NavProject, Scope, "Ir a Datos de Proyecto", "Alt+P", "Abre los datos generales, cliente y auditoría del proyecto.", false),
        new(NavItemizado, Scope, "Ir a Itemizado", "Alt+3", "Navega a la estructura de partidas y presupuesto.", false),
        new(NavCubicacion, Scope, "Ir a Cubicación", "Alt+4", "Navega a la cubicación detallada de partidas.", false),
        new(NavCubicacionSc, Scope, "Ir a Cubicación SC", "Alt+5", "Navega a tableros y circuitos de servicios comunes.", false),
        new(NavApu, Scope, "Ir a Análisis APU", "Alt+6", "Navega al análisis de precios unitarios e insumos.", false),
        new(NavPrecios, Scope, "Ir a Banco de Precios", "Alt+7", "Navega al catálogo maestro de materiales y mano de obra.", false),
        new(NavCotizaciones, Scope, "Ir a Cotizaciones", "Alt+8", "Navega a la comparativa de cotizaciones de proveedores.", false),
        new(NavCompras, Scope, "Ir a Lista de Compra", "Alt+9", "Navega a la consolidación de compras por insumo.", false),
        new(NavCosto, Scope, "Ir a Cierre y Costo", "Alt+0", "Navega al análisis de costo, márgenes y política comercial.", false),
        new(NavOferta, Scope, "Ir a Oferta y Exportación", "Alt+O", "Abre el asistente de entrega de oferta comercial y PDF/Excel.", false),
        new(NavPreferencias, Scope, "Ir a Preferencias", "Alt+M", "Abre las preferencias de usuario, atajos y sistema.", false)
    ];

    public static bool IsKnown(string actionId) =>
        All.Any(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));

    public static KeyboardShortcutDefinition? Find(string actionId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.Ordinal));
}
