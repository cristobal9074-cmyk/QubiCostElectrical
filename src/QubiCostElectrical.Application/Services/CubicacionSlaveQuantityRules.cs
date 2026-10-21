using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>
/// Al quitar un maestro, los esclavos de esa partida dejan de mostrar la cantidad calculada.
/// Si queda otro maestro con el mismo código, los esclavos siguen ligados a ese.
/// </summary>
public static class CubicacionSlaveQuantityRules
{
    public static List<CubicacionRow> ClearWithoutMaster(
        IList<CubicacionRow> rows,
        string? itemId,
        IReadOnlyCollection<CubicacionRow> removedMasters)
    {
        var cleared = new List<CubicacionRow>();
        if (rows == null || removedMasters == null || removedMasters.Count == 0)
            return cleared;

        var removedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var master in removedMasters)
        {
            if (master == null || master.Esclavo != null || string.IsNullOrWhiteSpace(master.Codigo))
                continue;
            if (!string.IsNullOrWhiteSpace(master.Id))
                removedIds.Add(master.Id);
            codes.Add(master.Codigo.Trim());
        }

        if (codes.Count == 0)
            return cleared;

        foreach (var code in codes)
        {
            if (MasterRemains(rows, itemId, code, removedIds))
                continue;

            foreach (var row in rows)
            {
                if (!SameItem(row, itemId) || row.Esclavo == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(row.Id) && removedIds.Contains(row.Id))
                    continue;
                if (!CodeHelper.Match(row.Esclavo.MaestroCodigo, code))
                    continue;

                row.Cantidad = 0m;
                row.QtyExpressions = Array.Empty<string>();
                row.TotalExpression = string.Empty;
                cleared.Add(row);
            }
        }

        return cleared;
    }

    private static bool MasterRemains(IList<CubicacionRow> rows, string? itemId, string code, HashSet<string> removedIds)
    {
        foreach (var row in rows)
        {
            if (!SameItem(row, itemId) || row.Esclavo != null || string.IsNullOrWhiteSpace(row.Codigo))
                continue;
            if (!string.IsNullOrWhiteSpace(row.Id) && removedIds.Contains(row.Id))
                continue;
            if (CodeHelper.Match(row.Codigo, code))
                return true;
        }

        return false;
    }

    private static bool SameItem(CubicacionRow row, string? itemId)
        => string.Equals(row.ItemId ?? "", itemId ?? "", StringComparison.OrdinalIgnoreCase);
}
