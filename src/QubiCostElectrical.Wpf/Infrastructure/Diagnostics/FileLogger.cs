using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

public sealed partial class FileLogger : IAppLogger
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly LogConfiguration config;
    private readonly string errorIdPrefix;
    private readonly Action<AppLogSeverity, string, string, Exception?, IReadOnlyDictionary<string, string>?>? previousSink;
    private readonly object sync = new();
    private StreamWriter? currentWriter;
    private string? currentDate;
    private bool disposed;

    private static readonly string[] SensitivePatterns =
    [
        @"(?<=^.{0,100})[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}(?=.*$)",
        @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
        @"\\\\[^\\]+\\[^\\]+\\[^\\]+",
    ];

    private static readonly Regex[] SensitiveRegexes = SensitivePatterns
        .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200)))
        .ToArray();

    public FileLogger(LogConfiguration? config = null)
    {
        this.config = config ?? new LogConfiguration();
        errorIdPrefix = this.config.SessionId[..8];
        previousSink = AppLog.Sink;
        AppLog.Sink = OnAppLog;
    }

    public string Info(string category, string message, IReadOnlyDictionary<string, string>? properties = null)
        => WriteEntry(LogLevel.Info, category, message, null, properties);

    public string Warning(string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => WriteEntry(LogLevel.Warning, category, message, ex, properties);

    public string Error(string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => WriteEntry(LogLevel.Error, category, message, ex, properties);

    public string Critical(string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => WriteEntry(LogLevel.Critical, category, message, ex, properties);

    public string Write(LogLevel level, string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => WriteEntry(level, category, message, ex, properties);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (sync)
        {
            if (AppLog.Sink == OnAppLog)
                AppLog.Sink = previousSink;
            currentWriter?.Flush();
            currentWriter?.Dispose();
            currentWriter = null;
        }
    }

    private string WriteEntry(LogLevel level, string category, string message, Exception? ex, IReadOnlyDictionary<string, string>? properties)
    {
        if (disposed) return "";
        var errorId = $"{errorIdPrefix}-{GetNextSequence()}";
        try
        {
            var entry = BuildEntry(level, category, message, ex, errorId, properties);
            var line = JsonSerializer.Serialize(entry, JsonOpts);
            WriteLine(line);
        }
        catch (Exception writeEx)
        {
            // Si el log en sí no se pudo escribir, el errorId mostrado al usuario queda sin
            // respaldo en disco. Dejar esto visible en Debug es lo único que podemos hacer
            // sin arriesgar un loop de fallos (no podemos loguear el fallo del logger).
            Debug.WriteLine($"[FileLogger] No se pudo escribir entrada de log (errorId={errorId}): {writeEx.Message}");
        }
        return errorId;
    }

    private LogEntry BuildEntry(LogLevel level, string category, string message, Exception? ex, string errorId, IReadOnlyDictionary<string, string>? properties)
    {
        var redactedMessage = Redact(message);
        var redactedCategory = Redact(category);

        return new LogEntry
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            Level = level.ToString(),
            Category = redactedCategory,
            Message = redactedMessage,
            ErrorId = level >= LogLevel.Error ? errorId : null,
            SessionId = config.SessionId,
            AppVersion = config.AppVersion,
            MachineName = config.MachineName,
            OsDescription = config.OsDescription,
            ProcessArchitecture = config.ProcessArchitecture,
            ExceptionType = ex?.GetType().FullName,
            ExceptionMessage = ex != null ? Redact(ex.Message) : null,
            StackTrace = ex?.ToString() != null ? Redact(ex.ToString()!) : null,
            Properties = properties
        };
    }

    private void WriteLine(string line)
    {
        lock (sync)
        {
            if (disposed) return;
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            if (currentDate != today)
            {
                currentWriter?.Flush();
                currentWriter?.Dispose();
                currentWriter = null;
                currentDate = today;
                RotateOldFiles();
            }

            if (currentWriter == null)
            {
                var path = config.GetLogFilePath(DateTime.UtcNow);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                // FileShare.ReadWrite: permite que otra instancia de la app (o un proceso de
                // test) escriba al mismo archivo sin que ambos se bloqueen mutuamente con
                // IOException. Sin esto, dos instancias corriendo a la vez pueden hacer que
                // el logueo de errores falle silenciosamente justo cuando más se necesita.
                var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                currentWriter = new StreamWriter(stream, Encoding.UTF8);
            }

            if (currentWriter.BaseStream.Length >= LogConfiguration.MaxFileSizeBytes)
            {
                currentWriter.Flush();
                currentWriter.Dispose();
                currentWriter = null;
                var path = config.GetLogFilePath(DateTime.UtcNow);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
                // FileShare.ReadWrite: permite que otra instancia de la app (o un proceso de
                // test) escriba al mismo archivo sin que ambos se bloqueen mutuamente con
                // IOException. Sin esto, dos instancias corriendo a la vez pueden hacer que
                // el logueo de errores falle silenciosamente justo cuando más se necesita.
                var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                currentWriter = new StreamWriter(stream, Encoding.UTF8);
            }

            currentWriter.WriteLine(line);
            currentWriter.Flush();
        }
    }

    private void RotateOldFiles()
    {
        try
        {
            var folder = config.LogsFolderPath();
            if (!Directory.Exists(folder)) return;
            var cutoff = DateTime.UtcNow.AddDays(-LogConfiguration.RetentionDays);
            foreach (var file in Directory.GetFiles(folder, "app_*.jsonl"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    try { File.Delete(file); } catch { Debug.WriteLine($"[FileLogger] No se pudo eliminar {file}"); }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileLogger] No se pudo rotar archivos antiguos: {ex.Message}");
        }
    }

    private static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var regex in SensitiveRegexes)
        {
            try { text = regex.Replace(text, "***"); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileLogger] Redaccion fallida para patron '{regex}': {ex.Message}");
            }
        }
        return text;
    }

    private int sequence;
    private int GetNextSequence() => Interlocked.Increment(ref sequence);

    private void OnAppLog(AppLogSeverity severity, string context, string message, Exception? ex, IReadOnlyDictionary<string, string>? properties)
    {
        var level = severity switch
        {
            AppLogSeverity.Info => LogLevel.Info,
            AppLogSeverity.Warning => LogLevel.Warning,
            AppLogSeverity.Error => LogLevel.Error,
            AppLogSeverity.Critical => LogLevel.Critical,
            _ => LogLevel.Info
        };
        WriteEntry(level, context, message, ex, properties);
    }

    private sealed class LogEntry
    {
        public string? Timestamp { get; set; }
        public string? Level { get; set; }
        public string? Category { get; set; }
        public string? Message { get; set; }
        public string? ErrorId { get; set; }
        public string? SessionId { get; set; }
        public string? AppVersion { get; set; }
        public string? MachineName { get; set; }
        public string? OsDescription { get; set; }
        public string? ProcessArchitecture { get; set; }
        public string? ExceptionType { get; set; }
        public string? ExceptionMessage { get; set; }
        public string? StackTrace { get; set; }
        public IReadOnlyDictionary<string, string>? Properties { get; set; }
    }
}
