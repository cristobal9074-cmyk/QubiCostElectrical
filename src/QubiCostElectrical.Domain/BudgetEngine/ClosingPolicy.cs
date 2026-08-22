using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Política comercial de cierre: cómo se convierte el CostBreakdown ya calculado (motor de costos,
/// fijo) en un precio de venta final. Se elige DESPUÉS de armar el presupuesto, sin recubicar.
/// </summary>
public enum ClosingPolicyType
{
    /// <summary>Costo + GG% + Utilidad% (la política actual, BuildExcelCostos/Legacy). Pass-through.</summary>
    CostPlusMarkup = 0,
    /// <summary>Precio de venta fijo (benchmark de mercado o techo del mandante); GG/Utilidad quedan como residual.</summary>
    TargetPrice = 1,
    /// <summary>Suma alzada / EPC: precio cerrado con contingencia mínima forzada, riesgo 100% del contratista.</summary>
    LumpSumEpc = 2,
    /// <summary>Costo reembolsable + fee fijo o porcentual, sin GG cargado sobre el costo.</summary>
    CostPlusFee = 3,
    /// <summary>Precio unitario puro: venta = suma de precios unitarios contractuales por partida.</summary>
    UnitPricePure = 4,
    /// <summary>Costo objetivo compartido (gainshare/painshare) entre mandante y contratista.</summary>
    GainsharePainshare = 5
}

/// <summary>Parámetros de la política de cierre elegida, persistidos con el proyecto.</summary>
public sealed class ClosingPolicySelection
{
    public ClosingPolicyType Policy { get; set; } = ClosingPolicyType.CostPlusMarkup;

    /// <summary>Política 1 · Precio Objetivo: venta fija (0 = usa el total del motor).</summary>
    public decimal TargetPriceClp { get; set; }

    /// <summary>Política 2 · Suma Alzada: contingencia mínima forzada (%).</summary>
    public decimal LumpSumMinRiskPct { get; set; } = 15m;

    /// <summary>Política 3 · Costo + Fee: fee fijo en CLP (tiene prioridad sobre FeePct si &gt; 0).</summary>
    public decimal FeeClp { get; set; }

    /// <summary>Política 3 · Costo + Fee: fee como % del costo de ejecución (si FeeClp = 0).</summary>
    public decimal FeePct { get; set; } = 8m;

    /// <summary>Política 5 · Gainshare: costo objetivo pactado con el mandante.</summary>
    public decimal TargetCostClp { get; set; }

    /// <summary>Política 5 · Gainshare: % del ahorro/sobrecosto que se comparte con el contratista.</summary>
    public decimal GainsharePct { get; set; } = 50m;

    /// <summary>Política 5 · Gainshare: techo del monto compartido en CLP (0 = sin techo).</summary>
    public decimal GainshareCapClp { get; set; }

    public static ClosingPolicySelection Default() => new();
}

/// <summary>Resultado de aplicar una política de cierre sobre un CostBreakdown ya calculado.</summary>
public sealed class ClosingPolicyResult
{
    public ClosingPolicyType Policy { get; set; }
    public decimal CostoEjecucionClp { get; set; }
    public decimal CostoProyectoClp { get; set; }
    public decimal VentaFinalClp { get; set; }
    public decimal MargenClp { get; set; }
    public decimal MargenPct { get; set; }
    public string Explicacion { get; set; } = "";
    public List<string> Advertencias { get; set; } = new();
}

/// <summary>
/// Aplica una política de cierre comercial sobre el resultado ya calculado por BudgetEngine.
/// No recalcula cubicación ni APU: solo transforma costo en precio según la política elegida.
/// </summary>
public static class ClosingPolicyEngine
{
    public static ClosingPolicyResult Apply(CostBreakdown breakdown, ClosingPolicySelection? selection)
    {
        breakdown ??= new CostBreakdown();
        selection ??= ClosingPolicySelection.Default();

        var result = selection.Policy switch
        {
            ClosingPolicyType.TargetPrice => ApplyTargetPrice(breakdown, selection),
            ClosingPolicyType.LumpSumEpc => ApplyLumpSumEpc(breakdown, selection),
            ClosingPolicyType.CostPlusFee => ApplyCostPlusFee(breakdown, selection),
            ClosingPolicyType.UnitPricePure => ApplyUnitPricePure(breakdown),
            ClosingPolicyType.GainsharePainshare => ApplyGainshare(breakdown, selection),
            _ => ApplyCostPlusMarkup(breakdown)
        };

        result.Policy = selection.Policy;
        result.CostoEjecucionClp = breakdown.CostoDirectoUf;
        result.CostoProyectoClp = breakdown.CostoProyectoUf > 0m ? breakdown.CostoProyectoUf : breakdown.SubtotalUf;
        result.MargenClp = result.VentaFinalClp - result.CostoEjecucionClp;
        result.MargenPct = result.VentaFinalClp > 0m ? result.MargenClp / result.VentaFinalClp * 100m : 0m;
        return result;
    }

    private static ClosingPolicyResult ApplyCostPlusMarkup(CostBreakdown breakdown) => new()
    {
        VentaFinalClp = breakdown.TotalVentaUf,
        Explicacion = "Costo + Gastos Generales + Utilidad (política vigente del motor)."
    };

    private static ClosingPolicyResult ApplyTargetPrice(CostBreakdown breakdown, ClosingPolicySelection selection)
    {
        var warnings = new List<string>();
        decimal venta = selection.TargetPriceClp > 0m ? selection.TargetPriceClp : breakdown.TotalVentaUf;
        if (selection.TargetPriceClp <= 0m)
            warnings.Add("No se definió un precio objetivo; se usa la venta calculada por el motor.");
        if (venta < breakdown.CostoDirectoUf)
            warnings.Add("El precio objetivo no cubre el costo de ejecución: margen negativo.");

        return new ClosingPolicyResult
        {
            VentaFinalClp = venta,
            Advertencias = warnings,
            Explicacion = "Precio de venta fijado primero (benchmark o techo del mandante); GG y Utilidad quedan como el margen residual disponible."
        };
    }

    private static ClosingPolicyResult ApplyLumpSumEpc(CostBreakdown breakdown, ClosingPolicySelection selection)
    {
        decimal impliedRiskPct = breakdown.CostoDirectoUf > 0m ? breakdown.RiesgoUf / breakdown.CostoDirectoUf * 100m : 0m;
        decimal minRisk = Math.Max(0m, selection.LumpSumMinRiskPct);
        decimal extraRiskPct = Math.Max(0m, minRisk - impliedRiskPct);
        decimal extraRiskClp = breakdown.CostoDirectoUf * extraRiskPct / 100m;
        decimal costoProyecto = breakdown.CostoProyectoUf > 0m ? breakdown.CostoProyectoUf : breakdown.SubtotalUf;

        var warnings = new List<string>();
        if (extraRiskPct > 0m)
            warnings.Add($"Contingencia del motor ({impliedRiskPct:0.#}%) es menor al mínimo de suma alzada ({minRisk:0.#}%); se agregó el excedente.");

        return new ClosingPolicyResult
        {
            VentaFinalClp = costoProyecto + extraRiskClp,
            Advertencias = warnings,
            Explicacion = "Precio cerrado e inamovible: el contratista absorbe todo sobrecosto. Contingencia mínima forzada por sobre el riesgo configurado en el motor."
        };
    }

    private static ClosingPolicyResult ApplyCostPlusFee(CostBreakdown breakdown, ClosingPolicySelection selection)
    {
        decimal fee = selection.FeeClp > 0m
            ? selection.FeeClp
            : breakdown.CostoDirectoUf * Math.Max(0m, selection.FeePct) / 100m;

        return new ClosingPolicyResult
        {
            VentaFinalClp = breakdown.CostoDirectoUf + fee,
            Explicacion = selection.FeeClp > 0m
                ? "Costo real traspasado 1:1 + fee fijo pactado, sin Gastos Generales cargados sobre el costo."
                : $"Costo real traspasado 1:1 + fee del {selection.FeePct:0.#}% sobre el costo de ejecución."
        };
    }

    private static ClosingPolicyResult ApplyUnitPricePure(CostBreakdown breakdown)
    {
        decimal venta = (breakdown.Origenes ?? new List<CostOriginBreakdown>()).Sum(o => o.VentaClp);
        return new ClosingPolicyResult
        {
            VentaFinalClp = venta > 0m ? venta : breakdown.TotalVentaUf,
            Explicacion = "Venta = suma de precios unitarios contractuales por partida (el margen ya está embebido en cada precio unitario)."
        };
    }

    private static ClosingPolicyResult ApplyGainshare(CostBreakdown breakdown, ClosingPolicySelection selection)
    {
        var warnings = new List<string>();
        decimal baseline = breakdown.TotalVentaUf;
        if (selection.TargetCostClp <= 0m)
        {
            warnings.Add("No se definió un costo objetivo pactado; se usa la venta calculada por el motor sin reparto.");
            return new ClosingPolicyResult
            {
                VentaFinalClp = baseline,
                Advertencias = warnings,
                Explicacion = "Costo objetivo compartido: falta pactar el costo objetivo con el mandante."
            };
        }

        decimal variance = selection.TargetCostClp - breakdown.CostoDirectoUf; // positivo = ahorro
        decimal shared = variance * Math.Clamp(selection.GainsharePct, 0m, 100m) / 100m;
        if (selection.GainshareCapClp > 0m)
            shared = Math.Clamp(shared, -selection.GainshareCapClp, selection.GainshareCapClp);

        warnings.Add("Estimado a la firma con el costo presupuestado; se recalcula con costo real al cierre de obra.");

        return new ClosingPolicyResult
        {
            VentaFinalClp = baseline + shared,
            Advertencias = warnings,
            Explicacion = variance >= 0m
                ? $"Costo estimado bajo el objetivo pactado: {selection.GainsharePct:0.#}% del ahorro se suma a la utilidad del contratista."
                : $"Costo estimado sobre el objetivo pactado: {selection.GainsharePct:0.#}% del sobrecosto se descuenta de la utilidad del contratista."
        };
    }
}
