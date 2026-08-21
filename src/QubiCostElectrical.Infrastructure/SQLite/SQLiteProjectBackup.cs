using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public static class SQLiteProjectBackup
{
    public static bool IsDatabaseHealthy(string databasePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
                return false;

            using var connection = OpenReadOnly(databasePath);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check;";
            string? result = Convert.ToString(cmd.ExecuteScalar());
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            AppLog.Warning("SQLite", $"quick_check falló para '{databasePath}'", ex);
            return false;
        }
    }

    public static string CreateNumberedBackup(string databasePath, int maxBackups, string reason)
    {
        if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
            return "";

        if (maxBackups <= 0)
            maxBackups = 10;

        // Antes de copiar, forzamos checkpoint para que el .db tenga todo lo que está en WAL.
        CheckpointWal(databasePath);

        if (!IsDatabaseHealthy(databasePath))
            throw new InvalidDataException("SQLite no pasó quick_check; no se crea backup para no rotar una base corrupta.");

        string dir = BackupDirectory(databasePath);
        Directory.CreateDirectory(dir);

        // Rotación numerada: .bak.01 es el más reciente, .bak.N el más antiguo.
        string oldest = BackupPath(databasePath, maxBackups);
        if (File.Exists(oldest)) File.Delete(oldest);

        for (int i = maxBackups - 1; i >= 1; i--)
        {
            string source = BackupPath(databasePath, i);
            if (!File.Exists(source)) continue;

            string target = BackupPath(databasePath, i + 1);
            if (File.Exists(target)) File.Delete(target);
            File.Move(source, target);
        }

        string latest = BackupPath(databasePath, 1);
        File.Copy(databasePath, latest, overwrite: true);

        File.WriteAllText(
            Path.Combine(dir, "backup_manifest.txt"),
            $"Último backup: {AppClock.Now:yyyy-MM-dd HH:mm:ss}\nBase: {Path.GetFileName(databasePath)}\nBackup: {Path.GetFileName(latest)}\nMotivo: {reason}\nBackups máximos: {maxBackups}\n",
            System.Text.Encoding.UTF8);

        return latest;
    }

    public static string GetLatestUsableBackup(string databasePath)
    {
        try
        {
            string dir = BackupDirectory(databasePath);
            if (!Directory.Exists(dir)) return "";

            var candidates = Enumerable.Range(1, 50)
                .Select(i => BackupPath(databasePath, i))
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .OrderBy(info => BackupSlotNumber(info.FullName))
                .ThenByDescending(info => info.LastWriteTimeUtc)
                .ToList();

            foreach (var file in candidates)
            {
                if (IsDatabaseHealthy(file.FullName))
                    return file.FullName;
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("SQLite", $"No se pudo localizar un backup utilizable para '{databasePath}'", ex);
        }

        return "";
    }

    public static bool RestoreBackup(string backupPath, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(backupPath) || string.IsNullOrWhiteSpace(databasePath))
            return false;

        if (!File.Exists(backupPath))
            return false;

        if (!IsDatabaseHealthy(backupPath))
            return false;

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory);

        DeleteIfExists(databasePath + "-wal");
        DeleteIfExists(databasePath + "-shm");
        File.Copy(backupPath, databasePath, overwrite: true);
        DeleteIfExists(databasePath + "-wal");
        DeleteIfExists(databasePath + "-shm");
        return IsDatabaseHealthy(databasePath);
    }

    private static void CheckpointWal(string databasePath)
    {
        using var connection = Open(databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
    }

    private static SqliteConnection Open(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static SqliteConnection OpenReadOnly(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static string BackupDirectory(string databasePath)
    {
        string dir = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
        string file = Path.GetFileName(databasePath);
        return Path.Combine(dir, file + ".backups");
    }

    private static string BackupPath(string databasePath, int slot)
    {
        string file = Path.GetFileName(databasePath);
        return Path.Combine(BackupDirectory(databasePath), $"{file}.bak.{slot:00}");
    }

    private static int BackupSlotNumber(string path)
    {
        string name = Path.GetFileName(path);
        int idx = name.LastIndexOf(".bak.", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return int.MaxValue;
        string number = name[(idx + 5)..];
        return int.TryParse(number, out int result) ? result : int.MaxValue;
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }
}
