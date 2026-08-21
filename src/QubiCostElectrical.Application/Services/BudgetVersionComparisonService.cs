using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record BudgetVersionComparisonRow(
    string ItemId,
    string Item,
    string Descripcion,
    decimal BaselineUf,
    decimal CurrentUf,
    decimal DeltaUf,
    decimal DeltaPct,
    string Estado);

public sealed class BudgetVersionComparisonSummary
{
    public string BaselineLabel { get; init; } = "Versión base";
    public string CurrentLabel { get; init; } = "Estado actual";
    public decimal BaselineUf { get; init; }
    public decimal CurrentUf { get; init; }
    public decimal DeltaUf => CurrentUf - BaselineUf;
    public decimal DeltaPct => BaselineUf == 0m ? (CurrentUf == 0m ? 0m : 100m) : (DeltaUf / BaselineUf) * 100m;
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ChangedCount { get; init; }
    public IReadOnlyList<BudgetVersionComparisonRow> Rows { get; init; } = Array.Empty<BudgetVersionComparisonRow>();
}

public sealed class BudgetVersionComparisonService : IVersionComparisonService
{
    public BudgetVersionComparisonSummary Compare(
        BudgetVersionData baseline,
        ProjectFileData current,
        string baselineLabel = "Versión base",
        string currentLabel = "Estado actual")
    {
        baseline ??= new BudgetVersionData();
        current ??= new ProjectFileData();

        var baselineRows = BuildRows(baseline.Itemizado, baseline.Cubicacion);
        var currentRows = BuildRows(current.Itemizado, current.Cubicacion);

        var keys = new HashSet<string>(baselineRows.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in currentRows.Keys)
            keys.Add(key);

        var rows = new List<BudgetVersionComparisonRow>();
        int added = 0;
        int removed = 0;
        int changed = 0;

        foreach (string key in keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            baselineRows.TryGetValue(key, out var baseRow);
            currentRows.TryGetValue(key, out var currentRow);

            decimal baselineUf = baseRow?.TotalUf ?? 0m;
            decimal currentUf = currentRow?.TotalUf ?? 0m;
            decimal deltaUf = currentUf - baselineUf;
            decimal deltaPct = baselineUf == 0m
                ? (currentUf == 0m ? 0m : 100m)
                : deltaUf / baselineUf * 100m;

            string estado = "Sin cambio";
            if (baselineUf == 0m && currentUf > 0m)
            {
                estado = "Nuevo";
                added++;
            }
            else if (baselineUf > 0m && currentUf == 0m)
            {
                estado = "Eliminado";
                removed++;
            }
            else if (deltaUf != 0m)
            {
                estado = deltaUf > 0m ? "Aumentó" : "Disminuyó";
                changed++;
            }

            rows.Add(new BudgetVersionComparisonRow(
                key,
                currentRow?.Item ?? baseRow?.Item ?? key,
                currentRow?.Descripcion ?? baseRow?.Descripcion ?? "Sin descripción",
                baselineUf,
                currentUf,
                deltaUf,
                deltaPct,
                estado));
        }

        return new BudgetVersionComparisonSummary
        {
            BaselineLabel = baselineLabel,
            CurrentLabel = currentLabel,
            BaselineUf = rows.Sum(x => x.BaselineUf),
            CurrentUf = rows.Sum(x => x.CurrentUf),
            AddedCount = added,
            RemovedCount = removed,
            ChangedCount = changed,
            Rows = rows
                .OrderByDescending(x => Math.Abs(x.DeltaUf))
                .ThenBy(x => x.Item, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public BudgetVersionComparisonSummary Compare(
        BudgetVersionData baseline,
        BudgetVersionData current,
        string baselineLabel,
        string currentLabel)
    {
        return Compare(baseline, ToProjectFileData(current), baselineLabel, currentLabel);
    }

    private static ProjectFileData ToProjectFileData(BudgetVersionData version)
    {
        version ??= new BudgetVersionData();
        return new ProjectFileData
        {
            Proyecto = version.Proyecto,
            Itemizado = version.Itemizado,
            Materiales = version.Materiales,
            Cubicacion = version.Cubicacion,
            Plantillas = version.Plantillas,
            ConjuntosElectricos = version.ConjuntosElectricos,
            CubicacionRecursos = version.CubicacionRecursos,
            CubicacionZonaCantidades = version.CubicacionZonaCantidades,
            CubicacionValorizaciones = version.CubicacionValorizaciones,
            PartidaApuVinculos = version.PartidaApuVinculos,
            QtyHeadersByItem = version.QtyHeadersByItem,
            Zonas = version.Zonas,
            Mediciones = version.Mediciones,
            Apus = version.Apus,
            OrdenesCambio = version.OrdenesCambio,
            CubicacionSC = version.CubicacionSC,
            CubicacionScConfig = version.CubicacionScConfig
        };
    }

    private static Dictionary<string, ComparisonRow> BuildRows(
        IReadOnlyList<ItemizadoRow> itemizado,
        IReadOnlyList<CubicacionRow> cubicacion)
    {
        var rows = new Dictionary<string, ComparisonRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in itemizado ?? Array.Empty<ItemizadoRow>())
        {
            if (string.IsNullOrWhiteSpace(item.Id) || item.EsTitulo)
                continue;

            rows[item.Id] = new ComparisonRow(item.Id, item.Item ?? "", item.Descripcion ?? "", 0m);
        }

        foreach (var group in (cubicacion ?? Array.Empty<CubicacionRow>())
            .Where(x => !string.IsNullOrWhiteSpace(x.ItemId))
            .GroupBy(x => x.ItemId, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            rows.TryGetValue(group.Key, out var existing);
            rows[group.Key] = new ComparisonRow(
                group.Key,
                existing?.Item ?? string.Empty,
                existing?.Descripcion ?? first.Descripcion ?? "Sin descripción",
                group.Sum(x => x.UfTotal));
        }

        return rows;
    }

    private sealed record ComparisonRow(string ItemId, string Item, string Descripcion, decimal TotalUf);
}
