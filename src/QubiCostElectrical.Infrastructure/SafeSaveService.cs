using System.Diagnostics;
using System.Text.Json;

namespace QubiCostElectrical;

public sealed record SafeSaveResult
{
    public bool IsSuccess { get; init; }
    public string? BackupPath { get; init; }
    public string? ErrorMessage { get; init; }
    public string? TempPath { get; init; }
}

public static class SafeSaveService
{
    public const int MaxBackupSlots = 10;

    public static async Task<SafeSaveResult> SaveJsonFileAsync(
        string path, ProjectFileData data, JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        var result = new SafeSaveResult();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        string tmp;
        do
        {
            tmp = Path.Combine(dir ?? ".", $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        }
        while (File.Exists(tmp));

        try
        {
            await using (var stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream))
            {
                var json = JsonSerializer.Serialize(data, options);
                await writer.WriteAsync(json.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            var content = await File.ReadAllTextAsync(tmp, cancellationToken);
            JsonSerializer.Deserialize<ProjectFileData>(content, options);
        }
        catch (Exception ex)
        {
            SafeCleanupTemp(tmp);
            return result with
            {
                IsSuccess = false,
                ErrorMessage = $"Error al serializar o validar: {ex.Message}",
                TempPath = File.Exists(tmp) ? tmp : null
            };
        }

        try
        {
            if (File.Exists(path))
            {
                var backup = await CreateBackupAsync(path, cancellationToken);

                File.Replace(tmp, path, backup ?? null, ignoreMetadataErrors: true);

                return result with
                {
                    IsSuccess = true,
                    BackupPath = backup
                };
            }
            else
            {
                File.Move(tmp, path);

                return result with
                {
                    IsSuccess = true
                };
            }
        }
        catch (Exception ex)
        {
            SafeCleanupTemp(tmp);
            return result with
            {
                IsSuccess = false,
                ErrorMessage = $"Error al reemplazar archivo: {ex.Message}",
                BackupPath = null
            };
        }
    }

    public static async Task<string?> CreateBackupAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var backupDir = Path.Combine(dir ?? ".", ".backups");
            Directory.CreateDirectory(backupDir);

            RotateOldBackups(backupDir, name);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"{name}_{timestamp}.bak");

            await Task.Run(() => File.Copy(path, backupPath, overwrite: false), cancellationToken);

            return backupPath;
        }
        catch (Exception ex)
        {
            AppLog.Warning("SafeSave", $"No se pudo crear backup de '{path}'", ex);
            return null;
        }
    }

    public static bool TryCreateBackup(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var backupDir = Path.Combine(dir ?? ".", ".backups");
            Directory.CreateDirectory(backupDir);

            RotateOldBackups(backupDir, name);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(backupDir, $"{name}_{timestamp}.bak");

            File.Copy(path, backupPath, overwrite: false);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warning("SafeSave", $"No se pudo crear backup de '{path}'", ex);
            return false;
        }
    }

    private static void RotateOldBackups(string backupDir, string projectName)
    {
        try
        {
            if (!Directory.Exists(backupDir)) return;

            var backups = Directory.GetFiles(backupDir, $"{projectName}_*.bak")
                .OrderByDescending(f => f)
                .ToList();

            while (backups.Count >= MaxBackupSlots)
            {
                var oldest = backups[^1];
                try { File.Delete(oldest); }
                catch (Exception ex) { Debug.WriteLine($"[SafeSave] No se pudo eliminar backup antiguo {oldest}: {ex.Message}"); }
                backups.RemoveAt(backups.Count - 1);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SafeSave] Error en rotación de backups: {ex.Message}");
        }
    }

    public static void SafeCleanupTemp(string? tmp)
    {
        if (tmp != null && File.Exists(tmp))
        {
            try { File.Delete(tmp); }
            catch { Debug.WriteLine($"[SafeSave] No se pudo limpiar temporal {tmp}"); }
        }
    }
}
