using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class ItemizadoImportPreviewService
{
    public static List<ItemizadoImportPreviewRow> CreateValidatedPreviewRows(IEnumerable<ItemizadoRow> source)
    {
        var rows = source.Select(r => new ItemizadoImportPreviewRow(r)).ToList();
        var duplicates = ItemizadoImportValidation.FindDuplicatePartidaCodes(source);

        foreach (var row in rows.Where(r => !r.EsTitulo && !string.IsNullOrWhiteSpace(r.Item)))
        {
            if (duplicates.Contains(row.Item.Trim()))
                row.SetDuplicateWbs(true);
        }

        return rows;
    }

    public static bool HasValidationIssues(IEnumerable<ItemizadoImportPreviewRow> rows)
        => rows.Any(r => r.IsRejected || (!r.EsTitulo && !string.IsNullOrWhiteSpace(r.RejectionReason)));

    public static void RefreshDuplicateFlags(IList<ItemizadoImportPreviewRow> rows)
    {
        foreach (var row in rows)
            row.SetDuplicateWbs(false);

        var groups = rows
            .Where(r => !r.EsTitulo && !string.IsNullOrWhiteSpace(r.Item))
            .GroupBy(r => r.Item.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var group in groups)
        {
            foreach (var row in group)
                row.SetDuplicateWbs(true);
        }
    }

    public static IReadOnlyList<ItemizadoRow> AcceptRows(IEnumerable<ItemizadoImportPreviewRow> rows)
        => rows.Where(r => !r.IsRejected).Select(r => r.ToRow()).ToList();
}
