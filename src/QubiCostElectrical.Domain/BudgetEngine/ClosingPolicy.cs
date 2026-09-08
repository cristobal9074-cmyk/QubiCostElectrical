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

    /// <summary>Salvaguarda: momento en que se fijó por última vez un monto absoluto de la política
    /// (precio objetivo, fee fijo, costo objetivo, tope de reparto).</summary>
    public DateTime? ParamsStampedAt { get; set; }

    /// <summary>Salvaguarda: costo de ejecución vigente cuando se fijó ese monto absoluto. Sirve para
    /// avisar si el costo del proyecto se movió y el monto quedó obsoleto.</summary>
    public decimal ParamsStampedCostoEjecucionClp { get; set; }

    public static ClosingPolicySelection Default() => new();
}

/// <summary>Resultado de aplicar una política de cierre. <see cref="VentaFinalClp"/> es el NETO
/// (venta post-política, <b>pre-descuento y pre-IVA</b>); <c>BudgetTotalsCalculator</c> aplica
/// descuento e IVA sobre ese neto de forma uniforme para todas las políticas.</summary>
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
/// Entrada mínima para <see cref="ClosingPolicyEngine.ApplyNeto"/>. Se arma en
/// <c>BudgetTotalsCalculator</c> ANTES de construir el <see cref="CostBreakdown"/> final:
/// por eso no se pasa el breakdown entero.
/// </summary>
public sealed class ClosingPolicyInput
{
    /// <summary>Venta por factores/markup del motor, post-factores, <b>pre-descuento y pre-IVA</b>.</summary>
    public decimal VentaBaseNetaClp { get; set; }
    /// <summary>Costo de ejecución (costo directo del motor, incl. GO en política Excel).</summary>
    public decimal CostoEjecucionClp { get; set; }
    /// <summary>Piso comercial: costo del proyecto con GG + utilidad + VA (bajo este piso ⇒ bloqueante).</summary>
    public decimal PisoComercialClp { get; set; }
    /// <summary>Contingencia/riesgo ya incluida en el costo de ejecución, como % de ese costo.</summary>
    public decimal RiesgoImplicitoPct { get; set; }
    /// <summary>Orígenes con su venta de línea (para <see cref="ClosingPolicyType.UnitPricePure"/>).</summary>
    public IReadOnlyList<CostOriginBreakdown> Origenes { get; set; } = new List<CostOriginBreakdown>();

    /// <summary>Reconstruye la entrada desde un breakdown ya calculado (fallback de UI/tests).</summary>
    public static ClosingPolicyInput FromBreakdown(CostBreakdown breakdown)
    {
        breakdown ??= new CostBreakdown();
        decimal costo = breakdown.CostoDirectoUf;
        return new ClosingPolicyInput
        {
            VentaBaseNetaClp = breakdown.TotalVentaNetoUf > 0m ? breakdown.TotalVentaNetoUf : breakdown.TotalVentaUf,
            CostoEjecucionClp = costo,
            PisoComercialClp = breakdown.CostoProyectoUf > 0m ? breakdown.CostoProyectoUf : breakdown.SubtotalUf,
            RiesgoImplicitoPct = costo > 0m ? breakdown.RiesgoUf / costo * 100m : 0m,
            Origenes = breakdown.Origenes ?? new List<CostOriginBreakdown>()
        };
    }
}

/// <summary>
/// Convierte la venta base del motor en el NETO de venta según la política comercial de cierre.
/// No recalcula cubicación ni APU. El descuento y el IVA se aplican después, fuera de aquí.
/// </summary>
public static class ClosingPolicyEngine
{
    public static ClosingPolicyResult ApplyNeto(ClosingPolicyInput input, ClosingPolicySelection? selection)
    {
        input ??= new ClosingPolicyInput();
        selection ??= ClosingPolicySelection.Default();

        var result = selection.Policy switch
        {
            ClosingPolicyType.TargetPrice => ApplyTargetPrice(input, selection),
            ClosingPolicyType.LumpSumEpc => ApplyLumpSumEpc(input, selection),
            ClosingPolicyType.CostPlusFee => ApplyCostPlusFee(input, selection),
            ClosingPolicyType.UnitPricePure => ApplyUnitPricePure(input),
            ClosingPolicyType.GainsharePainshare => ApplyGainshare(input, selection),
            _ => ApplyCostPlusMarkup(input)
        };

        result.Policy = selection.Policy;
        result.CostoEjecucionClp = input.CostoEjecucionClp;
        result.CostoProyectoClp = input.PisoComercialClp;
        result.MargenClp = result.VentaFinalClp - input.CostoEjecucionClp;
        result.MargenPct = result.VentaFinalClp > 0m ? result.MargenClp / result.VentaFinalClp * 100m : 0m;

        // Aviso NO bloqueante para políticas de precio-primero: la venta cubre el costo de
        // ejecución pero queda bajo el piso comercial (costo del proyecto con GG + utilidad).
        // FinalReviewService lo trata como advertencia (no contiene "margen negativo"/"no cubre").
        if (selection.Policy is ClosingPolicyType.TargetPrice or ClosingPolicyType.LumpSumEpc
                or ClosingPolicyType.GainsharePainshare)
        {
            decimal v = result.VentaFinalClp;
            if (input.PisoComercialClp > 0m && v >= input.CostoEjecucionClp && v < input.PisoComercialClp)
                result.Advertencias.Add(
                    $"La venta con esta política ({v:N0}) está bajo el costo del proyecto con utilidad ({input.PisoComercialClp:N0}); margen reducido.");
        }

        return result;
    }

