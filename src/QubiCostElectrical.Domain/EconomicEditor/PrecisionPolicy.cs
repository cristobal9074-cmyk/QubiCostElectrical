using System;

namespace QubiCostElectrical;

public static class PrecisionPolicy
{
    public const int CantidadDecimals = 4;

    public const int PrecioUnitarioDecimals = 4;

    public const int ImporteDecimals = 2;

    public const int PorcentajeDecimals = 2;

    public const int FactorDecimals = 6;

    public const int TasaCambioDecimals = 6;

    public static decimal RoundCantidad(decimal value)
    {
        return Math.Round(value, 4, MidpointRounding.ToEven);
    }

    public static decimal RoundPrecioUnitario(decimal value)
    {
        return Math.Round(value, 4, MidpointRounding.ToEven);
    }

    public static decimal RoundImporte(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.ToEven);
    }

    public static decimal RoundPorcentaje(decimal value)
    {
        return Math.Round(value, 2, MidpointRounding.ToEven);
    }

    public static decimal RoundFactor(decimal value)
    {
        return Math.Round(value, 6, MidpointRounding.ToEven);
    }

    public static decimal RoundTasaCambio(decimal value)
    {
        return Math.Round(value, 6, MidpointRounding.ToEven);
    }

    public static decimal Apply(decimal value, int decimals)
    {
        return Math.Round(value, decimals, MidpointRounding.ToEven);
    }
}
