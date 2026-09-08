using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Hist.: separa la etapa de totales/markup del recorrido de cubicación.
/// BudgetEngine queda como orquestador de orígenes y esta clase calcula venta, IVA, margen y UF/m².
/// </summary>
internal static class BudgetTotalsCalculator
{
    public static CostBreakdown Build(DirectCostResult direct, BudgetSettings settings)
    {
        var policy = settings.Markup ?? new MarkupPolicy();
        var markupErrors = policy.Validate();

        if (policy.PoliticaCostosExcel)
            return BuildExcelCostos(direct, settings, policy, markupErrors);

        return BuildLegacy(direct, settings, policy, markupErrors);
    }

    private static CostBreakdown BuildLegacy(
        DirectCostResult direct,
        BudgetSettings settings,
        MarkupPolicy policy,
        IReadOnlyList<string> markupErrors)
    {
        decimal riesgo = direct.TotalUf * policy.SafeRiesgoPct / 100m;
        decimal baseCosto = direct.TotalUf + riesgo;
        decimal gg = baseCosto * policy.SafeGastosGeneralesPct / 100m;
        decimal vaEquipos = direct.EquiposUf * policy.SafeValorAgregadoEquiposPct / 100m;
        decimal subtotal = baseCosto + gg + vaEquipos;
        decimal bruto;
        decimal utilidad;
        decimal diferenciaFactores = 0m;
        decimal costoProyecto;
        var extraInconsistencias = new List<BudgetInconsistencia>();
        decimal pisoComercial;
        if (direct.HasRowLevelMarkups)
        {
            // C-1: venta = Σ (costo_origen × factor_presentación_familia). El piso comercial es
            // subtotal + utilidad mínima (GG ya está en subtotal). Si la venta por factores cae
            // bajo ese piso, es una inconsistencia BLOQUEANTE (antes el signo se escondía con
            // Math.Max(0, utilidad) en CostoProyectoUf).
            bruto = direct.SumVentaClp;
            decimal utilidadFloor = (policy.MarkupMethod == MarkupMethod.SobreVenta && policy.SafeUtilidadPct < 100m)
                ? subtotal / (1m - policy.SafeUtilidadPct / 100m) - subtotal
                : subtotal * policy.SafeUtilidadPct / 100m;
            decimal pisoConUtilidad = subtotal + utilidadFloor;
            utilidad = bruto - subtotal;
            costoProyecto = pisoConUtilidad;
            pisoComercial = pisoConUtilidad;
            // C-1: solo para la ruta por factores/markup (CostPlusMarkup o sin política). El resto
            // de políticas evalúan el piso en FinalizeVentaYCierre sobre su propio neto.
            var polTipo = settings.ClosingPolicy?.Policy ?? ClosingPolicyType.CostPlusMarkup;
            if (polTipo == ClosingPolicyType.CostPlusMarkup
                && bruto < pisoConUtilidad && !direct.Inconsistencias.Any(x => x.Tipo == "UtilidadNegativa"))
                extraInconsistencias.Add(new BudgetInconsistencia
                {
                    Tipo = "UtilidadNegativa",
                    Detalle = $"La venta por factores ({bruto:N0}) no cubre el costo del proyecto con utilidad ({pisoConUtilidad:N0})."
                });
        }
        else
        {
            if (policy.MarkupMethod == MarkupMethod.SobreVenta && policy.SafeUtilidadPct < 100m)
            {
                bruto = subtotal / (1m - policy.SafeUtilidadPct / 100m);
                utilidad = bruto - subtotal;
            }
            else
            {
                utilidad = subtotal * policy.SafeUtilidadPct / 100m;
                bruto = subtotal + utilidad;
            }
            costoProyecto = subtotal + Math.Max(0m, utilidad);
            pisoComercial = costoProyecto;
        }

        var fin = FinalizeVentaYCierre(bruto, direct.TotalUf, pisoComercial, riesgo, direct, policy, settings, extraInconsistencias);
        decimal descuento = fin.Descuento;
        decimal neto = fin.Neto;
        decimal iva = fin.Iva;
        decimal conIva = fin.ConIva;
        decimal venta = fin.Venta;
        diferenciaFactores = fin.DiferenciaFactores;
        decimal ufM2 = settings.MetrosCuadrados > 0m ? venta / settings.MetrosCuadrados : 0m;
        // H-6: margen contra el NETO (post-descuento, pre-IVA), no contra la venta con IVA.
        decimal margenReal = neto > 0m ? (neto - direct.TotalUf) / neto * 100m : 0m;

        return new CostBreakdown
        {
            MaterialesUf = direct.MaterialesUf,
            ManoObraUf = direct.ManoObraUf,
            EquiposUf = direct.EquiposUf,
            SubcontratosUf = direct.SubcontratosUf,
            HerramientasUf = direct.HerramientasUf,
            TransporteUf = direct.TransporteUf,
            MermasUf = direct.MermasUf,
            OrdenesCambioUf = direct.OrdenesCambioUf,
            CostoDirectoUf = direct.TotalUf,
            RiesgoUf = riesgo,
            BaseCostoUf = baseCosto,
            GastosGeneralesUf = gg,
            ValorAgregadoEquiposUf = vaEquipos,
            BaseGgUtUf = baseCosto,
            UtilidadAditivaUf = 0m,
            DiferenciaFactoresUf = diferenciaFactores,
            CostoProyectoUf = costoProyecto,
            SubtotalUf = subtotal,
            UtilidadUf = utilidad,
            DescuentoUf = descuento,
            TotalVentaNetoUf = neto,
            IvaUf = iva,
            TotalVentaConIvaUf = conIva,
            TotalVentaUf = venta,
            UfM2 = ufM2,
            MargenRealPct = margenReal,
            ClosingPolicyResult = fin.Pol,
            ClosingPolicy = fin.Pol.Policy,
            ApuMode = settings.ApuMode,
            Origenes = direct.Origenes,
            OrigenesResumen = SummarizeOrigins(direct.Origenes),
            MaterialesCriticos = SummarizeMaterialTotals(direct.Materiales),
            Inconsistencias = BuildInconsistencias(direct.Inconsistencias, markupErrors, extraInconsistencias)
        };
    }

