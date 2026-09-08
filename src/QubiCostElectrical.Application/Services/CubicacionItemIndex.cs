using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Índice por <see cref="CubicacionRow.ItemId"/> para evitar recorrer toda la cubicación
/// en cada cambio de partida (<c>RecalculateForItem</c> / <c>GetRowsForItem</c>).
/// </summary>
public static class CubicacionItemIndex
{
    public static Dictionary<string, List<CubicacionRow>> Build(IEnumerable<CubicacionRow>? cubicacion)
    {
        var index = new Dictionary<string, List<CubicacionRow>>(StringComparer.OrdinalIgnoreCase);
        if (cubicacion == null)
            return index;

        foreach (var row in cubicacion)
        {
            string key = row.ItemId ?? "";
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<CubicacionRow>();
                index[key] = list;
            }

            list.Add(row);
        }

        return index;
    }

    public static List<CubicacionRow> GetRows(
        IReadOnlyDictionary<string, List<CubicacionRow>> index,
        string? itemId,
        bool onlyWithCode = false)
    {
        if (!index.TryGetValue(itemId ?? "", out var rows) || rows.Count == 0)
            return new List<CubicacionRow>();

        if (!onlyWithCode)
            return new List<CubicacionRow>(rows);

        var filtered = new List<CubicacionRow>(rows.Count);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.Codigo))
                filtered.Add(row);
        }

        return filtered;
    }

    /// <summary>Una sola pasada lineal cuando no hay índice preconstruido.</summary>
    public static List<CubicacionRow> FilterRows(
        IEnumerable<CubicacionRow>? cubicacion,
        string? itemId,
        bool onlyWithCode = false)
    {
        var result = new List<CubicacionRow>();
        if (cubicacion == null)
            return result;

        string key = itemId ?? "";
        foreach (var row in cubicacion)
        {
            if (!string.Equals(row.ItemId, key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (onlyWithCode && string.IsNullOrWhiteSpace(row.Codigo))
                continue;
            result.Add(row);
        }

        return result;
    }
}
