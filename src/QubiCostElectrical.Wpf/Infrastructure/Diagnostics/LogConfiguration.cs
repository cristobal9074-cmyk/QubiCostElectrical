using System.IO;
using System.Runtime.InteropServices;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

public sealed class LogConfiguration
{
    public const string AppName = "QubiCostElectrical";
    public const int MaxFileSizeBytes = 10 * 1024 * 1024;
    public const int RetentionDays = 30;
    public const int MaxOldFiles = 10;

    private readonly string logsFolderPath;

    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public string AppVersion { get; }
    public string MachineName { get; }
    public string OsDescription { get; }
    public string ProcessArchitecture { get; }

    public LogConfiguration(string? logsFolderPath = null)
    {
        AppVersion = ProductVersionInfo.ProductVersionLabel;
        MachineName = Environment.MachineName;
        OsDescription = RuntimeInformation.OSDescription;
        ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
        this.logsFolderPath = string.IsNullOrWhiteSpace(logsFolderPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName, "Logs")
            : logsFolderPath;
    }

    public string LogsFolderPath()
    {
        return logsFolderPath;
    }

    public string GetLogFilePath(DateTime date)
    {
        Directory.CreateDirectory(logsFolderPath);
        return Path.Combine(logsFolderPath, $"app_{date:yyyyMMdd}.jsonl");
    }
}
