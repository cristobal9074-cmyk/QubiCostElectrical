namespace QubiCostElectrical;

/// <summary>
/// Redondeo monetario único del motor (A1). El peso chileno no tiene centavos: todo cálculo
/// intermedio se mantiene a precisión plena (<see cref="decimal"/>) y solo se redondea al peso
/// en los totales que se muestran o exportan. No usar para UF ni porcentajes.
/// </summary>
public static class Money
{
    /// <summary>Redondea un monto en pesos al entero más cercano; el 0,5 se aleja de cero
    /// (positivos hacia arriba, negativos hacia abajo).</summary>
    public static decimal Round(decimal clp) => System.Math.Round(clp, 0, System.MidpointRounding.AwayFromZero);

    /// <summary>Redondea un porcentaje a 2 decimales para presentación.</summary>
    public static decimal RoundPct(decimal pct) => System.Math.Round(pct, 2, System.MidpointRounding.AwayFromZero);
}
