namespace QubiCostElectrical;

/// <summary>
/// Configuración global de estados de pago — aplica al cronograma de la obra.
/// Permite definir año de inicio, mes de inicio (1–12) y duración en meses.
/// </summary>
public sealed class EstadoPagoConfig
{
    public int Anio { get; set; } = AppClock.Now.Year;
    public int MesInicio { get; set; } = 1;
    public int CantMeses { get; set; } = 12;
    public decimal AnticipoPct { get; set; } = 10m;
    public decimal RetencionPct { get; set; } = 5m;
    public decimal IvaPct { get; set; } = 19m;

    public static EstadoPagoConfig Default() => new();
}
