using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class DashboardExecutiveService
{
    private static readonly decimal[] DefaultSensitivityPcts = { -10m, -5m, 0m, 5m, 10m, 15m, 20m };

    public DashboardExecutiveViewModel BuildViewModel(
        CostBreakdown cost,
        FinalReviewSummary review,
        IEnumerable<ItemizadoRow> itemizado,
        ProjectInfoRow project,
        string currencyLabel)
    {
        cost ??= new CostBreakdown();
        review ??= new FinalReviewSummary();
        project ??= new ProjectInfoRow();
        var items = itemizado?.ToList() ?? new List<ItemizadoRow>();

        int totalPartidas = items.Count(x => !x.EsTitulo);
        int listas = CountPartidasListas(review, totalPartidas);
        decimal impacto10 = CalculateMaterialImpactOnSalePct(cost, project, 10m);
        string riesgo = RiskLabel(review, impacto10);

        var kpis = new[]
        {
            new DashboardExecutiveKpiDefinition(
                "TOTAL VENTA",
                "Valor comercial oficial",
                "Total venta calculado por BudgetEngine: costo directo + riesgo + gastos generales + utilidad - descuento + IVA si corresponde.",
                true,
                DashboardExecutiveKpiAccent.Primary,
                DashboardExecutiveKpiValueKind.Money,
                cost.TotalVentaUf,
                null),
            new DashboardExecutiveKpiDefinition(
                "PARTIDAS LISTAS",
                $"Confianza {review.ConfidencePct:N0}%",
                "Partidas listas según revisión final: sin vacíos críticos y con APU cuando corresponde. Corrige pendientes en Revisión final.",
                false,
                ProgressAccent(listas, totalPartidas),
                DashboardExecutiveKpiValueKind.Text,
                null,
                totalPartidas > 0 ? $"{listas:N0}/{totalPartidas:N0}" : "0/0"),
            new DashboardExecutiveKpiDefinition(
                $"{NormalizeCurrencyLabel(currencyLabel)} / m²",
                "Costo comercial por superficie",
                "Total venta / m². Configura m² en Datos del proyecto si falta.",
                false,
                DashboardExecutiveKpiAccent.Orange,
                cost.UfM2 > 0m ? DashboardExecutiveKpiValueKind.Money : DashboardExecutiveKpiValueKind.ConfigurationHint,
                cost.UfM2 > 0m ? cost.UfM2 : null,
                null),
            new DashboardExecutiveKpiDefinition(
                "RIESGO OFERTA",
                $"+10% materiales: {impacto10:N1}%",
                "Riesgo de oferta combina críticos del checklist, advertencias y sensibilidad ante alza de materiales.",
                false,
                RiskAccent(riesgo),
                DashboardExecutiveKpiValueKind.Text,
                null,
                riesgo)
        };

        return new DashboardExecutiveViewModel(
            kpis,
            RankPartidas(cost, items),
            RankMateriales(cost),
            BuildSensitivityCurveOnSale(cost, project, DefaultSensitivityPcts),
            impacto10);
    }

    public decimal CalculateMaterialSensitivityTotal(CostBreakdown cost, ProjectInfoRow project, decimal materialVariationPct)
    {
        cost ??= new CostBreakdown();
        project ??= new ProjectInfoRow();

        decimal materialAjustado = cost.MaterialesUf * (1m + materialVariationPct / 100m);
        decimal directo = Math.Max(0m, cost.CostoDirectoUf - cost.MaterialesUf + materialAjustado);
        decimal riesgo = directo * Math.Max(0m, project.RiesgoPct) / 100m;
        decimal baseCosto = directo + riesgo;
        decimal gg;
        decimal utilidad;
        decimal va = cost.EquiposUf * Math.Max(0m, project.ValorAgregadoEquiposPct) / 100m;
        decimal subtotal;
        if (project.PoliticaCostosExcel)
        {
            decimal baseGgUt = cost.BaseGgUtUf > 0m
                ? cost.BaseGgUtUf
                : Math.Max(0m, cost.MaterialesUf) + Math.Max(0m, cost.ManoObraUf) + Math.Max(0m, cost.GastosOperacionalesUf);
            // Sensibilidad: ajustar materiales en la base GG/UT.
            decimal matDelta = cost.MaterialesUf * (materialVariationPct / 100m);
            baseGgUt = Math.Max(0m, baseGgUt + matDelta);
            gg = baseGgUt * Math.Max(0m, project.GastosGeneralesPct) / 100m;
            utilidad = baseGgUt * Math.Max(0m, project.UtilidadPct) / 100m;
            subtotal = baseCosto + gg + utilidad + va;
            return project.IncluirIvaEnTotal
                ? subtotal * (1m + (project.IvaPct <= 0m ? 19m : project.IvaPct) / 100m)
                : subtotal;
        }

        gg = baseCosto * Math.Max(0m, project.GastosGeneralesPct) / 100m;
        subtotal = baseCosto + gg + va;
        utilidad = subtotal * Math.Max(0m, project.UtilidadPct) / 100m;
        decimal bruto = subtotal + utilidad;
        decimal descuento = bruto * Math.Max(0m, project.DescuentoPct) / 100m;
        decimal neto = Math.Max(0m, bruto - descuento);
        decimal ivaPct = project.IvaPct <= 0m ? 19m : project.IvaPct;
        decimal iva = neto * ivaPct / 100m;
        return project.IncluirIvaEnTotal ? neto + iva : neto;
    }

    /// <summary>
    /// Venta proyectada del escenario de materiales, anclada en el total oficial del motor.
    /// La curva se corrige por el mismo delta que el motor aplica en 0%, de modo que el punto base
    /// coincide exactamente con el KPI de venta sin importar la política de costos aplicada.
    /// </summary>
    public decimal ProjectSaleOnMaterialVariation(CostBreakdown cost, ProjectInfoRow project, decimal materialVariationPct)
    {
        cost ??= new CostBreakdown();
        project ??= new ProjectInfoRow();

        decimal delta = CalculateMaterialSensitivityTotal(cost, project, materialVariationPct)
                      - CalculateMaterialSensitivityTotal(cost, project, 0m);

        return cost.TotalVentaUf + delta;
    }

    /// <summary>
    /// Impacto de la variación de materiales medido sobre la MISMA venta neta que muestra el KPI
    /// (antes se medía contra el costo total proyecto, así que gráfico y KPI no cerraban).
    /// </summary>
    public decimal CalculateMaterialImpactOnSalePct(CostBreakdown cost, ProjectInfoRow project, decimal materialVariationPct)
    {
        cost ??= new CostBreakdown();
        if (cost.TotalVentaUf <= 0m)
            return 0m;

        decimal proyectada = ProjectSaleOnMaterialVariation(cost, project, materialVariationPct);
        return (proyectada - cost.TotalVentaUf) / cost.TotalVentaUf * 100m;
    }

    /// <summary>Curva de sensibilidad anclada a la venta neta oficial, con los mismos puntos del contrato.</summary>
    public IReadOnlyList<DashboardSensitivityPoint> BuildSensitivityCurveOnSale(
        CostBreakdown cost,
        ProjectInfoRow project,
        IEnumerable<decimal> scenarioPcts)
    {
        return (scenarioPcts ?? DefaultSensitivityPcts)
            .Select(pct => new DashboardSensitivityPoint(pct, ProjectSaleOnMaterialVariation(cost, project, pct)))
            .ToList();
    }

    public string RiskLabel(FinalReviewSummary review, decimal impactoMaterial10Pct)
    {
        if (review.CriticalCount > 0 || impactoMaterial10Pct >= 5m) return "ALTO";
        if (review.WarningCount > 0 || impactoMaterial10Pct >= 2.5m) return "MEDIO";
        return "BAJO";
    }

    public IReadOnlyList<DashboardExecutivePartidaRow> RankPartidas(CostBreakdown cost, IEnumerable<ItemizadoRow> itemizado)
    {
        var items = (itemizado ?? Enumerable.Empty<ItemizadoRow>())
            .Where(i => !i.EsTitulo && !string.IsNullOrWhiteSpace(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return (cost.Origenes ?? Enumerable.Empty<CostOriginBreakdown>())
            .Where(x => x.TotalUf > 0m)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.ItemId) ? (x.Codigo + "|" + x.Descripcion) : x.ItemId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                string item = "-";
                string partida = ShortLabel(g.First().Descripcion, 80);
                if (!string.IsNullOrWhiteSpace(g.First().ItemId) && items.TryGetValue(g.First().ItemId, out var it))
                {
                    item = string.IsNullOrWhiteSpace(it.Item) ? "-" : it.Item;
                    partida = string.IsNullOrWhiteSpace(it.Descripcion) ? partida : it.Descripcion;
                }

                return new DashboardExecutivePartidaRow(item, partida, g.Sum(x => x.TotalUf));
            })
            .OrderByDescending(x => x.TotalUf)
            .ToList();
    }

    public IReadOnlyList<DashboardExecutiveMaterialRow> RankMateriales(CostBreakdown cost)
    {
        return (cost.MaterialesCriticos ?? new List<MaterialCostTotal>())
            .Where(x => x.TotalUf > 0m)
            .Select(x => new DashboardExecutiveMaterialRow(
                string.IsNullOrWhiteSpace(x.Codigo) ? "APU" : x.Codigo,
                string.IsNullOrWhiteSpace(x.Descripcion) ? "Material sin descripción" : x.Descripcion,
                x.Unidad ?? "",
                x.Cantidad,
                x.TotalUf))
            .OrderByDescending(x => x.TotalUf)
            .ToList();
    }

    public IReadOnlyList<DashboardSensitivityPoint> BuildSensitivityCurve(
        CostBreakdown cost,
        ProjectInfoRow project,
        IEnumerable<decimal> scenarioPcts)
    {
        return (scenarioPcts ?? DefaultSensitivityPcts)
            .Select(pct => new DashboardSensitivityPoint(pct, CalculateMaterialSensitivityTotal(cost, project, pct)))
            .ToList();
    }

    private static int CountPartidasListas(FinalReviewSummary review, int totalPartidas)
    {
        if (totalPartidas <= 0) return 0;
        int pendientes = review.EmptyPartidas + review.MaterialsWithoutPrice;
        return Math.Max(0, totalPartidas - pendientes);
    }

    private static DashboardExecutiveKpiAccent RiskAccent(string risk) => risk switch
    {
        "ALTO" => DashboardExecutiveKpiAccent.RiskHigh,
        "MEDIO" => DashboardExecutiveKpiAccent.RiskMedium,
        _ => DashboardExecutiveKpiAccent.RiskLow
    };

    private static DashboardExecutiveKpiAccent ProgressAccent(int listas, int totalPartidas)
    {
        if (totalPartidas <= 0) return DashboardExecutiveKpiAccent.Orange;

        decimal pct = (decimal)listas / totalPartidas;
        if (pct >= 1m) return DashboardExecutiveKpiAccent.RiskLow;
        if (pct >= 0.8m) return DashboardExecutiveKpiAccent.RiskMedium;
        return DashboardExecutiveKpiAccent.RiskHigh;
    }

    private static string NormalizeCurrencyLabel(string currencyLabel)
    {
        string label = (currencyLabel ?? "").Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(label) ? "CLP" : label;
    }

    private static string ShortLabel(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "General";
        text = text.Trim();
        return text.Length <= max ? text : text.Substring(0, Math.Max(0, max - 1)) + "…";
    }
}
