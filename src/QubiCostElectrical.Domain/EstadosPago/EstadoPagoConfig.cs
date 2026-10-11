namespace QubiCostElectrical;

/// <summary>
/// Configuración global de estados de pago — aplica a todos los meses del año
/// (ver docs/propuesta-estados-pago.html, tarjeta "Configuración del estado").
/// </summary>
public sealed class EstadoPagoConfig
{
    public int Anio { get; set; } = AppClock.Now.Year;
    public int CantMeses { get; set; } = 12;
    public decimal AnticipoPct { get; set; } = 10m;
    public decimal RetencionPct { get; set; } = 5m;
    public decimal IvaPct { get; set; } = 19m;

    public static EstadoPagoConfig Default() => new();
}
