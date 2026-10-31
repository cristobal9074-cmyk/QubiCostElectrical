using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed class ItemizadoClipboardPasteCoordinator
{
    private readonly BudgetProjectSession session;
    private readonly WpfUndoService undo;
    private readonly ItemizadoPresenter presenter;
    private readonly ItemizadoImportCoordinator importCoordinator;

    public ItemizadoClipboardPasteCoordinator(
        BudgetProjectSession session,
        WpfUndoService undo,
        ItemizadoPresenter presenter,
        ItemizadoImportCoordinator importCoordinator)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.undo = undo ?? throw new ArgumentNullException(nameof(undo));
        this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        this.importCoordinator = importCoordinator ?? throw new ArgumentNullException(nameof(importCoordinator));
    }

    public async Task HandlePasteKeyAsync(
        DataGrid? grid,
        bool showPreview,
        ItemizadoRowVm? selectedRow,
        Func<DataGrid, string, bool> tryPasteAnchored,
        Func<string, bool, Task> pasteFromClipboardAsync)
    {
        if (!Clipboard.ContainsText())
            return;

        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (grid != null && tryPasteAnchored(grid, text))
            return;

        if (selectedRow != null && !text.Contains('\n') && !text.Contains('\t'))
            return;

        await pasteFromClipboardAsync(text, showPreview);
    }

    public bool TryPasteAnchored(
        DataGrid grid,
        string text,
        ObservableCollection<ItemizadoRowVm> rows,
        ItemizadoFilterMode filterMode,
        string debouncedSearchText,
        Action syncRowModels,
        Action updateSaveState,
        Action refreshPresentationWithCosts,
        Action<string?, ItemizadoRowVm?> onCellEdited,
        Action<string> setStatusMessage)
    {
        return ItemizadoAnchoredPasteCoordinator.TryPaste(
            grid,
            text,
            session,
            undo,
            presenter,
            rows,
            filterMode,
            debouncedSearchText,
            syncRowModels,
            updateSaveState,
            refreshPresentationWithCosts,
            (header, row) => onCellEdited(header, row),
            setStatusMessage);
    }

    public async Task ImportExcelAsync(
        string? explicitPath,
        Func<string, string, bool> confirmReplace,
        Action<string?> setPendingSelectionId,
        Action reload,
        Action updateSaveState,
        Action<string> setStatusMessage)
    {
        if (session.IsReadOnly)
        {
            setStatusMessage("Solo lectura: " + session.ReadOnlyReason);
            return;
        }
        var path = !string.IsNullOrWhiteSpace(explicitPath) ? explicitPath : WpfDialogService.PromptOpenExcel();
        if (path == null)
            return;

        string? sheetName = null;
        try
        {
            var sheets = await Task.Run(() => presenter.InspectWorksheets(path));
            if (sheets.Count > 1)
            {
                var chosen = SelectExcelSheetDialog.Prompt(sheets, path);
                if (chosen == null)
                {
                    setStatusMessage("Importación cancelada.");
                    return;
                }
                sheetName = chosen.Name;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning("Itemizado", "No se pudo inspeccionar hojas del Excel de forma anticipada", ex);
        }

        setStatusMessage("Leyendo archivo…");
        try
        {
            var outcome = await importCoordinator.ImportExcelAsync(path, sheetName);
            if (!string.IsNullOrWhiteSpace(outcome.ErrorMessage))
            {
                WpfDialogService.Error(outcome.ErrorTitle ?? "Importar tabla", outcome.ErrorMessage);
                setStatusMessage(outcome.StatusMessage ?? $"Error al leer: {outcome.ErrorMessage}");
                return;
            }

            if (outcome.Cancelled)
            {
                setStatusMessage(outcome.StatusMessage ?? "Importación cancelada.");
                return;
            }

            if (!outcome.Success || outcome.Plan == null)
                return;

            var plan = outcome.Plan;
            if (plan.RequiresReplaceConfirmation &&
                !confirmReplace(plan.ActionLabel,
                    presenter.BuildReplaceConfirmationMessage(plan.CurrentRowCount, plan.ActionLabel)))
            {
                setStatusMessage("Importación cancelada.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(session.CurrentProjectPath) && System.IO.File.Exists(session.CurrentProjectPath))
            {
                _ = SafeSaveService.CreateBackupAsync(session.CurrentProjectPath);
            }

            undo.PushUndoPoint(session, "Importar itemizado");
            presenter.CommitReplace(outcome.Rows);
            setPendingSelectionId(outcome.Rows.FirstOrDefault(x => !x.EsTitulo)?.Id);
            reload();
            setStatusMessage(outcome.StatusMessage ?? "Importación completada.");
            updateSaveState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló la importación de Excel", ex);
            string userMsg = OperationFailureMessages.ToUserMessage(ex);
            setStatusMessage(userMsg);
            WpfDialogService.Error("Error al importar", userMsg);
        }
    }

    public async Task PasteFromClipboardAsync(
        string text,
        bool showPreview,
        Func<string, string, bool> confirmReplace,
        Action<string?> setPendingSelectionId,
        Action reload,
        Action updateSaveState,
        Action<string> setStatusMessage)
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
                    setStatusMessage(outcome.StatusMessage);
                return;
            }

            if (outcome.Cancelled)
            {
                setStatusMessage(outcome.StatusMessage ?? "Pegado cancelado.");
                return;
            }

            if (!outcome.Success || outcome.Plan == null)
                return;

            if (outcome.Plan.RequiresReplaceConfirmation &&
                !confirmReplace(outcome.Plan.ActionLabel,
                    presenter.BuildReplaceConfirmationMessage(outcome.Plan.CurrentRowCount, outcome.Plan.ActionLabel)))
                return;

            if (!string.IsNullOrWhiteSpace(session.CurrentProjectPath) && System.IO.File.Exists(session.CurrentProjectPath))
            {
                _ = SafeSaveService.CreateBackupAsync(session.CurrentProjectPath);
            }

            undo.PushUndoPoint(session, "Pegar itemizado");
            presenter.CommitReplace(outcome.Rows);
            setPendingSelectionId(outcome.Rows.FirstOrDefault(x => !x.EsTitulo)?.Id);
            reload();
            setStatusMessage(outcome.StatusMessage ?? "Pegado completado.");
            updateSaveState();
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló pegar itemizado desde el portapapeles", ex);
            WpfDialogService.Error("Pegar itemizado", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
