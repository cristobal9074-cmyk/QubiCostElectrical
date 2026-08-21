using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record BudgetVarianceRow(
    string ItemId,
    string Item,
    string Descripcion,
    decimal PresupuestoBaseUf,
    decimal PresupuestoActualUf,
    decimal CostoRealUf,
    decimal DesviacionVsBaseUf,
    decimal DesviacionVsBasePct,
    decimal DesviacionRealVsActualUf,
    decimal DesviacionRealVsActualPct,
    string Estado);

public sealed class BudgetVarianceSummary
{
    public decimal BaseUf { get; init; }
    public decimal ActualUf { get; init; }
    public decimal RealUf { get; init; }
    public decimal DeltaActualVsBaseUf { get; init; }
    public decimal DeltaActualVsBasePct { get; init; }
    public decimal DeltaRealVsActualUf { get; init; }
    public decimal DeltaRealVsActualPct { get; init; }
    public List<BudgetVarianceRow> Rows { get; init; } = new();
    public string Estado { get; init; } = "Sin datos";
}

/// <summary>
/// Hist.: análisis liviano de desviación presupuestal sin dependencias gráficas.
/// Compara versión base, presupuesto actual y costos reales manuales.
/// </summary>
public sealed class BudgetVarianceService
{
    public BudgetVarianceSummary Analyze(
        IReadOnlyList<ItemizadoRow>? itemizado,
        IReadOnlyList<CubicacionRow>? actualCubicacion,
        IReadOnlyList<BudgetVersionRow>? versiones,
        IReadOnlyList<RealCostRow>? realCosts,
        decimal alertThresholdPct = 8m)
    {
        itemizado ??= Array.Empty<ItemizadoRow>();
        actualCubicacion ??= Array.Empty<CubicacionRow>();
        versiones ??= Array.Empty<BudgetVersionRow>();
        realCosts ??= Array.Empty<RealCostRow>();

        var baseVersion = versiones
            .OrderBy(x => x.FechaCreacion == default ? DateTime.MaxValue : x.FechaCreacion)
            .FirstOrDefault(x => x.Snapshot?.Cubicacion != null && x.Snapshot.Cubicacion.Count > 0);

        var baseRows = baseVersion?.Snapshot?.Cubicacion ?? new List<CubicacionRow>();
        var baseByItem = baseRows.GroupBy(x => x.ItemId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => x.UfTotal), StringComparer.OrdinalIgnoreCase);
        var actualByItem = actualCubicacion.GroupBy(x => x.ItemId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => x.UfTotal), StringComparer.OrdinalIgnoreCase);
        var realByItem = realCosts.GroupBy(x => x.ItemId ?? "").ToDictionary(g => g.Key, g => g.Sum(x => x.CostoRealUf), StringComparer.OrdinalIgnoreCase);

        var itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in itemizado.Select(x => x.Id)) if (!string.IsNullOrWhiteSpace(id)) itemIds.Add(id);
        foreach (var id in baseByItem.Keys) if (!string.IsNullOrWhiteSpace(id)) itemIds.Add(id);
        foreach (var id in actualByItem.Keys) if (!string.IsNullOrWhiteSpace(id)) itemIds.Add(id);
        foreach (var id in realByItem.Keys) if (!string.IsNullOrWhiteSpace(id)) itemIds.Add(id);

        var itemMap = itemizado.Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var rows = new List<BudgetVarianceRow>();

        foreach (string itemId in itemIds)
        {
            decimal baseUf = baseByItem.TryGetValue(itemId, out var b) ? b : 0m;
            decimal actualUf = actualByItem.TryGetValue(itemId, out var a) ? a : 0m;
            decimal realUf = realByItem.TryGetValue(itemId, out var r) ? r : 0m;
            decimal deltaBase = actualUf - baseUf;
            decimal pctBase = baseUf == 0m ? (actualUf == 0m ? 0m : 100m) : deltaBase / baseUf * 100m;
            decimal deltaReal = realUf - actualUf;
            decimal pctReal = actualUf == 0m ? (realUf == 0m ? 0m : 100m) : deltaReal / actualUf * 100m;
            string estado = Math.Abs(pctBase) >= alertThresholdPct || Math.Abs(pctReal) >= alertThresholdPct ? "Alerta" : "Controlado";

            itemMap.TryGetValue(itemId, out var item);
            rows.Add(new BudgetVarianceRow(
                itemId,
                item?.Item ?? item?.WBSCode ?? "",
                item?.Descripcion ?? "Partida sin descripción",
                baseUf,
                actualUf,
                realUf,
                deltaBase,
                pctBase,
                deltaReal,
                pctReal,
                estado));
        }

        decimal totalBase = rows.Sum(x => x.PresupuestoBaseUf);
        decimal totalActual = rows.Sum(x => x.PresupuestoActualUf);
        decimal totalReal = rows.Sum(x => x.CostoRealUf);
        decimal totalDeltaBase = totalActual - totalBase;
        decimal totalPctBase = totalBase == 0m ? (totalActual == 0m ? 0m : 100m) : totalDeltaBase / totalBase * 100m;
        decimal totalDeltaReal = totalReal - totalActual;
        decimal totalPctReal = totalActual == 0m ? (totalReal == 0m ? 0m : 100m) : totalDeltaReal / totalActual * 100m;

        return new BudgetVarianceSummary
        {
            BaseUf = totalBase,
            ActualUf = totalActual,
            RealUf = totalReal,
            DeltaActualVsBaseUf = totalDeltaBase,
            DeltaActualVsBasePct = totalPctBase,
            DeltaRealVsActualUf = totalDeltaReal,
            DeltaRealVsActualPct = totalPctReal,
            Rows = rows.OrderByDescending(x => Math.Abs(x.DesviacionVsBasePct)).ThenBy(x => x.Item).ToList(),
            Estado = rows.Any(x => x.Estado == "Alerta") ? "Alerta" : "Controlado"
        };
    }
}
