namespace QubiCostElectrical;

/// <summary>
/// Snapshot del precio usado en un presupuesto cuando se congela la política de precios.
/// Fuente: <see cref="MatClp"/>/<see cref="MoClp"/>. <see cref="MatUf"/>/<see cref="MoUf"/>
/// quedan como UF derivados (o legacy si el snapshot es anterior a la migración CLP).
/// </summary>
public sealed class PrecioCongeladoRow
{
    public decimal MatClp { get; set; }
    public decimal MoClp { get; set; }
    public decimal MatUf { get; set; }
    public decimal MoUf { get; set; }
    public string Proveedor { get; set; } = "";
    public DateTime FechaPrecio { get; set; } = DateTime.Today;
    public string MonedaPrecio { get; set; } = Moneda.CLP;
    public decimal UfUsadaClp { get; set; }
    public decimal TipoCambioUsdClp { get; set; }
    public DateTime CongeladoEn { get; set; } = AppClock.Now;

    /// <summary>
    /// Precio material en CLP del snapshot. Prefiere MatClp; si falta, interpreta MatUf
    /// como pesos contaminados (&gt;1000), convierte UF AREAles con <see cref="UfUsadaClp"/>,
    /// o trata MatUf como CLP si el snapshot antiguo marcó moneda CLP con montos enteros.
    /// </summary>
    public decimal ResolveMatClp()
    {
        if (MatClp > 0m) return MatClp;
        if (MatUf <= 0m) return 0m;
        if (MatUf > 1000m) return MatUf;
        if (UfUsadaClp > 0m)
            return Math.Round(MatUf * UfUsadaClp, 0, MidpointRounding.AwayFromZero);
        if (string.Equals(MonedaPrecio, Moneda.CLP, StringComparison.OrdinalIgnoreCase) && MatUf >= 1m)
            return MatUf;
        return 0m;
    }

    /// <summary>Igual que <see cref="ResolveMatClp"/> para mano de obra (umbral de contaminación MoUf &gt; 15).</summary>
    public decimal ResolveMoClp()
    {
        if (MoClp > 0m) return MoClp;
        if (MoUf <= 0m) return 0m;
        if (MoUf > 15m) return MoUf;
        if (UfUsadaClp > 0m)
            return Math.Round(MoUf * UfUsadaClp, 0, MidpointRounding.AwayFromZero);
        if (string.Equals(MonedaPrecio, Moneda.CLP, StringComparison.OrdinalIgnoreCase) && MoUf >= 1m)
            return MoUf;
        return 0m;
    }
}
