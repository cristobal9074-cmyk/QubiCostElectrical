namespace QubiCostElectrical;

/// <summary>
/// Conversión CLP → UF para el modelo de costos: los precios SIEMPRE se ingresan en pesos
/// chilenos (MatClp/MoClp); la UF (MatUf/MoUf) es un valor derivado que el resto del motor
/// (APU, presupuesto, resumen, itemizado) sigue consumiendo sin cambios. Se recalcula cada vez
/// que cambia el valor de la UF del proyecto (pestaña Moneda).
/// </summary>
public static class PriceConversion
{
    /// <summary>Umbral de vigencia del precio: 6 meses (180 días) desde FechaPrecio.</summary>
    public const int VigenteDiasUmbral = 180;

    public const decimal DefaultValorUfClp = 39000m;

    public static decimal ClpToUf(decimal clp, decimal valorUfClp)
    {
        decimal rate = valorUfClp > 0m ? valorUfClp : DefaultValorUfClp;
        return Math.Round(clp / rate, 4, MidpointRounding.AwayFromZero);
    }

    public static decimal UfToClp(decimal uf, decimal valorUfClp) =>
        valorUfClp > 0m ? Math.Round(uf * valorUfClp, 0, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>
    /// Interpreta un valor legacy en columna MatUf/MoUf hacia CLP sin el error
    /// «MO=1 peso → 1×ValorUF». Reglas:
    /// <list type="bullet">
    /// <item>Umbral alto (MAT&gt;1000 / MO&gt;15): ya es CLP contaminado en la columna UF.</item>
    /// <item>Fracción &lt; 1: UF real → convertir con <paramref name="valorUfClp"/>.</item>
    /// <item>MO entero 1..15: CLP literal (p. ej. MO=1 en el banco), nunca × ValorUF.</item>
    /// <item>MAT entero: CLP dual-escrito; MAT con decimales (1,2): UF real → convertir.</item>
    /// </list>
    /// </summary>
    public static decimal LegacyUfFieldToClp(decimal ufField, decimal valorUfClp, bool isMo)
    {
        if (ufField <= 0m) return 0m;

        decimal clpThreshold = isMo ? 15m : 1000m;
        if (ufField > clpThreshold)
            return ufField;

        if (ufField < 1m)
            return valorUfClp > 0m
                ? Math.Round(ufField * valorUfClp, 0, MidpointRounding.AwayFromZero)
                : 0m;

        if (isMo)
            return ufField; // 1..15 → pesos, no UF

        // MAT: enteros (273) = CLP; con decimales (1.2) = UF
        if (ufField == decimal.Truncate(ufField))
            return ufField;

        return valorUfClp > 0m
            ? Math.Round(ufField * valorUfClp, 0, MidpointRounding.AwayFromZero)
            : 0m;
    }
}
