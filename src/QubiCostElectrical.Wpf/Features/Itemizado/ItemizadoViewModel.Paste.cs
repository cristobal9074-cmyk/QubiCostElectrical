using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel
{
    public async Task HandlePasteKeyAsync(DataGrid? grid = null, bool showPreview = false)
    {
        if (!Clipboard.ContainsText())
            return;

        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (grid != null && TryPasteAnchored(grid, text))
            return;

        if (SelectedRow != null && !text.Contains('\n') && !text.Contains('\t'))
            return;

        await PasteFromClipboardAsync(text, showPreview);
    }

    internal bool TryPasteAnchored(DataGrid grid, string text)
    {
        return ItemizadoAnchoredPasteCoordinator.TryPaste(
            grid,
            text,
            session,
            undo,
            presenter,
            Rows,
            FilterMode,
            debouncedSearchText,
            SyncRowModels,
            UpdateSaveState,
            () => RefreshPresentation(includeCosts: true),
            (header, row) => OnCellEdited(header, row),
            msg => StatusMessage = msg);
    }

    public async Task ImportExcelAsync(string? explicitPath = null)
    {
        if (session.IsReadOnly)
        {
            StatusMessage = "Solo lectura: " + session.ReadOnlyReason;
            return;
        }
        var path = !string.IsNullOrWhiteSpace(explicitPath) ? explicitPath : WpfDialogService.PromptOpenExcel();
        if (path == null)
            return;

        StatusMessage = "Leyendo archivo…";
        try
        {
            var outcome = await importCoordinator.ImportExcelAsync(path);
            if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
            {
                WpfDialogService.Error(outcome.ErrorTitle ?? "Importar tabla", outcome.ErrorMessage);
                StatusMessage = outcome.StatusMessage ?? $"Error al leer: {outcome.ErrorMessage}";
                return;
            }

            if (outcome.Cancelled)
            {
                StatusMessage = outcome.StatusMessage ?? "Importación cancelada.";
                return;
            }

            if (!outcome.Success || outcome.Plan == null)
                return;

            var plan = outcome.Plan;
            if (plan.RequiresReplaceConfirmation &&
                !WpfDialogService.Confirm(plan.ActionLabel,
                    presenter.BuildReplaceConfirmationMessage(plan.CurrentRowCount, plan.ActionLabel), isWarning: true))
            {
                StatusMessage = "Importación cancelada.";
                return;
            }

            undo.PushUndoPoint(session, "Importar itemizado");
            presenter.CommitReplace(outcome.Rows);
            pendingSelectionId = outcome.Rows.FirstOrDefault(x => !x.EsTitulo)?.Id;
            Reload(syncOnly: false);
            StatusMessage = outcome.StatusMessage ?? "Importación completada.";
            UpdateSaveState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló la importación de Excel", ex);
            StatusMessage = OperationFailureMessages.ToUserMessage(ex);
            WpfDialogService.Error("Error al importar", StatusMessage);
        }
    }

    private async Task PasteFromClipboardAsync() => await PasteFromClipboardAsync(Clipboard.GetText());

    private async Task PasteFromClipboardAsync(string text, bool showPreview = true)
    {
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Pegar itemizado", session.ReadOnlyReason);
            return;
        }
        try
        {
            var (result, plan) = await Task.Run(() => presenter.TryBuildPastePlan(text));
            var outcome = importCoordinator.ResolvePaste(result, plan, showPreview);

            if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
            {
                WpfDialogService.Error(outcome.ErrorTitle ?? "Pegar itemizado", outcome.ErrorMessage);
                if (!string.IsNullOrWhiteSpace(outcome.StatusMessage))
                    StatusMessage = outcome.StatusMessage;
                return;
            }

            if (outcome.Cancelled)
            {
                StatusMessage = outcome.StatusMessage ?? "Pegado cancelado.";
                return;
            }

            if (!outcome.Success || outcome.Plan == null)
                return;

            if (outcome.Plan.RequiresReplaceConfirmation &&
                !WpfDialogService.Confirm(outcome.Plan.ActionLabel,
                    presenter.BuildReplaceConfirmationMessage(outcome.Plan.CurrentRowCount, outcome.Plan.ActionLabel), isWarning: true))
                return;

            undo.PushUndoPoint(session, "Pegar itemizado");
            presenter.CommitReplace(outcome.Rows);
            pendingSelectionId = outcome.Rows.FirstOrDefault(x => !x.EsTitulo)?.Id;
            Reload(syncOnly: false);
            StatusMessage = outcome.StatusMessage ?? "Pegado completado.";
            UpdateSaveState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló pegar itemizado desde el portapapeles", ex);
            WpfDialogService.Error("Pegar itemizado", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
