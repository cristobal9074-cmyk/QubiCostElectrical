using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical.Infrastructure;

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
public class UserPreferencesStore : IUserPreferencesStore
{
    UserPreferences IUserPreferencesStore.Load() => Load();
    void IUserPreferencesStore.Save(UserPreferences prefs) => Save(prefs);
    string IUserPreferencesStore.PreferencesFilePath => PreferencesFilePath;
    IDisposable IUserPreferencesStore.UsePreferencesFilePathForTests(string? path) => UsePreferencesFilePathForTests(path);

    private const string FileName = "preferences.json";
    private static string? preferencesFilePathOverride;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
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
            {
                var initialized = CreateInitialPreferences();
                if (initialized != null)
                {
                    Save(initialized);
                    return initialized;
                }
                return new UserPreferences();
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return new UserPreferences();

            var prefs = JsonSerializer.Deserialize<UserPreferences>(json, _opts);
            var sanitized = prefs is null ? new UserPreferences() : Sanitize(prefs);

            // Si los proyectos recientes aún no se han inicializado o están vacíos (por ejemplo tras una actualización o perfil nuevo),
            // intentar rescatar de perfil hermano / instalación previa o auto-descubrir de la carpeta de presupuestos (estilo Office 365)
            if (sanitized.RecentFiles.Count == 0 && !sanitized.RecentProjectsInitialized && preferencesFilePathOverride == null)
            {
                if (TryEnrichRecentProjects(sanitized))
                {
                    sanitized.RecentProjectsInitialized = true;
                    Save(sanitized);
                }
            }
            else if (sanitized.RecentFiles.Count > 0 && !sanitized.RecentProjectsInitialized)
            {
                sanitized.RecentProjectsInitialized = true;
                Save(sanitized);
            }

            return sanitized;
        }
        catch (Exception ex)
        {
            // Archivo corrupto u otro error de IO → preferencias por defecto, sin bloquear la app.
            AppLog.Warning("Preferencias", "No se pudieron leer las preferencias; se usan los valores por defecto", ex);
            return new UserPreferences();
        }
    }

    private static UserPreferences? CreateInitialPreferences()
    {
        if (preferencesFilePathOverride != null)
            return null;

        var prefs = new UserPreferences();
        if (TryEnrichRecentProjects(prefs))
        {
            prefs.RecentProjectsInitialized = true;
            return prefs;
        }

        return null;
    }

    private static bool TryEnrichRecentProjects(UserPreferences prefs)
    {
        if (preferencesFilePathOverride != null)
            return false;

        bool modified = false;

        // 1. Intentar importar de perfiles hermanos o legados si existen
        foreach (var candidate in GetLegacyPreferencePaths())
        {
            if (File.Exists(candidate))
            {
                try
                {
                    var text = File.ReadAllText(candidate, Encoding.UTF8);
                    var legacy = JsonSerializer.Deserialize<UserPreferences>(text, _opts);
                    if (legacy != null && legacy.RecentFiles.Count > 0)
                    {
                        foreach (var f in legacy.RecentFiles)
                        {
                            if (!string.IsNullOrWhiteSpace(f) && File.Exists(f) && !prefs.RecentFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                            {
                                prefs.RecentFiles.Add(f);
                                modified = true;
                            }
                        }
                        foreach (var p in legacy.PinnedRecentFiles)
                        {
                            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p) && !prefs.PinnedRecentFiles.Contains(p, StringComparer.OrdinalIgnoreCase))
                            {
                                prefs.PinnedRecentFiles.Add(p);
                                modified = true;
                            }
                        }
                        if (string.IsNullOrWhiteSpace(prefs.LastUserEmail) && !string.IsNullOrWhiteSpace(legacy.LastUserEmail))
                        {
                            prefs.LastUserEmail = legacy.LastUserEmail;
                            modified = true;
                        }
                    }
                }
                catch { }
            }
            if (prefs.RecentFiles.Count > 0)
                break;
        }

        // 2. Si todavía está vacío, auto-descubrir proyectos locales (.qce) como Office 365
        if (prefs.RecentFiles.Count == 0)
        {
            var discovered = AutoDiscoverProjects(prefs.DefaultProjectFolder);
            if (discovered.Count > 0)
            {
                prefs.RecentFiles.AddRange(discovered);
                modified = true;
            }
        }

        return modified;
    }

    private static IEnumerable<string> GetLegacyPreferencePaths()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (ProductPaths.IsDevelopmentProfile())
        {
            yield return Path.Combine(local, ProductIdentity.AppDataFolderName, FileName);
            yield return Path.Combine(local, "QubiCostElectrical-Development", FileName);
        }
        else
        {
            yield return Path.Combine(local, "QubiCostElectrical-Development", FileName);
        }
        yield return Path.Combine(local, ProductIdentity.LegacyAppDataFolderName, FileName);
        yield return Path.Combine(local, "PresupuestoElectricoPRO", FileName);
    }

    private static List<string> AutoDiscoverProjects(string? defaultFolder)
    {
        var candidatesFolders = new List<string>();

        if (!string.IsNullOrWhiteSpace(defaultFolder) && Directory.Exists(defaultFolder))
            candidatesFolders.Add(defaultFolder);

        string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(myDocs) && Directory.Exists(myDocs))
        {
            string p1 = Path.Combine(myDocs, "Presupuestos Electricos");
            if (Directory.Exists(p1)) candidatesFolders.Add(p1);

            string p2 = Path.Combine(myDocs, "Qubi Cost Electrical");
            if (Directory.Exists(p2)) candidatesFolders.Add(p2);

            string p3 = Path.Combine(myDocs, "QubiCostElectrical");
            if (Directory.Exists(p3)) candidatesFolders.Add(p3);

            candidatesFolders.Add(myDocs);
        }

        var foundFiles = new List<FileInfo>();
        foreach (var folder in candidatesFolders)
        {
            try
            {
                var dir = new DirectoryInfo(folder);
                var files = dir.GetFiles("*.qce", SearchOption.TopDirectoryOnly);
                foreach (var f in files)
                {
                    if (IsCandidateProjectFile(f))
                        foundFiles.Add(f);
                }
            }
            catch { }
        }

        return foundFiles
            .GroupBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(8)
            .Select(f => f.FullName)
            .ToList();
    }

    private static bool IsCandidateProjectFile(FileInfo file)
    {
        if (!file.Extension.Equals(".qce", StringComparison.OrdinalIgnoreCase))
            return false;

        string name = file.Name;
        if (name.StartsWith(".", StringComparison.Ordinal))
            return false;
        if (name.Contains(".autosave", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Contains(".bak", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Contains(".pre-", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Contains(".backup", StringComparison.OrdinalIgnoreCase))
            return false;
        if (name.Contains(".sobrescrito", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
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
            string tmp = path + ".tmp";

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
        p.LetterheadRut = (p.LetterheadRut ?? "").Trim();
        p.LetterheadAddress = (p.LetterheadAddress ?? "").Trim();
        p.LetterheadPhone = (p.LetterheadPhone ?? "").Trim();
        p.LetterheadEmail = (p.LetterheadEmail ?? "").Trim();
        p.LetterheadWebsite = (p.LetterheadWebsite ?? "").Trim();
        p.LetterheadLogoPath = (p.LetterheadLogoPath ?? "").Trim();
        p.ProfessionalName = (p.ProfessionalName ?? "").Trim();
        p.ProfessionalRut = (p.ProfessionalRut ?? "").Trim();
        p.ProfessionalRole = (p.ProfessionalRole ?? "").Trim();
        p.ProfessionalRegistroSec = (p.ProfessionalRegistroSec ?? "").Trim();
        p.ProfessionalSignatureImagePath = (p.ProfessionalSignatureImagePath ?? "").Trim();
        p.ProfessionalPhone = (p.ProfessionalPhone ?? "").Trim();
        p.ProfessionalEmail = (p.ProfessionalEmail ?? "").Trim();
        p.ProfessionalSecClass = (p.ProfessionalSecClass ?? "").Trim();
        p.LetterheadClientLogoPath = (p.LetterheadClientLogoPath ?? "").Trim();
        if (p.ProfessionalSignatureWidthMm < 0) p.ProfessionalSignatureWidthMm = 0;
        if (p.ProfessionalSignatureHeightMm < 0) p.ProfessionalSignatureHeightMm = 0;

        // Listas nulas (archivo generado con versión anterior)
        p.RecentFiles ??= new();
        p.SidebarGroupsExpanded ??= new();
        p.CorporateTheme ??= new CorporateThemePreferences();
        p.DashboardWidgetLayout ??= new DashboardWidgetLayoutPreferences();
        p.ReportTemplate ??= new ReportTemplatePreferences();
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
