using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Política de recargos centralizada. Todas las pantallas deben calcular GG, utilidad,
/// riesgo, descuento e IVA desde esta misma política para evitar diferencias de totales.
/// </summary>
public sealed class MarkupPolicy
{
    public MarkupMethod MarkupMethod { get; set; } = MarkupMethod.SobreVenta;
    public decimal GastosGeneralesPct { get; set; } = 14m;
    public decimal UtilidadPct { get; set; } = 18m;
    /// <summary>Valor agregado solo sobre equipos (paridad estándar de costos, típ. 3%).</summary>
    public decimal ValorAgregadoEquiposPct { get; set; } = 0m;
    /// <summary>
    /// Paridad estándar de costos: GG/UT sobre Mat+MO+GO (sin equipos),
    /// utilidad aditiva, VA sobre equipos, diferencia por factores.
    /// </summary>
    public bool PoliticaCostosExcel { get; set; }

    /// <summary>
    /// Decisión explícita de proyecto: la venta se arma línea a línea con el factor de
    /// presentación por familia (<see cref="CubicacionRow.GgUtFactor"/>), en vez del markup
    /// global GG/Utilidad sobre el subtotal. Solo cuando esto es <c>true</c> el motor mira
    /// <c>GgUtFactor</c> para elegir el método; un factor ≠ 1 suelto ya NO cambia el método
    /// de todo el presupuesto.
    /// </summary>
    public bool FactoresPresentacionPorFamilia { get; set; }

    public decimal RiesgoPct { get; set; } = 0m;
    public decimal DescuentoPct { get; set; } = 0m;
    public decimal IvaPct { get; set; } = 19m;
    public bool IncluirIvaEnTotal { get; set; } = true;

    public decimal SafeGastosGeneralesPct => ClampPct(GastosGeneralesPct);
    public decimal SafeUtilidadPct => ClampPct(UtilidadPct);
    public decimal SafeValorAgregadoEquiposPct => ClampPct(ValorAgregadoEquiposPct);
    public decimal SafeRiesgoPct => ClampPct(RiesgoPct);
    public decimal SafeDescuentoPct => ClampPct(DescuentoPct);
    public decimal SafeIvaPct => ClampPct(IvaPct);

    /// <summary>
    /// Clampea entre 0 y 100 exclusivamente para porcentajes aditivos internos
    /// (herramientas, mermas). No usar para markup de venta: ahí se valida con Validate().
    /// </summary>
    public static decimal ClampPct(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 100m) return 100m;
        return value;
    }

    /// <summary>
    /// Devuelve los errores de configuración sin modificar los valores ni interrumpir el cálculo.
    /// La UI usa esta lista para advertir o bloquear antes de exportar.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (GastosGeneralesPct < 0m || GastosGeneralesPct > 100m)
            errors.Add($"Gastos generales {GastosGeneralesPct:0.##}% fuera de rango [0-100].");
        if (UtilidadPct < 0m || UtilidadPct > 100m)
            errors.Add($"Utilidad {UtilidadPct:0.##}% fuera de rango [0-100].");
        if (ValorAgregadoEquiposPct < 0m || ValorAgregadoEquiposPct > 100m)
            errors.Add($"VA equipos {ValorAgregadoEquiposPct:0.##}% fuera de rango [0-100].");
        if (RiesgoPct < 0m || RiesgoPct > 100m)
            errors.Add($"Riesgo {RiesgoPct:0.##}% fuera de rango [0-100].");
        if (DescuentoPct < 0m || DescuentoPct > 100m)
            errors.Add($"Descuento {DescuentoPct:0.##}% fuera de rango [0-100].");
        if (IvaPct < 0m || IvaPct > 100m)
            errors.Add($"IVA {IvaPct:0.##}% fuera de rango [0-100].");
        return errors;
    }
}
