using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical;

/// <summary>
/// Persiste <see cref="UserPreferences"/> en JSON dentro de %LOCALAPPDATA%.
///
/// Ruta:  %LOCALAPPDATA%\QubiCostElectrical\preferences.json
///
/// Reglas de diseño:
/// - Load() nunca lanza excepción: devuelve defaults si el archivo no existe o está corrupto.
/// - Save() nunca lanza: escribe en un .tmp y luego renombra (escritura atómica).
/// - Sin dependencias externas; usa sólo System.Text.Json (ya en el proyecto).
/// </summary>
public static class UserPreferencesStore
{
    private const string FileName     = "preferences.json";
    private static string? preferencesFilePathOverride;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented            = true,
        PropertyNameCaseInsensitive = true,
    };

    // ── Ruta ──────────────────────────────────────────────────────────────

    public static string PreferencesFilePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(preferencesFilePathOverride))
                return preferencesFilePathOverride;

            return ProductPaths.PreferencesFile();
        }
    }

    public static IDisposable UsePreferencesFilePathForTests(string? path)
    {
        string? previous = preferencesFilePathOverride;
        preferencesFilePathOverride = path;
        return new PreferencesFilePathScope(previous);
    }

    // ── Carga ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Carga las preferencias del disco.
    /// Si el archivo no existe o está dañado, devuelve una instancia con valores por defecto.
    /// </summary>
    public static UserPreferences Load()
    {
        try
        {
            string path = PreferencesFilePath;
            if (!File.Exists(path))
                return new UserPreferences();

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return new UserPreferences();

            var prefs = JsonSerializer.Deserialize<UserPreferences>(json, _opts);
            return prefs is null ? new UserPreferences() : Sanitize(prefs);
        }
        catch (Exception ex)
        {
            // Archivo corrupto u otro error de IO → preferencias por defecto, sin bloquear la app.
            AppLog.Warning("Preferencias", "No se pudieron leer las preferencias; se usan los valores por defecto", ex);
            return new UserPreferences();
        }
    }

    // ── Guardado ──────────────────────────────────────────────────────────

    /// <summary>
    /// Guarda las preferencias en disco de forma atómica (escribe en .tmp, luego mueve).
    /// No lanza excepción si falla.
    /// </summary>
    public static void Save(UserPreferences prefs)
    {
        try
        {
            string path = PreferencesFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string json = JsonSerializer.Serialize(prefs, _opts);
            string tmp  = path + ".tmp";

            File.WriteAllText(tmp, json, Encoding.UTF8);

            // Reemplaza atómicamente (en Windows mueve de forma atómica dentro del mismo volumen)
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            // No crítico: la app sigue funcionando sin preferencias guardadas.
            AppLog.Warning("Preferencias", "No se pudieron guardar las preferencias en disco", ex);
        }
    }

    // ── Sanitización ──────────────────────────────────────────────────────

    /// <summary>Corrige valores fuera de rango provenientes de archivos antiguos o editados a mano.</summary>
    private static UserPreferences Sanitize(UserPreferences p)
    {
        // Fuente: clamp entre 7.5 y 13 pt
        if (p.FontSize < 7.5f || p.FontSize > 13f)
            p.FontSize = 9.0f;

        // Tamaño de ventana: mínimos razonables
        if (p.WindowWidth  < 800)  p.WindowWidth  = 1600;
        if (p.WindowHeight < 600)  p.WindowHeight = 950;

        // Página de materiales
        if (p.MaterialsPageSize is not (50 or 100 or 250 or 500 or 1000))
            p.MaterialsPageSize = 250;

        // Panel cubicación
        if (p.CubicacionPanelWidth < 200) p.CubicacionPanelWidth = 485;

        // Onboarding: normaliza nulos de archivos antiguos.
        p.OnboardingVersion = (p.OnboardingVersion ?? "").Trim();

        // Autenticación.
        p.LastUserEmail = (p.LastUserEmail ?? "").Trim().ToLowerInvariant();

        // Archivos y rutas.
        p.DefaultProjectFolder = (p.DefaultProjectFolder ?? "").Trim();

        // Membrete corporativo: normaliza nulos de archivos antiguos.
        p.LetterheadCompanyName = (p.LetterheadCompanyName ?? "").Trim();
        p.LetterheadRut         = (p.LetterheadRut ?? "").Trim();
        p.LetterheadAddress     = (p.LetterheadAddress ?? "").Trim();
        p.LetterheadPhone       = (p.LetterheadPhone ?? "").Trim();
        p.LetterheadEmail       = (p.LetterheadEmail ?? "").Trim();
        p.LetterheadWebsite     = (p.LetterheadWebsite ?? "").Trim();
        p.LetterheadLogoPath               = (p.LetterheadLogoPath ?? "").Trim();
        p.ProfessionalName                 = (p.ProfessionalName ?? "").Trim();
        p.ProfessionalRut                  = (p.ProfessionalRut ?? "").Trim();
        p.ProfessionalRole                 = (p.ProfessionalRole ?? "").Trim();
        p.ProfessionalRegistroSec          = (p.ProfessionalRegistroSec ?? "").Trim();
        p.ProfessionalSignatureImagePath   = (p.ProfessionalSignatureImagePath ?? "").Trim();

        // Listas nulas (archivo generado con versión anterior)
        p.RecentFiles             ??= new();
        p.SidebarGroupsExpanded   ??= new();
        p.GridColumnWidths        ??= new();
        p.GridHiddenColumns       ??= new();
        p.GridColumnDisplayIndexes ??= new();
        p.CorporateTheme          ??= new CorporateThemePreferences();
        p.DashboardWidgetLayout   ??= new DashboardWidgetLayoutPreferences();
        p.ReportTemplate          ??= new ReportTemplatePreferences();
        p.DashboardWidgetLayout.WidgetOrder ??= new List<string> { "Alertas", "KPIs", "TopPartidas", "MaterialesCriticos", "Areas", "Revision", "Sensibilidad" };
        p.DashboardWidgetLayout.WidgetHeights ??= new Dictionary<string, int>();
        p.ReportTemplate.OrderedFields ??= new List<string> { "Logo", "Proyecto", "Cliente", "Resumen UF", "Itemizado", "Condiciones", "Firma" };
        p.CorporateTheme.Name = string.IsNullOrWhiteSpace(p.CorporateTheme.Name) ? "Corporativo" : p.CorporateTheme.Name.Trim();
        p.CorporateTheme.CompanyName = string.IsNullOrWhiteSpace(p.CorporateTheme.CompanyName) ? p.LetterheadCompanyName : p.CorporateTheme.CompanyName.Trim();
        p.CorporateTheme.PrimaryHex = NormalizeHex(p.CorporateTheme.PrimaryHex, "#0C3B5E");
        p.CorporateTheme.AccentHex = NormalizeHex(p.CorporateTheme.AccentHex, "#0F6CBD");
        p.CorporateTheme.BackgroundHex = NormalizeHex(p.CorporateTheme.BackgroundHex, "#F7F9FC");
        p.CorporateTheme.LogoPath = (p.CorporateTheme.LogoPath ?? "").Trim();
        p.CorporateTheme.FontFamily = string.IsNullOrWhiteSpace(p.CorporateTheme.FontFamily) ? "Segoe UI" : p.CorporateTheme.FontFamily.Trim();

        // Recientes: máx 8 entradas
        while (p.RecentFiles.Count > 8)
            p.RecentFiles.RemoveAt(p.RecentFiles.Count - 1);

        // Funciones experimentales: default false si no existe en archivo antiguo.
        // No requiere sanitización adicional (bool).

        KeyboardShortcutPreferences.Sanitize(p);

        return p;
    }

    private static string NormalizeHex(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string hex = value.Trim();
        if (!hex.StartsWith("#", StringComparison.Ordinal)) hex = "#" + hex;
        string raw = hex[1..];
        if (raw.Length == 3) raw = string.Concat(raw.Select(c => new string(c, 2)));
        if (raw.Length != 6 || raw.Any(c => !Uri.IsHexDigit(c))) return fallback;
        return "#" + raw.ToUpperInvariant();
    }

    private sealed class PreferencesFilePathScope(string? previous) : IDisposable
    {
        public void Dispose() => preferencesFilePathOverride = previous;
    }

}
