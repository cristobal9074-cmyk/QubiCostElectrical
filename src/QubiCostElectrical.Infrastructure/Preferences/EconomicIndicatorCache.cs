using System;
using System.IO;
using System.Text.Json;

namespace QubiCostElectrical;

/// <summary>
/// Caché global de indicadores económicos (UF y dólar observado) en %LOCALAPPDATA%.
/// Pre-llena proyectos nuevos y advierte cuando los datos son del día anterior o más.
/// </summary>
public static class EconomicIndicatorCache
{
    public const int StaleHours = 24;

    private const string FileName = "indicators_cache.json";
    private static string? cachePathOverride;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string CachePath
    {
        get
        {
            return cachePathOverride ?? ProductPaths.EconomicIndicatorsCacheFile();
        }
    }

    public static IDisposable UseCachePathForTests(string path)
    {
        string? previous = cachePathOverride;
        cachePathOverride = path;
        return new CachePathScope(previous);
    }

    public sealed class CachedIndicators
    {
        public decimal UfClp { get; set; }
        public decimal UsdClp { get; set; }
        public DateTime FetchedAt { get; set; } = DateTime.MinValue;
    }

    /// <summary>
    /// Lee la caché. Devuelve null si el archivo no existe o está corrupto.
    /// Nunca lanza excepción.
    /// </summary>
    public static CachedIndicators? TryRead()
    {
        try
        {
            string path = CachePath;
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
            var result = JsonSerializer.Deserialize<CachedIndicators>(json, JsonOpts);
            if (result == null || result.UfClp <= 0) return null;
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Escribe los indicadores en la caché (escritura atómica vía archivo temporal).
    /// Nunca lanza excepción.
    /// </summary>
    public static void Write(decimal ufClp, decimal usdClp, DateTime fetchedAt)
    {
        try
        {
            string path = CachePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var data = new CachedIndicators { UfClp = ufClp, UsdClp = usdClp, FetchedAt = fetchedAt };
            string json = JsonSerializer.Serialize(data, JsonOpts);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json, System.Text.Encoding.UTF8);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
        }
    }

    /// <summary>Devuelve true si los indicadores tienen más de <see cref="StaleHours"/> horas.</summary>
    public static bool IsStale(DateTime fetchedAt)
    {
        if (fetchedAt == DateTime.MinValue) return true;
        return (DateTime.UtcNow - fetchedAt.ToUniversalTime()).TotalHours >= StaleHours;
    }

    private sealed class CachePathScope(string? previous) : IDisposable
    {
        public void Dispose() => cachePathOverride = previous;
    }
}
