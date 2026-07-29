using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public static class ItemizadoPasteDialog
{
    public static ItemizadoImportResult ShowPreview(PegarItemizadoResult result, ItemizadoImportPlan? plan)
    {
        if (plan == null)
            return new ItemizadoImportResult(false, new List<ItemizadoRow>(), 0, 0, 0);

        int partidas = result.Rows.Count(r => !r.EsTitulo);
        int titulos = result.Rows.Count(r => r.EsTitulo);
        string summary = titulos > 0
            ? $"{partidas:N0} partidas y {titulos:N0} títulos detectados"
            : $"{partidas:N0} partidas detectadas";
        if (ItemizadoImportValidation.HasDuplicatePartidaCodes(result.Rows))
            summary += " · códigos WBS duplicados detectados";

        string note = string.IsNullOrWhiteSpace(result.ColumnMap)
            ? "Revise las filas antes de pegar en el itemizado."
            : result.ColumnMap;

        string? warning = string.IsNullOrWhiteSpace(result.Warning) ? null : $"Advertencia: {result.Warning}";

        var previews = ItemizadoImportPreviewService.CreateValidatedPreviewRows(result.Rows);
        var rows = previews.Select(p => new ItemizadoImportRowVm(p)).ToList();
        return ItemizadoImportDialog.Show(
            "Vista previa de pegado",
            summary,
            note,
            warning,
            rows);
    }
}