    private readonly record struct CierreTotals(
        ClosingPolicyResult Pol,
        decimal Neto0,
        decimal Descuento,
        decimal Neto,
        decimal Iva,
        decimal ConIva,
        decimal Venta,
        decimal DiferenciaFactores);

    /// <summary>
    /// Paso final común a ambas políticas de costo: aplica la política de cierre comercial
    /// (<see cref="ClosingPolicyEngine"/>) sobre la venta base neta y luego descuento e IVA de
    /// forma uniforme. Con <see cref="ClosingPolicyType.CostPlusMarkup"/> (por defecto) es
    /// pass-through: <c>neto0 == ventaBaseNeta</c> y el pipeline no cambia.
    /// </summary>
    private static CierreTotals FinalizeVentaYCierre(
        decimal ventaBaseNeta,
        decimal costoEjecucion,
        decimal pisoComercial,
        decimal riesgo,
        DirectCostResult direct,
        MarkupPolicy policy,
        BudgetSettings settings,
        List<BudgetInconsistencia> extraInconsistencias)
    {
        var input = new ClosingPolicyInput
        {
            VentaBaseNetaClp = ventaBaseNeta,
            CostoEjecucionClp = costoEjecucion,
            PisoComercialClp = pisoComercial,
            RiesgoImplicitoPct = costoEjecucion > 0m ? riesgo / costoEjecucion * 100m : 0m,
            Origenes = direct.Origenes
        };
        var pol = ClosingPolicyEngine.ApplyNeto(input, settings.ClosingPolicy);
        decimal neto0 = pol.VentaFinalClp;
        decimal descuento = neto0 * policy.SafeDescuentoPct / 100m;
        decimal neto = Math.Max(0m, neto0 - descuento);
        decimal iva = neto * policy.SafeIvaPct / 100m;
        decimal conIva = neto + iva;
        decimal venta = policy.IncluirIvaEnTotal ? conIva : neto;

        var polType = settings.ClosingPolicy?.Policy ?? ClosingPolicyType.CostPlusMarkup;

        // H-2: "diferencia por factores" (neto vs piso comercial) solo tiene sentido para las
        // políticas que venden por factores/markup. Para Costo+Fee, Precio Objetivo, Suma Alzada
        // y Gainshare el neto no proviene de los factores y el delta sería engañoso.
        bool factorPolicy = polType is ClosingPolicyType.CostPlusMarkup or ClosingPolicyType.UnitPricePure;
        decimal diferenciaFactores = factorPolicy ? neto0 - pisoComercial : 0m;

        // H-1: el bloqueo UtilidadNegativa es policy-aware.
        //   CostPlusMarkup .......... lo maneja la comprobación C-1 previa (BuildLegacy/BuildExcelCostos).
        //   UnitPricePure ........... bloquea si el neto no cubre el piso comercial (los unitarios
        //                             contractuales deben cubrir GG + utilidad).
        //   CostPlusFee ............. bloquea solo si el fee es ≤ 0 (neto ≤ costo de ejecución).
        //   TargetPrice/LumpSum/Gainshare: bloquea solo si hay pérdida de caja real (neto < costo
        //                             de ejecución); entre costo y piso ⇒ advertencia (ApplyNeto).
        bool alreadyHasBlock = direct.Inconsistencias.Any(x => x.Tipo == "UtilidadNegativa")
            || extraInconsistencias.Any(x => x.Tipo == "UtilidadNegativa");
        bool block = polType switch
        {
            ClosingPolicyType.CostPlusMarkup => false,
            ClosingPolicyType.UnitPricePure => neto0 < pisoComercial,
            ClosingPolicyType.CostPlusFee => neto0 <= costoEjecucion,
            _ => neto0 < costoEjecucion,
        };
        if (block && !alreadyHasBlock)
            extraInconsistencias.Add(new BudgetInconsistencia
            {
                Tipo = "UtilidadNegativa",
                Detalle = polType == ClosingPolicyType.UnitPricePure
                    ? $"La venta con la política de cierre ({neto0:N0}) no cubre el costo del proyecto con utilidad ({pisoComercial:N0})."
                    : $"La venta con la política de cierre ({neto0:N0}) no cubre el costo de ejecución ({costoEjecucion:N0}): margen negativo."
            });

        return new CierreTotals(pol, neto0, descuento, neto, iva, conIva, venta, diferenciaFactores);
    }

