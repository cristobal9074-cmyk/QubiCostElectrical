using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class SafeSaveServiceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    [Fact]
    public async Task SaveJsonFileAsync_CreatesFile_WhenNew()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.Delete(path);
            var data = new ProjectFileData { Proyecto = { Nombre = "Test" } };
            var result = await SafeSaveService.SaveJsonFileAsync(path, data, JsonOpts);
            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(path));
            Assert.Null(result.BackupPath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveJsonFileAsync_AtomicReplacement_CreatesBackup()
    {
        var path = Path.GetTempFileName();
        try
        {
            var original = new ProjectFileData { Proyecto = { Nombre = "Original" } };
            File.WriteAllText(path, JsonSerializer.Serialize(original, JsonOpts));
            var updated = new ProjectFileData { Proyecto = { Nombre = "Updated" } };
            var result = await SafeSaveService.SaveJsonFileAsync(path, updated, JsonOpts);
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.BackupPath);
            Assert.True(File.Exists(result.BackupPath));
            var saved = JsonSerializer.Deserialize<ProjectFileData>(File.ReadAllText(path), JsonOpts);
            Assert.NotNull(saved);
            Assert.Equal("Updated", saved!.Proyecto.Nombre);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            CleanupBackups(path);
        }
    }

    [Fact]
    public async Task SaveJsonFileAsync_DoesNotOverwrite_WhenReplaceFails()
    {
        var path = Path.GetTempFileName();
        try
        {
            var original = new ProjectFileData { Proyecto = { Nombre = "Original" } };
            File.WriteAllText(path, JsonSerializer.Serialize(original, JsonOpts));
            File.SetAttributes(path, FileAttributes.ReadOnly);
            var updated = new ProjectFileData { Proyecto = { Nombre = "Updated" } };
            var result = await SafeSaveService.SaveJsonFileAsync(path, updated, JsonOpts);
            Assert.False(result.IsSuccess);
            File.SetAttributes(path, FileAttributes.Normal);
            var saved = JsonSerializer.Deserialize<ProjectFileData>(File.ReadAllText(path), JsonOpts);
            Assert.NotNull(saved);
            Assert.Equal("Original", saved!.Proyecto.Nombre);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CreateBackup_CopiesFileAndRotates()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "content");
            var first = SafeSaveService.TryCreateBackup(path);
            Assert.True(first);
            System.Threading.Thread.Sleep(1100);
            var second = SafeSaveService.TryCreateBackup(path);
            Assert.True(second);
            var backupDir = Path.Combine(Path.GetDirectoryName(path)!, ".backups");
            Assert.True(Directory.Exists(backupDir));
            var backups = Directory.GetFiles(backupDir, $"{Path.GetFileNameWithoutExtension(path)}_*.bak");
            Assert.True(backups.Length >= 2);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            CleanupBackups(path);
        }
    }

    [Fact]
    public void CreateBackup_FailsCleanly_WhenSourceMissing()
    {
        var result = SafeSaveService.TryCreateBackup(@"C:\__nonexistent__\proyecto.pepro");
        Assert.False(result);
    }

    private static void CleanupBackups(string path)
    {
        var backupDir = Path.Combine(Path.GetDirectoryName(path) ?? ".", ".backups");
        if (Directory.Exists(backupDir))
            try { Directory.Delete(backupDir, recursive: true); } catch { }
    }
}
