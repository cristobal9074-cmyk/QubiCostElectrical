using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum ItemizadoImportResolveAction
{
    Failed,
    Cancelled,
    NeedsUiPreview,
    AcceptDirectly
}

public sealed record ItemizadoPasteResolveResult(
    ItemizadoImportResolveAction Action,
    IReadOnlyList<ItemizadoRow> Rows,
    string? StatusMessage,
    string? ErrorTitle,
    string? ErrorMessage);

/// <summary>Resolución de pegado sin UI — el coordinador WPF muestra diálogo solo si Action = NeedsUiPreview.</summary>
public static class ItemizadoImportPasteResolver
{
    public static ItemizadoPasteResolveResult Resolve(PegarItemizadoResult result, ItemizadoImportPlan? plan, bool showPreview)
    {
        if (result.Rows.Count == 0)
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.Failed, Array.Empty<ItemizadoRow>(),
                "No se encontraron filas válidas en el portapapeles.", null, null);

        if (plan == null)
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.Failed, Array.Empty<ItemizadoRow>(),
                null, null, null);

        var previewRows = ItemizadoImportPreviewService.CreateValidatedPreviewRows(result.Rows);
        bool hasIssues = ItemizadoImportPreviewService.HasValidationIssues(previewRows);
        bool hasDuplicates = ItemizadoImportValidation.HasDuplicatePartidaCodes(result.Rows);

        if (showPreview && ItemizadoImportPreviewPolicy.ShouldShowPastePreview(result, hasIssues, hasDuplicates))
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.NeedsUiPreview, Array.Empty<ItemizadoRow>(), null, null, null);

        if (!string.IsNullOrWhiteSpace(result.Warning))
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.Failed, Array.Empty<ItemizadoRow>(),
                null, "Pegar itemizado", result.Warning);

        if (previewRows.Count(r => !r.EsTitulo && !r.IsRejected) == 0)
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.Failed, Array.Empty<ItemizadoRow>(),
                "Pegado cancelado: sin partidas válidas.", "Pegar itemizado", "No hay partidas válidas para pegar.");

        return new ItemizadoPasteResolveResult(
            ItemizadoImportResolveAction.AcceptDirectly,
            ItemizadoImportPreviewService.AcceptRows(previewRows),
            BuildPasteStatus(plan, result.Warning),
            null,
            null);
    }

    public static ItemizadoPasteResolveResult ResolveImportPlan(ItemizadoImportPlan plan)
    {
        var previewRows = ItemizadoImportPreviewService.CreateValidatedPreviewRows(plan.Rows);
        bool hasIssues = ItemizadoImportPreviewService.HasValidationIssues(previewRows);
        bool hasDuplicates = ItemizadoImportValidation.HasDuplicatePartidaCodes(plan.Rows);

        if (ItemizadoImportPreviewPolicy.ShouldShowImportPreview(plan, hasIssues, hasDuplicates))
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.NeedsUiPreview, Array.Empty<ItemizadoRow>(), null, null, null);

        if (previewRows.Count(r => !r.EsTitulo && !r.IsRejected) == 0)
            return new ItemizadoPasteResolveResult(ItemizadoImportResolveAction.Failed, Array.Empty<ItemizadoRow>(),
                "Importación cancelada: sin partidas válidas.", "Importar tabla", "No hay partidas válidas para importar.");

        return new ItemizadoPasteResolveResult(
            ItemizadoImportResolveAction.AcceptDirectly,
            ItemizadoImportPreviewService.AcceptRows(previewRows),
            null,
            null,
            null);
    }

    private static string BuildPasteStatus(ItemizadoImportPlan plan, string? warning)
    {
        string status = $"Pegado completado: {plan.Partidas:N0} partidas";
        if (!string.IsNullOrWhiteSpace(warning))
            status += $" · Advertencia: {warning}";
        status += ". Guarde el proyecto para persistir los cambios.";
        return status;
    }
}
