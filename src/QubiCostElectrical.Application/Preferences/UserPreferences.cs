using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class CorporateThemePreferences
{
    public bool Enabled { get; set; } = false;
    public string Name { get; set; } = "Corporativo";
    public string CompanyName { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string PrimaryHex { get; set; } = "#0C3B5E";
    public string AccentHex { get; set; } = "#0F6CBD";
    public string BackgroundHex { get; set; } = "#F7F9FC";
    public string FontFamily { get; set; } = "Segoe UI";
}

public sealed class DashboardWidgetLayoutPreferences
{
    public bool Enabled { get; set; } = true;
    public List<string> WidgetOrder { get; set; } = new()
    {
        "Alertas", "KPIs", "TopPartidas", "MaterialesCriticos", "Areas", "Revision", "Sensibilidad"
    };
    public Dictionary<string, int> WidgetHeights { get; set; } = new();
}

public sealed class ReportTemplatePreferences
{
    public string TemplateName { get; set; } = "Oferta profesional";
    public string CoverTitle { get; set; } = "Oferta comercial";
    public string CoverSubtitle { get; set; } = "Resumen ejecutivo";
    public bool ShowDraftWatermark { get; set; } = true;
    public bool ShowCover { get; set; } = true;
    public bool ShowExecutiveSummary { get; set; } = true;
    public bool ShowItemizedDetail { get; set; } = true;
    public bool ShowCubicacionDetail { get; set; } = false;
    public bool ShowPriceValidity { get; set; } = true;
    public bool ShowTermsAndConditions { get; set; } = true;
    public bool ShowCoverTraceability { get; set; } = true;
    public List<string> OrderedFields { get; set; } = new()
    {
        "Logo", "Proyecto", "Cliente", "Resumen UF", "Itemizado", "Condiciones", "Firma"
    };
}

/// <summary>
/// Preferencias del usuario almacenadas en JSON local.
/// Todos los campos tienen valores por defecto para que el archivo sea opcional.
/// </summary>
public sealed class UserPreferences
{
    // ── Archivos y rutas ─────────────────────────────────────────────────
    /// <summary>Carpeta predeterminada para abrir y guardar proyectos.</summary>
    public string DefaultProjectFolder { get; set; } = "";

    /// <summary>Ruta personalizada del archivo SQLite del banco de precios. Vacío = ubicación predeterminada.</summary>
    public string PriceBankDatabasePath { get; set; } = "";

    // ── Navegación ────────────────────────────────────────────────────────
    /// <summary>Email del último usuario que inició sesión (para pre-llenar login).</summary>
    public string LastUserEmail { get; set; } = "";

    /// <summary>
    /// Si es verdadero, los cambios se escriben solos al .qce unos segundos después de editar.
    /// Desactivado por defecto: Guardar es manual y un .bak permite recuperar un cierre inesperado.
    /// </summary>
    public bool AutosaveEnabled { get; set; } = false;
    /// <summary>
    /// Default para proyectos nuevos: usar M.O. de instalación por rendimiento
    /// (tarifa D/H ÷ rendimiento del material) en vez del M.O. directo del catálogo.
    /// </summary>
    public bool MoInstalacionPorRendimientoDefault { get; set; } = false;

    // ── Márgenes comerciales predeterminados ─────────────────────────────
    /// <summary>Porcentaje predeterminado de Gastos Generales para nuevos proyectos (defecto 14,0%).</summary>
    public decimal DefaultGastosGeneralesPct { get; set; } = 14m;

    /// <summary>Porcentaje predeterminado de Utilidad para nuevos proyectos (defecto 18,0%).</summary>
    public decimal DefaultUtilidadPct { get; set; } = 18m;

    /// <summary>Porcentaje predeterminado de Mermas sobre materiales para nuevos proyectos (defecto 0,0%).</summary>
    public decimal DefaultMermasPct { get; set; } = 0m;

    /// <summary>Porcentaje predeterminado de Herramientas sobre mano de obra para nuevos proyectos (defecto 0,0%).</summary>
    public decimal DefaultHerramientasPct { get; set; } = 0m;

    /// <summary>Moneda predeterminada para nuevos proyectos ("CLP", "UF", "USD", "EUR"). Defecto CLP.</summary>
    public string DefaultMoneda { get; set; } = "CLP";

    /// <summary>
    /// Default del modo automático del índice de M.O. (FA): desactivado por defecto para mantener
    /// el FA y los precios fijos al eliminar o modificar materiales (paridad con Excel).
    /// </summary>
    public bool FaAutomatico { get; set; } = false;
    /// <summary>Últimas N rutas abiertas (max 8), más reciente primero.</summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Rutas de proyectos fijados al inicio con PIN (permanecen arriba).</summary>
    public List<string> PinnedRecentFiles { get; set; } = new();

    /// <summary>Indica si el historial de proyectos recientes fue inicializado o migrado desde instalaciones previas.</summary>
    public bool RecentProjectsInitialized { get; set; } = false;

    /// <summary>Estado expandido/colapsado de cada grupo del sidebar por clave de módulo.</summary>
    public Dictionary<string, bool> SidebarGroupsExpanded { get; set; } = new();

    /// <summary>Modo compacto del sidebar: sólo íconos para ganar espacio en pantallas pequeñas.</summary>
    public bool SidebarCompactMode { get; set; } = true;

    // ── Tipografía / zoom ────────────────────────────────────────────────
    /// <summary>Tamaño base de fuente en puntos. Rango recomendado: 8.0 – 11.0 (defecto 9.0).</summary>
    public float FontSize { get; set; } = 9.0f;

    // ── Tema ─────────────────────────────────────────────────────────────
    /// <summary>Nombre del tema activo. Por ahora sólo "Default"; listo para V4.</summary>
    public string ThemeName { get; set; } = "Default";
    public bool DarkMode { get; set; } = false;

    /// <summary>Paleta corporativa editable para UI y exportaciones.</summary>
    public CorporateThemePreferences CorporateTheme { get; set; } = new();

    /// <summary>Layout del dashboard ejecutivo personalizable.</summary>
    public DashboardWidgetLayoutPreferences DashboardWidgetLayout { get; set; } = new();

    /// <summary>Plantilla visual usada por el diseñador WYSIWYG básico de informes.</summary>
    public ReportTemplatePreferences ReportTemplate { get; set; } = new();

    // ── Rendimiento / fluidez ────────────────────────────────────────────
    /// <summary>Reduce animaciones, capturas de pantalla y esqueletos de carga al cambiar de pestaña.</summary>
    public bool ReduceMotion { get; set; } = false;
    /// <summary>Muestra panel de carga de navegación sólo en pantallas realmente pesadas.</summary>
    public bool QuietNavigationLoading { get; set; } = true;

    // ── Onboarding ───────────────────────────────────────────────────────
    /// <summary>Indica si el usuario ya completó u omitió el wizard de primer uso.</summary>
    public bool OnboardingCompleted { get; set; } = false;
    /// <summary>Última versión de onboarding mostrada. Permite reactivar guía si cambia el flujo en V4.</summary>
    public string OnboardingVersion { get; set; } = "";

    // ── Membrete corporativo global ─────────────────────────────────────
    /// <summary>Razón social usada en portadas, PDF y Excel.</summary>
    public string LetterheadCompanyName { get; set; } = "";
    /// <summary>RUT de la empresa, mostrado en documentos comerciales.</summary>
    public string LetterheadRut { get; set; } = "";
    /// <summary>Dirección comercial del membrete.</summary>
    public string LetterheadAddress { get; set; } = "";
    /// <summary>Teléfono comercial del membrete.</summary>
    public string LetterheadPhone { get; set; } = "";
    /// <summary>Correo comercial del membrete.</summary>
    public string LetterheadEmail { get; set; } = "";
    /// <summary>Sitio web comercial del membrete.</summary>
    public string LetterheadWebsite { get; set; } = "";
    /// <summary>Ruta local del logo JPG/PNG usado en exportaciones.</summary>
    public string LetterheadLogoPath { get; set; } = "";

    // ── Datos del profesional firmante ────────────────────────────────────
    public string ProfessionalName { get; set; } = "";
    public string ProfessionalRut { get; set; } = "";
    public string ProfessionalRole { get; set; } = "Ingeniero Eléctrico";
    public string ProfessionalRegistroSec { get; set; } = "";
    public string ProfessionalSignatureImagePath { get; set; } = "";
    /// <summary>Teléfono del firmante guardado como predeterminado global.</summary>
    public string ProfessionalPhone { get; set; } = "";
    /// <summary>Correo del firmante guardado como predeterminado global.</summary>
    public string ProfessionalEmail { get; set; } = "";
    /// <summary>Clase de licencia SEC (A, B, C, D) guardada como predeterminado global.</summary>
    public string ProfessionalSecClass { get; set; } = "";
    /// <summary>Ancho predeterminado de la firma en el PDF, en milímetros (0 = sin definir).</summary>
    public int ProfessionalSignatureWidthMm { get; set; } = 0;
    /// <summary>Alto predeterminado de la firma en el PDF, en milímetros (0 = sin definir).</summary>
    public int ProfessionalSignatureHeightMm { get; set; } = 0;
    /// <summary>Ruta local del logo del mandante/cliente usado en exportaciones.</summary>
    public string LetterheadClientLogoPath { get; set; } = "";

    // ── Paginación ────────────────────────────────────────────────────────
    /// <summary>Filas por página en la grilla de materiales.</summary>
    public int MaterialsPageSize { get; set; } = 250;

    // ── Panel izquierdo de cubicación ─────────────────────────────────────
    /// <summary>Ancho del panel de partidas en la vista Cubicación.</summary>
    public int CubicacionPanelWidth { get; set; } = 485;

    // ── Banco de precios conectado ────────────────────────────────────────
    /// <summary>
    /// URLs de feeds de precios. Cada entrada apunta a un JSON con estructura
    /// [{"codigo":"XXX","matUf":0.0,"moUf":0.0,"fecha":"2025-01-01"}].
    /// </summary>
    public List<string> PriceFeedUrls { get; set; } = new();

    // ── Funciones experimentales ──────────────────────────────────────────
    /// <summary>Habilita el acceso a funciones experimentales en el menú secundario.</summary>
    public bool ExperimentalFeaturesEnabled { get; set; } = false;

    /// <summary>Política de PIN para cuentas locales del equipo.</summary>
    public PinSecurityPolicy PinSecurity { get; set; } = new();

    // ── Atajos de teclado ─────────────────────────────────────────────────
    /// <summary>
    /// Atajos personalizados por acción (clave = id del catálogo, valor = gesto tipo Alt+L).
    /// Las acciones ausentes usan el valor por defecto del catálogo.
    /// </summary>
    public Dictionary<string, string> KeyboardShortcuts { get; set; } = new();

    // ── Memoria de observaciones y justificaciones ──────────────────────
    /// <summary>Memoria de observaciones y justificaciones frecuentes y recientes del usuario.</summary>
    public List<string> RecentObservations { get; set; } = new()
    {
        "Por compañía eléctrica",
        "Suministrado por Mandante",
        "Valor proforma",
        "No considerado en bases técnicas",
        "Alternativa / Opcional",
        "Se incluye en ítem matriz"
    };
}
