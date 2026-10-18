using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class SqliteMigrationLedger
{
    internal static int DetectLegacySchemaVersion(SqliteConnection connection)
    {
        if (!HasAnyProjectTable(connection)) return 0;
        if (SqliteMigratorHelpers.TableExists(connection, "ordenes_cambio")) return 4;
        if (SqliteMigratorHelpers.TableExists(connection, "apu")) return 3;
        if (SqliteMigratorHelpers.TableExists(connection, "versiones")) return 2;
        if (SqliteMigratorHelpers.TableExists(connection, "project_info")) return 1;
        return 0;
    }

    internal static bool HasAnyProjectTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name <> 'schema_migrations';
            """;
        object? value = cmd.ExecuteScalar();
        return Convert.ToInt32(value ?? 0) > 0;
    }

    internal static void BackfillMigrationLedger(SqliteConnection connection, int currentVersion, System.Collections.Generic.IEnumerable<(int Version, string Name)> steps)
    {
        using var transaction = connection.BeginTransaction();
        try
        {
            CreateMigrationLedger(connection, transaction);
            foreach (var step in steps.Where(x => x.Version <= currentVersion).OrderBy(x => x.Version))
            {
                RecordMigration(connection, transaction, step.Version, step.Name + " (detectada desde base antigua)");
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    internal static void CreateMigrationLedger(SqliteConnection connection, SqliteTransaction? transaction)
    {
        SqliteMigratorHelpers.Execute(connection, transaction, """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                applied_at TEXT NOT NULL,
                app_version TEXT NOT NULL,
                note TEXT NOT NULL DEFAULT ''
            );
            """);
    }

    internal static void RecordMigration(SqliteConnection connection, SqliteTransaction transaction, int version, string name)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO schema_migrations (version, name, applied_at, app_version, note)
            VALUES ($version, $name, $applied_at, $app_version, $note)
            ON CONFLICT(version) DO UPDATE SET
                name = excluded.name,
                applied_at = excluded.applied_at,
                app_version = excluded.app_version,
                note = excluded.note;
            """;
        cmd.Parameters.AddWithValue("$version", version);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$applied_at", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$app_version", ProductVersionInfo.ProductVersionLabel);
        cmd.Parameters.AddWithValue("$note", "Migración automática con PRAGMA user_version.");
        cmd.ExecuteNonQuery();
    }

    internal static void UpsertMetaIfPossible(SqliteConnection connection, SqliteTransaction? transaction, string key, string value)
    {
        if (!SqliteMigratorHelpers.TableExists(connection, "app_meta", transaction)) return;

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO app_meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    internal static string? TryCreateMigrationBackup(SqliteConnection connection, int fromVersion, int latestVersion)
    {
        try
        {
            string databasePath = connection.DataSource;
            if (string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath)) return null;
            if (new FileInfo(databasePath).Length == 0) return null;

            using (var checkpoint = connection.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                checkpoint.ExecuteNonQuery();
            }

            string directory = Path.Combine(Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory, Path.GetFileName(databasePath) + ".schema_backups");
            Directory.CreateDirectory(directory);

            string backupPath = Path.Combine(
                directory,
                $"{Path.GetFileName(databasePath)}.before_schema_v{fromVersion}_to_v{latestVersion}_{AppClock.Now:yyyyMMdd_HHmmss}.bak");

            File.Copy(databasePath, backupPath, overwrite: false);
            RotateMigrationBackups(directory, maxBackups: 10);
            return backupPath;
        }
        catch (Exception ex)
        {
            // El backup de migración es preventivo. Si falló por bloqueo de archivo, no detenemos la apertura del proyecto.
            AppLog.Warning("SQLiteMigración", $"No se pudo crear el backup preventivo previo a migrar '{connection.DataSource}'", ex);
            return null;
        }
    }

    internal static bool TryRestoreFromBackup(SqliteConnection targetConnection, string backupPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
                return false;

            using (var checkpoint = targetConnection.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                checkpoint.ExecuteNonQuery();
            }

            using var sourceConn = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly");
            sourceConn.Open();
            sourceConn.BackupDatabase(targetConnection);
            AppLog.Warning("SQLiteMigración", $"Base de datos '{targetConnection.DataSource}' auto-restaurada desde '{backupPath}' tras abortar migración.");
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Critical("SQLiteMigración", $"Error al auto-restaurar backup '{backupPath}' sobre '{targetConnection.DataSource}'", ex);
            return false;
        }
    }

    private static void RotateMigrationBackups(string directory, int maxBackups)
    {
        var files = Directory.GetFiles(directory, "*.bak")
            .Select(path => new System.IO.FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Skip(maxBackups)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                AppLog.Warning("SQLiteMigracion", $"No se pudo eliminar el backup antiguo '{file.FullName}'", ex);
            }
        }
    }
}
