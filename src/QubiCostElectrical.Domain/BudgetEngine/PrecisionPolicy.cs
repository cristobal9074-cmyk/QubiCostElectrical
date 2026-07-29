namespace QubiCostElectrical;

public static class PrecisionPolicy
{
    public const int CantidadDecimals = 4;
    public const int PrecioUnitarioDecimals = 4;
    public const int ImporteDecimals = 2;
    public const int PorcentajeDecimals = 2;
    public const int FactorDecimals = 6;
    public const int TasaCambioDecimals = 6;

    public static decimal RoundCantidad(decimal value) => Math.Round(value, CantidadDecimals, MidpointRounding.ToEven);
    public static decimal RoundPrecioUnitario(decimal value) => Math.Round(value, PrecioUnitarioDecimals, MidpointRounding.ToEven);
    public static decimal RoundImporte(decimal value) => Math.Round(value, ImporteDecimals, MidpointRounding.ToEven);
    public static decimal RoundPorcentaje(decimal value) => Math.Round(value, PorcentajeDecimals, MidpointRounding.ToEven);
    public static decimal RoundFactor(decimal value) => Math.Round(value, FactorDecimals, MidpointRounding.ToEven);
    public static decimal RoundTasaCambio(decimal value) => Math.Round(value, TasaCambioDecimals, MidpointRounding.ToEven);

    public static decimal Apply(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.ToEven);
}
