using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QubiCostElectrical;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed record ItemizadoImportOutcome(
    bool Success,
    bool Cancelled,
    IReadOnlyList<ItemizadoRow> Rows,
    ItemizadoImportPlan? Plan,
    string? StatusMessage,
    string? ErrorTitle,
    string? ErrorMessage);

/// <summary>Orquesta importación Excel y pegado desde portapapeles.</summary>
public sealed class ItemizadoImportCoordinator
{
    private readonly ItemizadoPresenter presenter;

    public ItemizadoImportCoordinator(ItemizadoPresenter presenter)
    {
        this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    public async Task<ItemizadoImportOutcome> ImportExcelAsync(string filePath)
    {
        var (plan, error) = await Task.Run(() => presenter.TryBuildImportPlan(filePath));
        if (!string.IsNullOrWhiteSpace(error))
            return new ItemizadoImportOutcome(false, false, Array.Empty<ItemizadoRow>(), null, null, "Importar Excel", error);

        if (plan == null)
            return new ItemizadoImportOutcome(false, false, Array.Empty<ItemizadoRow>(), null,
                "El archivo no produjo ningún plan de importación.",
                "Importar Excel",
                "El archivo no produjo ningún plan de importación. Verifica que sea un Excel con filas de itemizado.");

        return ResolveImportPlan(plan);
    }

    public ItemizadoImportOutcome ResolvePaste(PegarItemizadoResult result, ItemizadoImportPlan? plan, bool showPreview)
    {
        var resolved = ItemizadoImportPasteResolver.Resolve(result, plan, showPreview);
        if (resolved.Action == ItemizadoImportResolveAction.NeedsUiPreview)
        {
            var importResult = ItemizadoPasteDialog.ShowPreview(result, plan);
            if (!importResult.Confirmed)
                return new ItemizadoImportOutcome(false, true, Array.Empty<ItemizadoRow>(), plan, "Pegado cancelado.", null, null);

            var summary = BuildRowsSummary("Pegado", importResult.ImportedCount, importResult.RejectedCount, importResult.WarningCount);
            return new ItemizadoImportOutcome(true, false, importResult.Rows, plan,
                BuildPasteStatus(summary, result.Warning), null, null);
        }

        return MapPasteResult(resolved, plan);
    }

    internal ItemizadoImportOutcome ResolveImportPlan(ItemizadoImportPlan plan)
    {
        var resolved = ItemizadoImportPasteResolver.ResolveImportPlan(plan);
        if (resolved.Action == ItemizadoImportResolveAction.NeedsUiPreview)
        {
            var importResult = ItemizadoImportDialog.ShowPreview(plan);
            if (!importResult.Confirmed)
                return new ItemizadoImportOutcome(false, true, Array.Empty<ItemizadoRow>(), plan, "Importación cancelada.", null, null);

            var summary = BuildRowsSummary("Importación", importResult.ImportedCount, importResult.RejectedCount, importResult.WarningCount);
            return new ItemizadoImportOutcome(true, false, importResult.Rows, plan,
                $"{summary}. Guarde el proyecto para persistir los cambios.",
                null, null);
        }

        return MapPasteResult(resolved, plan);
    }

    private static ItemizadoImportOutcome MapPasteResult(ItemizadoPasteResolveResult resolved, ItemizadoImportPlan? plan)
    {
        return resolved.Action switch
        {
            ItemizadoImportResolveAction.Cancelled => new ItemizadoImportOutcome(false, true, Array.Empty<ItemizadoRow>(), plan,
                resolved.StatusMessage, null, null),
            ItemizadoImportResolveAction.NeedsUiPreview => new ItemizadoImportOutcome(false, false, Array.Empty<ItemizadoRow>(), plan,
                null, null, null),
            ItemizadoImportResolveAction.Failed => new ItemizadoImportOutcome(false, false, Array.Empty<ItemizadoRow>(), plan,
                resolved.StatusMessage, resolved.ErrorTitle, resolved.ErrorMessage),
            ItemizadoImportResolveAction.AcceptDirectly => new ItemizadoImportOutcome(true, false, resolved.Rows, plan,
                resolved.StatusMessage, null, null),
            _ => new ItemizadoImportOutcome(false, false, Array.Empty<ItemizadoRow>(), plan, null, null, null)
        };
    }

    private static string BuildPasteStatus(string summary, string? warning)
    {
        string status = $"{summary}";
        if (!string.IsNullOrWhiteSpace(warning))
            status += $" · Advertencia: {warning}";
        status += ". Guarde el proyecto para persistir los cambios.";
        return status;
    }

    private static string BuildRowsSummary(string action, int imported, int rejected, int warnings)
        => ItemizadoImportSummary.FormatStatus(action, imported, rejected, warnings);
}
