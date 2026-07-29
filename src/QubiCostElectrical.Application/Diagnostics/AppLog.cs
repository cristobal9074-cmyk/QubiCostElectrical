namespace QubiCostElectrical;

public enum AppLogSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public static class AppLog
{
    public static Action<AppLogSeverity, string, string, Exception?, IReadOnlyDictionary<string, string>?>? Sink { get; set; }

    public static void Info(string context, string message, IReadOnlyDictionary<string, string>? properties = null)
        => Emit(AppLogSeverity.Info, context, message, null, properties);

    public static void Warning(string context, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => Emit(AppLogSeverity.Warning, context, message, ex, properties);

    public static void Error(string context, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => Emit(AppLogSeverity.Error, context, message, ex, properties);

    public static void Critical(string context, string message, Exception? ex = null, IReadOnlyDictionary<string, string>? properties = null)
        => Emit(AppLogSeverity.Critical, context, message, ex, properties);

    private static void Emit(AppLogSeverity severity, string context, string message, Exception? ex, IReadOnlyDictionary<string, string>? properties)
    {
        var sink = Sink;
        if (sink is null) return;
        try
        {
            sink(severity, context ?? "", message ?? "", ex, properties);
        }
        catch
        {
        }
    }
}
