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
        if (direct.HasRowLevelMarkups)
        {
            bruto = direct.SumVentaClp;
            utilidad = bruto - subtotal;
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
        }
        decimal descuento = bruto * policy.SafeDescuentoPct / 100m;
        decimal neto = Math.Max(0m, bruto - descuento);
        decimal iva = neto * policy.SafeIvaPct / 100m;
        decimal conIva = neto + iva;
        decimal venta = policy.IncluirIvaEnTotal ? conIva : neto;
        decimal ufM2 = settings.MetrosCuadrados > 0m ? venta / settings.MetrosCuadrados : 0m;
        decimal margenReal = venta > 0m ? (venta - direct.TotalUf) / venta * 100m : 0m;

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
            DiferenciaFactoresUf = 0m,
            CostoProyectoUf = subtotal + Math.Max(0m, utilidad),
            SubtotalUf = subtotal,
            UtilidadUf = utilidad,
            DescuentoUf = descuento,
            TotalVentaNetoUf = neto,
            IvaUf = iva,
            TotalVentaConIvaUf = conIva,
            TotalVentaUf = venta,
            UfM2 = ufM2,
            MargenRealPct = margenReal,
            ApuMode = settings.ApuMode,
            Origenes = direct.Origenes,
            OrigenesResumen = SummarizeOrigins(direct.Origenes),
            MaterialesCriticos = SummarizeMaterialTotals(direct.Materiales),
            Inconsistencias = BuildInconsistencias(direct.Inconsistencias, markupErrors)
        };
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

        decimal descuento = bruto * policy.SafeDescuentoPct / 100m;
        decimal neto = Math.Max(0m, bruto - descuento);
        decimal iva = neto * policy.SafeIvaPct / 100m;
        decimal conIva = neto + iva;
        decimal venta = policy.IncluirIvaEnTotal ? conIva : neto;
        decimal diferenciaFactores = venta - costoProyecto;
        decimal ufM2 = settings.MetrosCuadrados > 0m ? venta / settings.MetrosCuadrados : 0m;
        // Margen observado estilo Excel: (venta − costo ejecución) / venta
        decimal margenReal = venta > 0m ? (venta - ejecucion) / venta * 100m : 0m;

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
            ApuMode = settings.ApuMode,
            Origenes = direct.Origenes,
            OrigenesResumen = SummarizeOrigins(direct.Origenes),
            MaterialesCriticos = SummarizeMaterialTotals(direct.Materiales),
            Inconsistencias = BuildInconsistencias(direct.Inconsistencias, markupErrors)
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
        IReadOnlyList<string> markupErrors)
    {
        var list = new List<BudgetInconsistencia>(engineErrors);
        foreach (var msg in markupErrors)
            list.Add(new BudgetInconsistencia { Tipo = "PorcentajeInvalido", Detalle = msg });
        return list;
    }
}
