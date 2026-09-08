using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Totales de gastos operacionales y MO de dotación para el puente Excel COSTOS.
/// Incluye duración de obra y leyes sociales cuando están en el proyecto.
/// </summary>
public static class ExcelCostosBridge
{
    public const string PresetPel029 = "PEL029";
    public const string PresetTipicos = "Tipicos";

    public static decimal SumGastosOperacionalesClp(IEnumerable<GastoOperacionalRow>? rows)
    {
        if (rows == null)
            return 0m;

        decimal total = 0m;
        foreach (var cat in rows)
        {
            if (cat.Items is { Count: > 0 })
            {
                foreach (var item in cat.Items)
                    total += Math.Max(0m, item.Cantidad) * Math.Max(0m, item.PrecioUnitarioClp);
            }
            else
            {
                total += Math.Max(0m, cat.MontoClp);
            }
        }

        return total;
    }

    /// <summary>Dotación indirecta por rol (Administrador, Supervisor, etc.) × dedicación% × meses.</summary>
    public static decimal SumStaffingPlanClp(IEnumerable<StaffingRoleRow>? roles)
        => roles?.Sum(r => r.TotalClp) ?? 0m;

    /// <summary>
    /// Suma MO directa + indirecta (sueldos base × dotación × meses) × (1 + leyes%).
    /// </summary>
    public static decimal SumMoDotacionClp(
        IEnumerable<MoDirectaRow>? directa,
        IEnumerable<MoIndirectaRow>? indirecta,
        int fallbackMonths = 0,
        decimal leyesSocialesPct = 0m)
    {
        var dir = directa ?? Enumerable.Empty<MoDirectaRow>();
        var ind = indirecta ?? Enumerable.Empty<MoIndirectaRow>();
        decimal baseMo = SumMoDirecta(dir, fallbackMonths) + SumMoIndirecta(ind, fallbackMonths);
        decimal leyes = MarkupPolicy.ClampPct(leyesSocialesPct) / 100m;
        return baseMo * (1m + leyes);
    }

    public static decimal SumMoDirecta(IEnumerable<MoDirectaRow> rows, int fallbackMonths = 0)
    {
        var list = rows?.ToList() ?? new List<MoDirectaRow>();
        int months = list.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max();
        if (months <= 0)
            months = Math.Max(0, fallbackMonths);

        if (months <= 0)
            return list.Sum(r => Math.Max(0, r.Dotacion) * Math.Max(0m, r.SueldoMensualClp));

        decimal total = 0m;
        foreach (var r in list)
        {
            for (int i = 0; i < months; i++)
            {
                int d = Math.Max(0, r.Dotacion);
                if (r.DotacionMensual != null && i < r.DotacionMensual.Count && r.DotacionMensual[i] >= 0)
                    d = r.DotacionMensual[i];
                total += d * Math.Max(0m, r.SueldoMensualClp);
            }
        }

        return total;
    }

    public static decimal SumMoIndirecta(IEnumerable<MoIndirectaRow> rows, int fallbackMonths = 0)
    {
        var list = rows?.ToList() ?? new List<MoIndirectaRow>();
        int months = list.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max();
        if (months <= 0)
            months = Math.Max(0, fallbackMonths);

        if (months <= 0)
            return list.Sum(r => Math.Max(0, r.Dotacion) * Math.Max(0m, r.SueldoMensualClp));

        decimal total = 0m;
        foreach (var r in list)
        {
            for (int i = 0; i < months; i++)
            {
                int d = Math.Max(0, r.Dotacion);
                if (r.DotacionMensual != null && i < r.DotacionMensual.Count && r.DotacionMensual[i] >= 0)
                    d = r.DotacionMensual[i];
                total += d * Math.Max(0m, r.SueldoMensualClp);
            }
        }

        return total;
    }

    public static BudgetSettings EnrichFromProjectData(BudgetSettings settings, ProjectFileData? data)
    {
        settings ??= BudgetSettings.FromProject(null);
        data ??= new ProjectFileData();
        var project = data.Proyecto ?? new ProjectInfoRow();
        int months = project.MoDuracionMeses > 0 ? project.MoDuracionMeses : 12;
        settings.GastosOperacionalesClp = SumGastosOperacionalesClp(data.GastosOperacionales) + SumStaffingPlanClp(data.StaffingPlan);
        settings.NegotiatedDiscounts = data.NegotiatedDiscounts ?? new List<NegotiatedDiscountRow>();
        // La política de cierre vive en ProjectFileData (no en ProjectInfoRow): este es el punto
        // único donde el motor la recibe para que mande sobre el precio de venta en todos los consumidores.
        settings.ClosingPolicy = data.ClosingPolicy;
        // Preferir total del módulo MO (leyes + curva); si no hay, recomputar desde filas.
        settings.ManoObraDotacionClp = project.MoTotalClp > 0m
            ? project.MoTotalClp
            : SumMoDotacionClp(
                data.MoDirecta,
                data.MoIndirecta,
                fallbackMonths: months,
                leyesSocialesPct: project.LeyesSocialesMoPct);
        return settings;
    }

    /// <summary>Factores de presentación del Excel PEL029A2026 (E194–E202).</summary>
    public static IReadOnlyDictionary<string, decimal> PresetFactoresPel029 { get; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Conductores"] = 1.57m,
            ["Canalización"] = 1.57m,
            ["Tableros"] = 1.30m,
            ["Iluminación"] = 1.30m,
            ["Equipos"] = 1.30m,
            ["Accesorios"] = 1.30m,
            ["Enchufes y tomacorrientes"] = 1.30m,
            ["Protecciones"] = 1.30m,
            ["Puesta a tierra"] = 1.30m,
        };

    /// <summary>Típicos genéricos PEL (promedio histórico de varios proyectos).</summary>
    public static IReadOnlyDictionary<string, decimal> PresetFactoresTipicos { get; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["Conductores"] = 1.50m,
            ["Canalización"] = 1.44m,
            ["Tableros"] = 1.10m,
            ["Iluminación"] = 1.17m,
            ["Equipos"] = 1.12m,
            ["Accesorios"] = 1.14m,
            ["Enchufes y tomacorrientes"] = 1.05m,
            ["Protecciones"] = 1.12m,
            ["Puesta a tierra"] = 1.20m,
        };

    public static void ApplyPoliticaExcelPel029(ProjectInfoRow project)
    {
        project ??= new ProjectInfoRow();
        project.GastosGeneralesPct = 5m;
        project.UtilidadPct = 8m;
        project.ValorAgregadoEquiposPct = 3m;
        project.PoliticaCostosExcel = true;
        project.MarkupMethod = "SobreCosto";
    }

    /// <summary>Factor × en cubicación: markup × (1 − descuento%).</summary>
    public static decimal PresentationFactor(decimal markup, decimal discountPct)
    {
        markup = Math.Clamp(markup, 1m, 5m);
        discountPct = MarkupPolicy.ClampPct(discountPct);
        return markup * (1m - discountPct / 100m);
    }
}
