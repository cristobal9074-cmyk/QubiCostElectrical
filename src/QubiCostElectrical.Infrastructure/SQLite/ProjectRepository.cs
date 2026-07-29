using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class ProjectRepository : IProjectRepository
{
    public void SaveProjectFile(string path, ProjectFileData data, JsonSerializerOptions options)
    {
        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public ProjectFileData LoadProjectFile(string path, JsonSerializerOptions options)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        var data = JsonSerializer.Deserialize<ProjectFileData>(json, options);
        return data ?? throw new InvalidOperationException("El archivo no contiene un proyecto válido.");
    }

    public async Task SaveProjectFileAsync(string path, ProjectFileData data, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        var result = await SafeSaveService.SaveJsonFileAsync(path, data, options, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.ErrorMessage ?? "Error al guardar el archivo del proyecto.");
    }

    public async Task<ProjectFileData> LoadProjectFileAsync(string path, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        var data = JsonSerializer.Deserialize<ProjectFileData>(json, options);
        return data ?? throw new InvalidOperationException("El archivo no contiene un proyecto válido.");
    }

    public string GetSQLiteDatabasePathForProject(string projectPath, string fallbackDirectory, string fallbackProjectName)
    {
        string dir = Path.GetDirectoryName(projectPath) ?? fallbackDirectory;
        string name = Path.GetFileNameWithoutExtension(projectPath);
        if (string.IsNullOrWhiteSpace(name)) name = fallbackProjectName;
        string ext = Path.GetExtension(projectPath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ProjectFileTypes.CurrentExtension;
        return Path.Combine(dir, name + ext + ProjectFileTypes.SqliteSuffix);
    }

    public void SaveSQLiteSnapshot(string databasePath, ProjectFileData data)
    {
        SQLiteProjectStore.SaveSnapshot(databasePath, data);
    }

    public void SaveSQLiteIncremental(string databasePath, ProjectFileData data, string[] scopes)
    {
        SQLiteProjectStore.SaveIncremental(databasePath, data, scopes);
    }

    public Task SaveSQLiteSnapshotAsync(string databasePath, ProjectFileData data, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveSQLiteSnapshot(databasePath, data);
        }, cancellationToken);
    }

    public Task SaveSQLiteIncrementalAsync(string databasePath, ProjectFileData data, string[] scopes, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveSQLiteIncremental(databasePath, data, scopes);
        }, cancellationToken);
    }

    public ProjectFileData LoadSQLiteSnapshot(string databasePath)
    {
        return SQLiteProjectStore.LoadSnapshot(databasePath);
    }

    public Task<ProjectFileData> LoadSQLiteSnapshotAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return LoadSQLiteSnapshot(databasePath);
        }, cancellationToken);
    }

    public DateTime GetSQLiteLastSavedAt(string databasePath)
    {
        return SQLiteProjectStore.GetLastSavedAt(databasePath);
    }

    public string? PeekSQLiteEstadoPresupuesto(string databasePath)
    {
        return SQLiteProjectStore.PeekEstadoPresupuesto(databasePath);
    }

    public string CreateNumberedSQLiteBackup(string databasePath, int slots, string reason)
    {
        return SQLiteProjectBackup.CreateNumberedBackup(databasePath, slots, reason);
    }

    public Task<string> CreateNumberedSQLiteBackupAsync(string databasePath, int slots, string reason, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return CreateNumberedSQLiteBackup(databasePath, slots, reason);
        }, cancellationToken);
    }

    public string GetLatestUsableSQLiteBackup(string databasePath)
    {
        return SQLiteProjectBackup.GetLatestUsableBackup(databasePath);
    }

    public bool RestoreSQLiteBackup(string backupPath, string databasePath)
    {
        return SQLiteProjectBackup.RestoreBackup(backupPath, databasePath);
    }

    public void ClearSQLitePools()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }
}
