namespace QubiCostElectrical;

/// <summary>
/// Fuente centralizada de tiempo del sistema.
/// En producción devuelve DateTime.Now. En pruebas puede reemplazarse con SetProvider.
/// </summary>
public static class AppClock
{
    private static Func<DateTime>? _provider;

    public static DateTime Now => _provider != null ? _provider() : DateTime.Now;

    /// <summary>Solo para pruebas unitarias. Nunca llamar en código de producción.</summary>
    public static void SetProvider(Func<DateTime>? provider) => _provider = provider;

    public static void Reset() => _provider = null;
}
