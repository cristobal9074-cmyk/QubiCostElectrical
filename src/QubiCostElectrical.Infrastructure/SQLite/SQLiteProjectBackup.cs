using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

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

        // Liberar pools para asegurar que no queden file handles abiertos en archivos .bak
        SqliteConnection.ClearAllPools();

        // Antes de copiar, forzamos checkpoint para que el .db tenga todo lo que está en WAL.
        CheckpointWal(databasePath);

        if (!IsDatabaseHealthy(databasePath))
            throw new InvalidDataException("SQLite no pasó quick_check; no se crea backup para no rotar una base corrupta.");

        string dir = BackupDirectory(databasePath);
        Directory.CreateDirectory(dir);

        // Rotación numerada: .bak.01 es el más reciente, .bak.N el más antiguo.
        string oldest = BackupPath(databasePath, maxBackups);
        SafeDeleteFile(oldest);

        for (int i = maxBackups - 1; i >= 1; i--)
        {
            string source = BackupPath(databasePath, i);
            string target = BackupPath(databasePath, i + 1);
            SafeRotateBackupSlot(source, target);
        }

        string latest = BackupPath(databasePath, 1);
        SafeDeleteFile(latest);

        using (var sourceConnection = Open(databasePath))
        {
            var destBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = latest,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            };
            using var destConnection = new SqliteConnection(destBuilder.ToString());
            destConnection.Open();
            sourceConnection.BackupDatabase(destConnection);
            destConnection.Close();
            SqliteConnection.ClearPool(destConnection);
        }

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

        SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory);

        SafeDeleteFile(databasePath + "-wal");
        SafeDeleteFile(databasePath + "-shm");
        File.Copy(backupPath, databasePath, overwrite: true);
        SafeDeleteFile(databasePath + "-wal");
        SafeDeleteFile(databasePath + "-shm");
        return IsDatabaseHealthy(databasePath);
    }

    private static void CheckpointWal(string databasePath)
    {
        using var connection = Open(databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
    }

    private static void SafeRotateBackupSlot(string source, string target)
    {
        if (!File.Exists(source)) return;

        SafeDeleteFile(target);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Move(source, target);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                SqliteConnection.ClearAllPools();
                System.Threading.Thread.Sleep(60);
                SafeDeleteFile(target);
            }
            catch (Exception ex)
            {
                try
                {
                    File.Copy(source, target, overwrite: true);
                    SafeDeleteFile(source);
                    return;
                }
                catch
                {
                    AppLog.Warning("SQLite", $"No se pudo rotar slot de backup de '{source}' a '{target}'.", ex);
                    return;
                }
            }
        }
    }

    private static void SafeDeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                SqliteConnection.ClearAllPools();
                System.Threading.Thread.Sleep(50);
            }
            catch
            {
                break;
            }
        }
    }

    private static SqliteConnection Open(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout = 5000;";
        cmd.ExecuteNonQuery();
        return connection;
    }

    private static SqliteConnection OpenReadOnly(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SQLiteBackup] No se pudo eliminar archivo temporal {path}: {ex.Message}");
        }
    }
}
