using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed record ItemizadoItemEditResult(bool Success, string? ErrorMessage);

/// <summary>Validación y aplicación de ediciones en grilla (códigos visibles, duplicados, jerarquía).</summary>
public static class ItemizadoGridValidation
{
    public static ItemizadoItemEditResult TryApplyVisibleItemEdit(
        ItemizadoRow row,
        string proposedItem,
        IReadOnlyList<ItemizadoRow> allRows)
    {
        if (row == null)
            throw new ArgumentNullException(nameof(row));

        proposedItem = (proposedItem ?? "").Trim();
        if (string.IsNullOrWhiteSpace(proposedItem))
            return new ItemizadoItemEditResult(false, "El código ítem es obligatorio.");

        return row.EsTitulo
            ? TryApplyTitleItemEdit(row, proposedItem)
            : TryApplyPartidaItemEdit(row, proposedItem, allRows);
    }

    public static string? ValidateItemCode(ItemizadoRow row, string proposedItem, IReadOnlyList<ItemizadoRow> allRows)
    {
        if (string.IsNullOrWhiteSpace(proposedItem))
            return "El código ítem es obligatorio.";

        return row.EsTitulo
            ? ValidateTitleItemCode(row, proposedItem)
            : ValidateDuplicatePartidaCode(row, proposedItem, allRows);
    }

    public static string? ValidateDuplicatePartidaCode(ItemizadoRow row, IReadOnlyList<ItemizadoRow> allRows)
        => ValidateDuplicatePartidaCode(row, row.Item ?? "", allRows);

    private static string? ValidateDuplicatePartidaCode(ItemizadoRow row, string proposedItem, IReadOnlyList<ItemizadoRow> allRows)
    {
        if (row.EsTitulo || string.IsNullOrWhiteSpace(proposedItem))
            return null;

        foreach (var other in allRows)
        {
            if (ReferenceEquals(other, row) || other.EsTitulo)
                continue;

            if (string.Equals(other.Item?.Trim(), proposedItem, StringComparison.OrdinalIgnoreCase))
                return $"El código «{proposedItem}» ya existe en otra partida.";
        }

        return null;
    }

    private static ItemizadoItemEditResult TryApplyTitleItemEdit(ItemizadoRow row, string proposedItem)
    {
        string? validation = ValidateTitleItemCode(row, proposedItem);
        if (validation != null)
            return new ItemizadoItemEditResult(false, validation);

        row.Item = proposedItem;
        row.WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(proposedItem);
        row.ParentId = "";
        return new ItemizadoItemEditResult(true, null);
    }

    private static string? ValidateTitleItemCode(ItemizadoRow row, string proposedItem)
    {
        string currentTitleId = ItemizadoHierarchyRules.NormalizeTitleId(row.Id);
        string proposedTitleId = ItemizadoImportParser.BuildImportedTitleId(
            proposedItem,
            ParseChapterNumber(currentTitleId));

        if (!string.Equals(proposedTitleId, currentTitleId, StringComparison.OrdinalIgnoreCase))
        {
            return "No puede cambiar el número de capítulo editando el ítem. Agregue un título nuevo o reordénelo con Subir/Bajar.";
        }

        return null;
    }

    private static ItemizadoItemEditResult TryApplyPartidaItemEdit(
        ItemizadoRow row,
        string proposedItem,
        IReadOnlyList<ItemizadoRow> allRows)
    {
        string? duplicate = ValidateDuplicatePartidaCode(row, proposedItem, allRows);
        if (duplicate != null)
            return new ItemizadoItemEditResult(false, duplicate);

        row.Item = proposedItem;
        row.WBSCode = ItemizadoHierarchyRules.NormalizeIdFromItem(proposedItem);
        row.ParentId = ResolveParentTitleId(row.WBSCode, allRows);
        return new ItemizadoItemEditResult(true, null);
    }

    private static string ResolveParentTitleId(string wbsCode, IReadOnlyList<ItemizadoRow> allRows)
    {
        var parts = (wbsCode ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return "";

        string chapterId = $"{parts[0]}.0";
        var title = allRows.FirstOrDefault(r =>
            r.EsTitulo && string.Equals(r.Id, chapterId, StringComparison.OrdinalIgnoreCase));

        return title?.Id ?? chapterId;
    }

    private static int ParseChapterNumber(string titleId)
    {
        var parts = (titleId ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 && int.TryParse(parts[0], out var n) ? n : 1;
    }
}
