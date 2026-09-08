#nullable enable
using System.IO;

namespace QubiCostElectrical;

public static class ProductPaths
{
    public static string GetLocalAppDataRoot()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string canonical = Path.Combine(local, ProductIdentity.AppDataFolderName);
        string legacy = Path.Combine(local, ProductIdentity.LegacyAppDataFolderName);

        if (Directory.Exists(canonical))
            return canonical;

        if (Directory.Exists(legacy))
        {
            try
            {
                Directory.Move(legacy, canonical);
                return canonical;
            }
            catch
            {
                return legacy;
            }
        }

        Directory.CreateDirectory(canonical);
        return canonical;
    }

    public static string AutoSaveDirectory()
        => Path.Combine(GetLocalAppDataRoot(), "Autosave");

    public static string DatabaseDirectory()
        => Path.Combine(GetLocalAppDataRoot(), "Database");

    public static string PreferencesFile()
        => Path.Combine(GetLocalAppDataRoot(), "preferences.json");

    public static string AuthDatabaseFile()
        => Path.Combine(GetLocalAppDataRoot(), "auth.db");

    public static string LicenseFile()
        => Path.Combine(GetLocalAppDataRoot(), "license.qclic");

    public static string PriceBankDatabaseFile()
        => Path.Combine(GetLocalAppDataRoot(), "banco_precios.db");

    public static string ResolvePriceBankDatabaseFile(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath.Trim();
        return PriceBankDatabaseFile();
    }

    public static bool IsUncPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        return trimmed.StartsWith(@"\\", StringComparison.Ordinal);
    }

    public static bool IsNetworkOrUncPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (IsUncPath(path))
            return true;

        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                return drive.DriveType == DriveType.Network;
            }
        }
        catch
        {
            // Ignorar excepciones al consultar DriveInfo en rutas virtuales o no accesibles
        }

        return false;
    }

    public static bool IsAbsolutePriceBankPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        return IsUncPath(trimmed) || Path.IsPathRooted(trimmed);
    }

    private static readonly string[] CloudSyncFolderNames =
    {
        "OneDrive", "Dropbox", "Google Drive", "GoogleDrive", "iCloudDrive", "iCloud Drive", "Box Sync", "BoxSync"
    };

    /// <summary>
    /// SQLite no soporta escritura concurrente multiusuario de forma segura: si la ruta cae dentro
    /// de una carpeta sincronizada por un cliente de nube (OneDrive/Dropbox/Drive/iCloud), el archivo
    /// puede sincronizarse a medio escribir y corromperse. Solo se detectan rutas locales; una ruta
    /// UNC apunta a un share de red, no a una carpeta de sync local.
    /// </summary>
    public static bool IsLikelyCloudSyncPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsUncPath(path))
            return false;

        var segments = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            foreach (var cloudName in CloudSyncFolderNames)
            {
                if (string.Equals(segment, cloudName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public static string EconomicIndicatorsCacheFile()
        => Path.Combine(GetLocalAppDataRoot(), "indicators_cache.json");
}