    /// <summary>Compat.: aplica la política sobre un breakdown ya calculado (fallback de UI/tests).</summary>
    public static ClosingPolicyResult Apply(CostBreakdown breakdown, ClosingPolicySelection? selection)
        => ApplyNeto(ClosingPolicyInput.FromBreakdown(breakdown), selection);

    private static ClosingPolicyResult ApplyCostPlusMarkup(ClosingPolicyInput input) => new()
    {
        VentaFinalClp = input.VentaBaseNetaClp,
        Explicacion = "Costo + Gastos Generales + Utilidad (política vigente del motor)."
    };

    private static ClosingPolicyResult ApplyTargetPrice(ClosingPolicyInput input, ClosingPolicySelection selection)
    {
        var warnings = new List<string>();
        decimal venta = selection.TargetPriceClp > 0m ? selection.TargetPriceClp : input.VentaBaseNetaClp;
        if (selection.TargetPriceClp <= 0m)
            warnings.Add("No se definió un precio objetivo; se usa la venta calculada por el motor.");
        if (venta < input.CostoEjecucionClp)
            warnings.Add("El precio objetivo no cubre el costo de ejecución: margen negativo.");

        return new ClosingPolicyResult
        {
            VentaFinalClp = venta,
            Advertencias = warnings,
            Explicacion = "Precio de venta fijado primero (benchmark o techo del mandante); GG y Utilidad quedan como el margen residual disponible."
        };
    }

    private static ClosingPolicyResult ApplyLumpSumEpc(ClosingPolicyInput input, ClosingPolicySelection selection)
    {
        decimal impliedRiskPct = input.RiesgoImplicitoPct;
        decimal minRisk = Math.Max(0m, selection.LumpSumMinRiskPct);
        decimal extraRiskPct = Math.Max(0m, minRisk - impliedRiskPct);
        decimal extraRiskClp = input.CostoEjecucionClp * extraRiskPct / 100m;

        var warnings = new List<string>();
        if (extraRiskPct > 0m)
            warnings.Add($"Contingencia del motor ({impliedRiskPct:0.#}%) es menor al mínimo de suma alzada ({minRisk:0.#}%); se agregó el excedente.");

        return new ClosingPolicyResult
        {
            VentaFinalClp = input.PisoComercialClp + extraRiskClp,
            Advertencias = warnings,
            Explicacion = "Precio cerrado e inamovible: el contratista absorbe todo sobrecosto. Contingencia mínima forzada por sobre el riesgo configurado en el motor."
        };
    }

    private static ClosingPolicyResult ApplyCostPlusFee(ClosingPolicyInput input, ClosingPolicySelection selection)
    {
        decimal fee = selection.FeeClp > 0m
            ? selection.FeeClp
            : input.CostoEjecucionClp * Math.Max(0m, selection.FeePct) / 100m;

        return new ClosingPolicyResult
        {
            VentaFinalClp = input.CostoEjecucionClp + fee,
            Explicacion = selection.FeeClp > 0m
                ? "Costo real traspasado 1:1 + fee fijo pactado, sin Gastos Generales cargados sobre el costo."
                : $"Costo real traspasado 1:1 + fee del {selection.FeePct:0.#}% sobre el costo de ejecución."
        };
    }

    private static ClosingPolicyResult ApplyUnitPricePure(ClosingPolicyInput input)
    {
        // Algunos orígenes no traen precio unitario propio (cargos globales de herramientas/mermas,
        // órdenes de cambio aprobadas guardan solo el delta de costo): sin este fallback quedarían
        // en 0 y la venta se subcontaría silenciosamente. Se suman a costo (sin margen) y se avisa.
        decimal venta = 0m;
        bool hadCostOnlyOrigins = false;
        foreach (var o in input.Origenes ?? new List<CostOriginBreakdown>())
        {
            if (o.VentaClp > 0m)
            {
                venta += o.VentaClp;
            }
            else if (o.TotalUf > 0m)
            {
                venta += o.TotalUf;
                hadCostOnlyOrigins = true;
            }
        }

        var warnings = new List<string>();
        if (hadCostOnlyOrigins)
            warnings.Add("Algunas líneas (cargos globales de herramientas/mermas, órdenes de cambio aprobadas) no tienen precio unitario propio; se sumaron a costo, sin margen adicional.");

        return new ClosingPolicyResult
        {
            VentaFinalClp = venta > 0m ? venta : input.VentaBaseNetaClp,
            Advertencias = warnings,
            Explicacion = "Venta = suma de precios unitarios contractuales por partida (el margen ya está embebido en cada precio unitario)."
        };
    }

    private static ClosingPolicyResult ApplyGainshare(ClosingPolicyInput input, ClosingPolicySelection selection)
    {
        var warnings = new List<string>();
        decimal baseline = input.VentaBaseNetaClp;
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

        decimal variance = selection.TargetCostClp - input.CostoEjecucionClp; // positivo = ahorro
        decimal shared = variance * MarkupPolicy.ClampPct(selection.GainsharePct) / 100m;
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
