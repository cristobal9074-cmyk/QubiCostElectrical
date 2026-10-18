using System.Threading.Tasks;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel
{
    public Task HandlePasteKeyAsync(DataGrid? grid = null, bool showPreview = false) =>
        pasteCoordinator.HandlePasteKeyAsync(grid, showPreview, SelectedRow, TryPasteAnchored, PasteFromClipboardAsync);

    internal bool TryPasteAnchored(DataGrid grid, string text) =>
        pasteCoordinator.TryPasteAnchored(
            grid,
            text,
            Rows,
            FilterMode,
            debouncedSearchText,
            SyncRowModels,
            UpdateSaveState,
            () => RefreshPresentation(includeCosts: true),
            (header, row) => OnCellEdited(header, row),
            msg => StatusMessage = msg);

    public Task ImportExcelAsync(string? explicitPath = null) =>
        pasteCoordinator.ImportExcelAsync(
            explicitPath,
            ConfirmReplace,
            id => pendingSelectionId = id,
            () => Reload(syncOnly: false),
            UpdateSaveState,
            msg => StatusMessage = msg);

    private Task PasteFromClipboardAsync() => PasteFromClipboardAsync(System.Windows.Clipboard.GetText());

    private Task PasteFromClipboardAsync(string text, bool showPreview = true) =>
        pasteCoordinator.PasteFromClipboardAsync(
            text,
            showPreview,
            ConfirmReplace,
            id => pendingSelectionId = id,
            () => Reload(syncOnly: false),
            UpdateSaveState,
            msg => StatusMessage = msg);
}
