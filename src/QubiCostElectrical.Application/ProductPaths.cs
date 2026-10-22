#nullable enable
using System.IO;

namespace QubiCostElectrical;

public static class ProductPaths
{
    private static readonly Lazy<bool> developmentProfile = new(() =>
        ResolveDevelopmentProfile(
            Environment.GetEnvironmentVariable(ProductIdentity.DevelopmentProfileEnvironmentVariable),
            Environment.ProcessPath));

    /// <summary>
    /// La instalación publicada (Velopack, %LOCALAPPDATA%\...\current\) y la build de desarrollo del
    /// repositorio (publish\win-x64) deben poder correr a la vez: cada una con su propio mutex,
    /// su propia tubería de handoff y su propia carpeta de datos. Sin esto, el segundo proceso se
    /// cierra solo por la instancia única y ambas compiten por auth.db, la sesión y los autosaves.
    /// </summary>
    public static bool IsDevelopmentProfile() => developmentProfile.Value;

    public static bool ResolveDevelopmentProfile(string? profileValue, string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(profileValue))
            return profileValue.Trim().Equals(ProductIdentity.DevelopmentProfileFolderName, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(executablePath))
            return false;

        return executablePath.Contains(@"\publish\", StringComparison.OrdinalIgnoreCase);
    }

    public static string InstanceNameSuffix()
        => IsDevelopmentProfile() ? "_" + ProductIdentity.DevelopmentProfileFolderName : string.Empty;

    public static string GetLocalAppDataRoot()
    {
        if (IsDevelopmentProfile())
        {
            string dev = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductIdentity.AppDataFolderName,
                ProductIdentity.DevelopmentProfileFolderName);
            Directory.CreateDirectory(dev);
            return dev;
        }

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

    public static string BackupsDirectory()
        => Path.Combine(GetLocalAppDataRoot(), "Backups");

    public static string ProjectBackupDirectory(string projectPath)
    {
        string fileName = Path.GetFileName(projectPath);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "ProyectoSinNombre";

        uint hash = 2166136261;
        string normalized = (projectPath ?? "").Trim().ToLowerInvariant();
        foreach (char c in normalized)
        {
            hash = (hash ^ (uint)c) * 16777619;
        }

        string folderName = $"{fileName}_{hash:x8}";
        return Path.Combine(BackupsDirectory(), folderName);
    }

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
