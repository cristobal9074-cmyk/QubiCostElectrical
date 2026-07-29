using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class ItemizadoImportValidation
{
    public const int MaxImportRows = 10_000;

    public static string? ValidateRowCount(int rowCount) =>
        rowCount > MaxImportRows
            ? $"El itemizado tiene {rowCount:N0} filas; el máximo permitido es {MaxImportRows:N0}. Divida el archivo o reduzca el alcance."
            : null;

    public static HashSet<string> FindDuplicatePartidaCodes(IEnumerable<ItemizadoRow> rows)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Where(r => !r.EsTitulo && !string.IsNullOrWhiteSpace(r.Item)))
        {
            string code = row.Item.Trim();
            if (!seen.Add(code))
                duplicates.Add(code);
        }

        return duplicates;
    }

    public static bool HasDuplicatePartidaCodes(IEnumerable<ItemizadoRow> rows)
        => FindDuplicatePartidaCodes(rows).Count > 0;
}