    /// <summary>
    /// Paridad Excel -=COSTOS=-:
    /// GG/UT sobre Mat+MO+GO; VA sobre equipos; utilidad aditiva;
    /// venta con factores; diferencia por factores = venta − costo proyecto.
    /// </summary>
    private static CostBreakdown BuildExcelCostos(
        DirectCostResult direct,
        BudgetSettings settings,
        MarkupPolicy policy,
        IReadOnlyList<string> markupErrors)
    {
        decimal go = Math.Max(0m, settings.GastosOperacionalesClp);
        decimal moEngine = direct.ManoObraUf;
        decimal mo = settings.ManoObraDotacionClp > 0m ? settings.ManoObraDotacionClp : moEngine;

        // Ejecución = motor − MO motor + MO efectiva + GO
        decimal ejecucion = direct.TotalUf - moEngine + mo + go;
        decimal riesgo = ejecucion * policy.SafeRiesgoPct / 100m;
        decimal baseCosto = ejecucion + riesgo;

        // Excel H161: Mat + MO + GO (sin equipos)
        decimal baseGgUt = Math.Max(0m, direct.MaterialesUf) + mo + go;
        decimal gg = baseGgUt * policy.SafeGastosGeneralesPct / 100m;
        decimal utilidadAditiva = baseGgUt * policy.SafeUtilidadPct / 100m;
        decimal vaEquipos = direct.EquiposUf * policy.SafeValorAgregadoEquiposPct / 100m;
        decimal costoProyecto = baseCosto + gg + utilidadAditiva + vaEquipos;

        // Venta: con factores = presentación cubicación/APU + GO
        // (+ ajuste MO módulo si reemplaza MO del motor).
        decimal bruto;
        if (direct.HasRowLevelMarkups)
        {
            bruto = direct.SumVentaClp + go;
            if (settings.ManoObraDotacionClp > 0m)
                bruto = bruto - moEngine + mo;
        }
        else
        {
            // Sin factores: la venta es el costo proyecto (margen por factores = 0).
            bruto = costoProyecto;
        }

        var extraInconsistencias = new List<BudgetInconsistencia>();
        var fin = FinalizeVentaYCierre(bruto, ejecucion, costoProyecto, riesgo, direct, policy, settings, extraInconsistencias);
        decimal descuento = fin.Descuento;
        decimal neto = fin.Neto;
        decimal iva = fin.Iva;
        decimal conIva = fin.ConIva;
        decimal venta = fin.Venta;
        decimal diferenciaFactores = fin.DiferenciaFactores;
        // C-1 (ruta por factores, política CostPlusMarkup): la venta por factores PRE-política
        // debe cubrir el costo del proyecto con utilidad. El resto de políticas se evalúan en
        // FinalizeVentaYCierre sobre su propio neto (H-1), así que aquí no se duplica.
        var polTipoExcel = settings.ClosingPolicy?.Policy ?? ClosingPolicyType.CostPlusMarkup;
        if (polTipoExcel == ClosingPolicyType.CostPlusMarkup
            && direct.HasRowLevelMarkups && bruto < costoProyecto
            && !direct.Inconsistencias.Any(x => x.Tipo == "UtilidadNegativa")
            && !extraInconsistencias.Any(x => x.Tipo == "UtilidadNegativa"))
            extraInconsistencias.Add(new BudgetInconsistencia
            {
                Tipo = "UtilidadNegativa",
                Detalle = $"La venta por factores ({bruto:N0}) no cubre el costo del proyecto con utilidad ({costoProyecto:N0})."
            });
        decimal ufM2 = settings.MetrosCuadrados > 0m ? venta / settings.MetrosCuadrados : 0m;
        // Margen observado estilo Excel: (neto − costo ejecución) / neto. H-6: la base es el NETO
        // post-descuento pre-IVA, no la venta con IVA (antes inflaba/deformaba el margen).
        decimal margenReal = neto > 0m ? (neto - ejecucion) / neto * 100m : 0m;

        return new CostBreakdown
        {
            MaterialesUf = direct.MaterialesUf,
            ManoObraUf = mo,
            EquiposUf = direct.EquiposUf,
            SubcontratosUf = direct.SubcontratosUf,
            HerramientasUf = direct.HerramientasUf,
            TransporteUf = direct.TransporteUf,
            MermasUf = direct.MermasUf,
            OrdenesCambioUf = direct.OrdenesCambioUf,
            GastosOperacionalesUf = go,
            CostoDirectoUf = ejecucion,
            RiesgoUf = riesgo,
            BaseCostoUf = baseCosto,
            BaseGgUtUf = baseGgUt,
            GastosGeneralesUf = gg,
            ValorAgregadoEquiposUf = vaEquipos,
            UtilidadAditivaUf = utilidadAditiva,
            UtilidadUf = utilidadAditiva,
            CostoProyectoUf = costoProyecto,
            DiferenciaFactoresUf = diferenciaFactores,
            SubtotalUf = costoProyecto,
            DescuentoUf = descuento,
            TotalVentaNetoUf = neto,
            IvaUf = iva,
            TotalVentaConIvaUf = conIva,
            TotalVentaUf = venta,
            UfM2 = ufM2,
            MargenRealPct = margenReal,
            ClosingPolicyResult = fin.Pol,
            ClosingPolicy = fin.Pol.Policy,
            ApuMode = settings.ApuMode,
            Origenes = direct.Origenes,
            OrigenesResumen = SummarizeOrigins(direct.Origenes),
            MaterialesCriticos = SummarizeMaterialTotals(direct.Materiales),
            Inconsistencias = BuildInconsistencias(direct.Inconsistencias, markupErrors, extraInconsistencias)
        };
    }

