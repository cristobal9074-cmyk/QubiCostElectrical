namespace QubiCostElectrical.Wpf.Infrastructure.Diagnostics;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Critical
}

public interface IAppLogger : IDisposable
{
    string Info(string category, string message, IReadOnlyDictionary<string, string>? properties = null);
    string Warning(string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null);
    string Error(string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null);
    string Critical(string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null);
    string Write(LogLevel level, string category, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null);
}
