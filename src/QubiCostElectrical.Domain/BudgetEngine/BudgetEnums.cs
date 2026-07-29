namespace QubiCostElectrical;

/// <summary>
/// Método de cálculo de utilidad.
/// SobreCosto: utilidad = base × pct (markup sobre costo).
/// SobreVenta: precio_venta = base / (1 − pct/100) — estándar en contratos eléctricos chilenos.
/// </summary>
public enum MarkupMethod
{
    SobreCosto = 1,
    SobreVenta = 2
}

public enum BudgetApuMode
{
    CubicacionManda = 1,
    ApuManda = 2,
    CubicacionMasApuComplementarios = 3,
    Manual = 4
}

public static class BudgetCostOrigin
{
    public const string Cubicacion = "Cubicación";
    public const string Apu = "APU";
    public const string Plantilla = "Plantilla";
    public const string Manual = "Manual";
    public const string OrdenCambio = "Orden de cambio";
}
