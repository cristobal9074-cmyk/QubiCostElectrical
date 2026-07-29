using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed record ItemizadoImportResult(
    bool Confirmed,
    IReadOnlyList<ItemizadoRow> Rows,
    int ImportedCount,
    int RejectedCount,
    int WarningCount);

public static class ItemizadoImportDialog
{
    public static ItemizadoImportResult ShowPreview(ItemizadoImportPlan plan)
    {
        var previews = ItemizadoImportPreviewService.CreateValidatedPreviewRows(plan.Rows);
        var rows = previews.Select(p => new ItemizadoImportRowVm(p)).ToList();
        string summary = $"{plan.Partidas:N0} partidas · {plan.Titulos:N0} títulos · {rows.Count:N0} filas detectadas";
        if (ItemizadoImportValidation.HasDuplicatePartidaCodes(plan.Rows))
            summary += " · códigos WBS duplicados detectados";

        return Show(
            "Importar itemizado",
            summary,
            "Revise el itemizado detectado y confirme la importación.",
            null,
            rows);
    }

    public static ItemizadoImportResult ShowPreview(
        string title,
        string summary,
        string note,
        string? warning,
        List<ItemizadoImportRowVm> rows)
    {
        return Show(title, summary, note, warning, rows);
    }

    public static ItemizadoImportResult Show(
        string title,
        string summary,
        string note,
        string? warning,
        List<ItemizadoImportRowVm> rows)
    {
        var dialog = new ItemizadoImportPreviewDialog(title, summary, note, warning, rows);
        if (Application.Current?.MainWindow != null)
            dialog.Owner = Application.Current.MainWindow;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        bool accepted = dialog.ShowDialog() == true;
        var previewRows = rows
            .Select(r => r.Preview)
            .Where(p => p != null)
            .Cast<ItemizadoImportPreviewRow>()
            .ToList();
        var (imported, rejected, warnings) = ItemizadoImportSummary.CountPreviewRows(previewRows);

        if (!accepted)
            return new ItemizadoImportResult(false, [], 0, rejected, warnings);

        return new ItemizadoImportResult(
            true,
            dialog.AcceptedRows,
            imported,
            rejected,
            warnings);
    }
}
