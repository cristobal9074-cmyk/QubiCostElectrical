using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

[Collection(WpfHostCollectionDefinition.Name)]
public sealed class FileLoggerTests
{
    public FileLoggerTests()
    {
        WpfTestHost.EnsureInitialized();
    }

    [Fact]
    public void WriteEntry_CreatesLogFile()
    {
        using var scope = CreateTempLogScope();
        var config = new LogConfiguration(scope.FolderPath);
        var logger = new FileLogger(config);

        logger.Info("TestCategory", "Test message");
        logger.Dispose();

        var folder = config.LogsFolderPath();
        Assert.True(Directory.Exists(folder));
        Assert.StartsWith(Path.GetTempPath(), folder, StringComparison.OrdinalIgnoreCase);
        var files = Directory.GetFiles(folder, "app_*.jsonl");
        Assert.NotEmpty(files);
        Assert.Contains(scope.FolderPath, files[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteEntry_ReturnsErrorId_ForErrorLevel()
    {
        using var scope = CreateTempLogScope();
        var config = new LogConfiguration(scope.FolderPath);
        var logger = new FileLogger(config);

        var errorId = logger.Error("TestCategory", "Error occurred");
        Assert.False(string.IsNullOrWhiteSpace(errorId));
        Assert.StartsWith(config.SessionId[..8], errorId);
        logger.Dispose();
    }

    [Fact]
    public void SensitiveData_IsRedacted()
    {
        using var scope = CreateTempLogScope();
        var config = new LogConfiguration(scope.FolderPath);
        var logger = new FileLogger(config);

        logger.Info("Test", "user@example.com logged in from 192.168.1.1");
        logger.Dispose();

        var files = Directory.GetFiles(config.LogsFolderPath(), "app_*.jsonl");
        var content = ReadAllTextWithRetry(files[0]);
        Assert.DoesNotContain("user@example.com", content);
        Assert.DoesNotContain("192.168.1.1", content);
    }

    [Fact]
    public void Dispose_RemovesAppLogSink()
    {
        using var scope = CreateTempLogScope();
        var config = new LogConfiguration(scope.FolderPath);
        var previousSink = AppLog.Sink;
        var logger = new FileLogger(config);

        Assert.NotNull(AppLog.Sink);
        logger.Dispose();
        Assert.Same(previousSink, AppLog.Sink);
    }

    [Fact]
    public void LogLevels_ProduceCorrectEntries()
    {
        using var scope = CreateTempLogScope();
        var config = new LogConfiguration(scope.FolderPath);
        var logger = new FileLogger(config);

        logger.Warning("WarnCat", "Warning message");
        logger.Error("ErrCat", "Error message");
        logger.Critical("CritCat", "Critical message");
        logger.Dispose();

        var files = Directory.GetFiles(config.LogsFolderPath(), "app_*.jsonl");
        var lines = ReadAllLinesWithRetry(files[0])
            .Where(l => l.Contains("WarnCat") || l.Contains("ErrCat") || l.Contains("CritCat"))
            .ToList();
        var levels = lines
            .Select(l => JsonSerializer.Deserialize<JsonElement>(l))
            .Select(e => e.GetProperty("Level").GetString())
            .ToList();
        Assert.Contains("Warning", levels);
        Assert.Contains("Error", levels);
        Assert.Contains("Critical", levels);
    }

    private static TempLogScope CreateTempLogScope()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"logger_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        return new TempLogScope(folder);
    }

    private static string ReadAllTextWithRetry(string path)
    {
        return Retry(() => File.ReadAllText(path));
    }

    private static string[] ReadAllLinesWithRetry(string path)
    {
        return Retry(() => File.ReadAllLines(path));
    }

    private static T Retry<T>(Func<T> action)
    {
        IOException? last = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                return action();
            }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(50);
            }
        }

        throw last ?? new IOException("No se pudo leer el archivo temporal.");
    }

    private sealed class TempLogScope(string folderPath) : IDisposable
    {
        public string FolderPath { get; } = folderPath;

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(FolderPath))
                    Directory.Delete(FolderPath, recursive: true);
            }
            catch
            {
            }
        }
    }
}
