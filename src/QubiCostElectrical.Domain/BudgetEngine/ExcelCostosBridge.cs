using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Totales de gastos operacionales y MO de dotación para el puente de costos.
/// Incluye duración de obra y leyes sociales cuando están en el proyecto.
/// </summary>
public static class ExcelCostosBridge
{
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
            return list.Sum(r => Math.Max(0, r.Dotacion) * Math.Max(0m, r.SueldoMensualClp) * r.FraccionEfectiva);

        decimal total = 0m;
        foreach (var r in list)
        {
            var fracc = r.FraccionEfectiva;
            for (int i = 0; i < months; i++)
            {
                int d = Math.Max(0, r.Dotacion);
                if (r.DotacionMensual != null && i < r.DotacionMensual.Count && r.DotacionMensual[i] >= 0)
                    d = r.DotacionMensual[i];
                total += d * Math.Max(0m, r.SueldoMensualClp) * fracc;
            }
        }

        return total;
    }

    /// <summary>
    /// Días-hombre (DD/HH) del plan de mano de obra: Σ dotación-mes × 22 jornadas, la misma cuenta
    /// que la columna TOTAL DD/HH del Excel (MANO DE OBRA!U23 = 182 × 22 = 4.004). Es el
    /// denominador con el que el estimador lee la tarifa y cuadra el FA, no un costo.
    /// </summary>
    public static decimal SumDiasHombre(
        IEnumerable<MoDirectaRow>? directa,
        IEnumerable<MoIndirectaRow>? indirecta,
        int fallbackMonths = 0)
    {
        var dir = directa ?? Enumerable.Empty<MoDirectaRow>();
        var ind = indirecta ?? Enumerable.Empty<MoIndirectaRow>();
        int months = dir.Select(r => r.DotacionMensual?.Count ?? 0)
            .Concat(ind.Select(r => r.DotacionMensual?.Count ?? 0))
            .DefaultIfEmpty(0).Max();
        if (months <= 0)
            months = Math.Max(0, fallbackMonths);

        decimal dotacion = 0m;
        foreach (var r in dir)
        {
            if (months <= 0)
            {
                dotacion += Math.Max(0, r.Dotacion);
                continue;
            }
            for (int i = 0; i < months; i++)
                dotacion += DotacionDelMes(r.DotacionMensual, i, r.Dotacion);
        }
        foreach (var r in ind)
        {
            decimal fracc = r.FraccionEfectiva;
            if (months <= 0)
            {
                dotacion += Math.Max(0, r.Dotacion) * fracc;
                continue;
            }
            for (int i = 0; i < months; i++)
                dotacion += DotacionDelMes(r.DotacionMensual, i, r.Dotacion) * fracc;
        }

        return Math.Round(dotacion * MaterialMoResolver.DiasPorJornal, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Suma solo la MO directa (sueldos base × dotación × meses) × (1 + leyes%).
    /// Equivalente al costo directo de MO en Excel (MANO DE OBRA!T23).
    /// </summary>
    public static decimal SumMoDirectaConLeyes(
        IEnumerable<MoDirectaRow>? directa,
        int fallbackMonths = 0,
        decimal leyesSocialesPct = 0m)
    {
        var dir = directa ?? Enumerable.Empty<MoDirectaRow>();
        decimal baseMo = SumMoDirecta(dir, fallbackMonths);
        decimal leyes = MarkupPolicy.ClampPct(leyesSocialesPct) / 100m;
        return baseMo * (1m + leyes);
    }

    /// <summary>
    /// Días-hombre (DD/HH) de la mano de obra directa únicamente: Σ dotación-mes directa × 22 jornadas.
    /// Exactamente la cuenta de MANO DE OBRA!U23 del Excel original (182 × 22 = 4.004 DD/HH).
    /// </summary>
    public static decimal SumDiasHombreDirecta(
        IEnumerable<MoDirectaRow>? directa,
        int fallbackMonths = 0)
    {
        var dir = directa ?? Enumerable.Empty<MoDirectaRow>();
        int months = dir.Select(r => r.DotacionMensual?.Count ?? 0).DefaultIfEmpty(0).Max();
        if (months <= 0)
            months = Math.Max(0, fallbackMonths);

        decimal dotacion = 0m;
        foreach (var r in dir)
        {
            if (months <= 0)
            {
                dotacion += Math.Max(0, r.Dotacion);
                continue;
            }
            for (int i = 0; i < months; i++)
                dotacion += DotacionDelMes(r.DotacionMensual, i, r.Dotacion);
        }

        return Math.Round(dotacion * MaterialMoResolver.DiasPorJornal, 0, MidpointRounding.AwayFromZero);
    }

    private static int DotacionDelMes(List<int>? dotacionMensual, int mes, int dotacionBase)
    {
        if (dotacionMensual != null && mes < dotacionMensual.Count && dotacionMensual[mes] >= 0)
            return dotacionMensual[mes];
        return Math.Max(0, dotacionBase);
    }

    public static BudgetSettings EnrichFromProjectData(BudgetSettings settings, ProjectFileData? data)
    {        settings ??= BudgetSettings.FromProject(null);
        data ??= new ProjectFileData();
        var project = data.Proyecto ?? new ProjectInfoRow();
        int months = project.MoDuracionMeses > 0 ? project.MoDuracionMeses : 12;
        settings.GastosOperacionalesClp = SumGastosOperacionalesClp(data.GastosOperacionales) + SumStaffingPlanClp(data.StaffingPlan);
        settings.NegotiatedDiscounts = data.NegotiatedDiscounts ?? new List<NegotiatedDiscountRow>();
        // La política de cierre vive en ProjectFileData (no en ProjectInfoRow): este es el punto
        // único donde el motor la recibe para que mande sobre el precio de venta en todos los consumidores.
        settings.ClosingPolicy = data.ClosingPolicy;
        // Para el costo de ejecución del proyecto en el cierre (paridad COSTOS!I164), la M.O. de dotación es la TOTAL (Directa + Indirecta).
        // Si el proyecto tiene dotación detallada (filas en MoDirecta/MoIndirecta) y MoTotalClp no incluye la indirecta
        // (por ejemplo MoTotalClp <= MoDirectaClp habiendo MoIndirecta), la suma de filas manda para no omitir la supervisión.
        decimal sumFromRows = SumMoDotacionClp(
            data.MoDirecta,
            data.MoIndirecta,
            fallbackMonths: months,
            leyesSocialesPct: project.LeyesSocialesMoPct);

        bool moTotalOmiteIndirecta = data.MoIndirecta != null && data.MoIndirecta.Count > 0 && project.MoTotalClp <= project.MoDirectaClp;
        settings.ManoObraDotacionClp = (project.MoTotalClp > 0m && !moTotalOmiteIndirecta && project.MoTotalClp >= sumFromRows)
            ? project.MoTotalClp
            : (sumFromRows > 0m
                ? sumFromRows
                : (project.MoTotalClp > 0m ? project.MoTotalClp : project.MoDirectaClp));

        // La M.O. directa se almacena explícitamente para calibración de FA y comparación de partidas (COSTOS!G192).
        decimal sumDirecta = SumMoDirectaConLeyes(
            data.MoDirecta,
            fallbackMonths: months,
            leyesSocialesPct: project.LeyesSocialesMoPct);

        settings.ManoObraDirectaDotacionClp = project.MoDirectaClp > 0m
            ? project.MoDirectaClp
            : (sumDirecta > 0m
                ? sumDirecta
                : settings.ManoObraDotacionClp);
        return settings;
    }

    /// <summary>Factores de presentación estándar.</summary>
    public static IReadOnlyDictionary<string, decimal> PresetFactoresEstandar { get; } =
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

    /// <summary>Típicos genéricos (promedio histórico de varios proyectos).</summary>
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

    public static void ApplyPoliticaEstandar(ProjectInfoRow project)
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
