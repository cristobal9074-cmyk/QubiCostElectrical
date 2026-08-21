using System.Collections.Generic;
using System.Drawing;

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

    // ── Ventana ───────────────────────────────────────────────────────────
    /// <summary>Estado de la ventana al cerrar: "Maximized" | "Normal"</summary>
    public string WindowState  { get; set; } = "Maximized";
    public int    WindowX      { get; set; } = 100;
    public int    WindowY      { get; set; } = 100;
    public int    WindowWidth  { get; set; } = 1600;
    public int    WindowHeight { get; set; } = 950;

    // ── Navegación ────────────────────────────────────────────────────────
    /// <summary>Última pestaña abierta al cerrar la app.</summary>
    public string LastPage        { get; set; } = "Cubicacion";
    /// <summary>Email del último usuario que inició sesión (para pre-llenar login).</summary>
    public string LastUserEmail { get; set; } = "";
    /// <summary>Ruta del último proyecto abierto (para ofrecer reapertura).</summary>
    public string LastProjectPath { get; set; } = "";
    /// <summary>Últimas N rutas abiertas (max 8), más reciente primero.</summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Estado expandido/colapsado de cada grupo del sidebar por clave de módulo.</summary>
    public Dictionary<string, bool> SidebarGroupsExpanded { get; set; } = new();

    /// <summary>Modo compacto del sidebar: sólo íconos para ganar espacio en pantallas pequeñas.</summary>
    public bool SidebarCompactMode { get; set; } = true;

    /// <summary>Historial de comandos ejecutados desde la Command Palette.</summary>
    public List<string> CommandHistory { get; set; } = new();

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

    // ── Grillas: anchos de columna ────────────────────────────────────────
    /// <summary>
    /// Ancho de columnas por grilla.
    /// Clave externa: nombre de grilla ("Materiales", "Itemizado").
    /// Clave interna: nombre de columna tal como aparece en DataGridView.Columns.
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> GridColumnWidths { get; set; } = new();

    // ── Grillas: visibilidad de columnas ──────────────────────────────────
    /// <summary>Columnas ocultas por el usuario.</summary>
    public Dictionary<string, List<string>> GridHiddenColumns { get; set; } = new();

    /// <summary>Orden visual de columnas por grilla, persistido por DisplayIndex.</summary>
    public Dictionary<string, Dictionary<string, int>> GridColumnDisplayIndexes { get; set; } = new();

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
}