    private static List<CostOriginSummary> SummarizeOrigins(IEnumerable<CostOriginBreakdown> origins)
    {
        return origins
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Origen) ? BudgetCostOrigin.Cubicacion : x.Origen.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new CostOriginSummary
            {
                Origen = g.Key,
                MaterialesUf = g.Sum(x => x.MaterialesUf),
                ManoObraUf = g.Sum(x => x.ManoObraUf),
                EquiposUf = g.Sum(x => x.EquiposUf),
                SubcontratosUf = g.Sum(x => x.SubcontratosUf),
                HerramientasUf = g.Sum(x => x.HerramientasUf),
                TransporteUf = g.Sum(x => x.TransporteUf),
                MermasUf = g.Sum(x => x.MermasUf),
                OrdenesCambioUf = g.Sum(x => x.OrdenesCambioUf),
                TotalUf = g.Sum(x => x.TotalUf)
            })
            .OrderByDescending(x => x.TotalUf)
            .ToList();
    }

    private static List<MaterialCostTotal> SummarizeMaterialTotals(IEnumerable<MaterialCostTotal> materials)
    {
        return materials
            .Where(x => !string.IsNullOrWhiteSpace(x.Codigo))
            .GroupBy(x => new
            {
                Codigo = x.Codigo.Trim().ToUpperInvariant(),
                Descripcion = x.Descripcion ?? string.Empty,
                Unidad = x.Unidad ?? string.Empty
            })
            .Select(g => new MaterialCostTotal
            {
                Codigo = g.Key.Codigo,
                Descripcion = g.Key.Descripcion,
                Unidad = g.Key.Unidad,
                Cantidad = g.Sum(x => x.Cantidad),
                TotalUf = g.Sum(x => x.TotalUf)
            })
            .OrderByDescending(x => x.TotalUf)
            .Take(20)
            .ToList();
    }

    private static List<BudgetInconsistencia> BuildInconsistencias(
        IEnumerable<BudgetInconsistencia> engineErrors,
        IReadOnlyList<string> markupErrors,
        IEnumerable<BudgetInconsistencia>? extra = null)
    {
        var list = new List<BudgetInconsistencia>(engineErrors);
        foreach (var msg in markupErrors)
            list.Add(new BudgetInconsistencia { Tipo = "PorcentajeInvalido", Detalle = msg });
        if (extra != null)
            list.AddRange(extra);
        return list;
    }
}
