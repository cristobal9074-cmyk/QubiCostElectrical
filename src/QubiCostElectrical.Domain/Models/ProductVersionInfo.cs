using System.Reflection;
using System.Linq;

namespace QubiCostElectrical;

public static class ProductVersionInfo
{
    public static int DatabaseSchemaVersion => ReadMetadataInt(nameof(DatabaseSchemaVersion), 20);
    public static int ProjectFormatVersion => ReadMetadataInt(nameof(ProjectFormatVersion), 6);
    public static int CalculationEngineVersion => ReadMetadataInt(nameof(CalculationEngineVersion), 2);

    public static string ProductVersion => AssemblyVersion(3);
    public static string ProductVersionLabel => $"v{ProductVersion}";
    public static string ProjectFormatLabel => $"qce-{ProjectFormatVersion}";
    public static string CalculationEngineLabel => $"cost-engine-{CalculationEngineVersion}";

    /// <summary>
    /// URL del repositorio GitHub donde se publican los releases de Velopack.
    /// Cambiar antes de distribuir: "https://github.com/USUARIO/REPO"
    /// </summary>
    public const string GitHubReleasesUrl = "https://github.com/smingspa/presupuesto-electrico-pro";

    public static string TechnicalSummary =>
        $"Producto {ProductVersionLabel} · DB {DatabaseSchemaVersion} · Proyecto {ProjectFormatVersion} · Motor {CalculationEngineVersion}";

    private static int ReadMetadataInt(string key, int defaultValue)
    {
        var attrs = typeof(ProductVersionInfo).Assembly.GetCustomAttributes(false);
        var attr = attrs.OfType<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
        if (attr?.Value is string value && int.TryParse(value, out int result))
            return result;
        return defaultValue;
    }

    private static string AssemblyVersion(int fieldCount)
    {
        var v = typeof(ProductVersionInfo).Assembly.GetName().Version;
        return v is null ? "0.0.0" : fieldCount switch
        {
            1 => $"{v.Major}",
            2 => $"{v.Major}.{v.Minor}",
            3 => $"{v.Major}.{v.Minor}.{v.Build}",
            _ => v.ToString()
        };
    }
}
