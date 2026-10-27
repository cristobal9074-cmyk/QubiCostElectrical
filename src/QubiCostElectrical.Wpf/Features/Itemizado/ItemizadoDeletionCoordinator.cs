using System;
using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed class ItemizadoDeletionCoordinator
{
    private readonly BudgetProjectSession _session;
    private readonly WpfUndoService _undo;
    private readonly ItemizadoPresenter _presenter;
    private readonly IItemizadoWorkspaceService _workspace;
    private readonly Action _refreshAfterStructureChange;
    private readonly Action<string?> _setPendingSelectionId;
    private readonly Action<string> _setStatusMessage;
    private readonly Func<ItemizadoRowVm, bool> _passesCurrentFilter;

    public ItemizadoDeletionCoordinator(
        BudgetProjectSession session,
        WpfUndoService undo,
        ItemizadoPresenter presenter,
        IItemizadoWorkspaceService workspace,
        Action refreshAfterStructureChange,
        Action<string?> setPendingSelectionId,
        Action<string> setStatusMessage,
        Func<ItemizadoRowVm, bool> passesCurrentFilter)
    {
        _session = session;
        _undo = undo;
        _presenter = presenter;
        _workspace = workspace;
        _refreshAfterStructureChange = refreshAfterStructureChange;
        _setPendingSelectionId = setPendingSelectionId;
        _setStatusMessage = setStatusMessage;
        _passesCurrentFilter = passesCurrentFilter;
    }

    public void DeleteRows(IReadOnlyList<ItemizadoRowVm>? rowsToDelete, ItemizadoRowVm? selectedRow)
    {
        if (_session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Eliminar", _session.ReadOnlyReason);
            return;
        }

        var targets = (rowsToDelete ?? (selectedRow != null ? new[] { selectedRow } : Array.Empty<ItemizadoRowVm>()))
            .Where(r => r != null && _passesCurrentFilter(r))
            .Distinct()
            .ToList();

        if (targets.Count == 0)
        {
            if (selectedRow != null && _passesCurrentFilter(selectedRow))
                targets.Add(selectedRow);
            else
                return;
        }

        string title = "Eliminar";
        string msg;
        if (targets.Count == 1)
        {
            var single = targets[0];
            int index = _session.Itemizado.FindIndex(x => x.Id == single.Id);
            if (index < 0) return;

            int affected = _workspace.CountDeleteAffectedRows(_presenter, index);
            string description = _workspace.BuildDeleteDescription(_session, index);
            msg = _presenter.BuildDeleteConfirmationMessage(description, affected);
        }
        else
        {
            int totalPartidas = targets.Count(t => !t.EsTitulo);
            int totalTitulos = targets.Count(t => t.EsTitulo);
            string countLabel = totalTitulos > 0
                ? $"{targets.Count:N0} elementos seleccionados ({totalPartidas} partidas, {totalTitulos} títulos)"
                : $"{targets.Count:N0} partidas seleccionadas";

            msg = $"¿Eliminar {countLabel}?\n\nSe eliminarán también las cubicaciones y APU asociados.";
        }

        if (!WpfDialogService.Confirm(title, msg, isWarning: true))
            return;

        string undoDesc = targets.Count > 1
            ? $"Eliminación de {targets.Count} filas"
            : (!string.IsNullOrWhiteSpace(targets[0].Item) ? $"Eliminación de partida {targets[0].Item}" : "Eliminación de partida");

        _undo.PushUndoPoint(_session, undoDesc);

        var idsToDelete = targets.Select(r => r.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        int deletedCount = 0;
        int lastDeletedIndex = -1;

        while (true)
        {
            int lastIdx = _session.Itemizado.FindLastIndex(x => idsToDelete.Contains(x.Id));
            if (lastIdx < 0)
                break;

            lastDeletedIndex = lastIdx;
            _presenter.DeleteAt(lastIdx);
            deletedCount++;
        }

        if (lastDeletedIndex >= 0 && _session.Itemizado.Count > 0)
        {
            int clamp = Math.Clamp(lastDeletedIndex, 0, _session.Itemizado.Count - 1);
            _setPendingSelectionId(_session.Itemizado[clamp].Id);
        }
        else
        {
            _setPendingSelectionId(null);
        }

        _refreshAfterStructureChange();
        _setStatusMessage(deletedCount > 1 ? $"{deletedCount:N0} filas eliminadas." : "Fila eliminada.");
        ShellNotificationService.Instance.ShowInfo("Itemizado", deletedCount > 1 ? $"{deletedCount:N0} filas eliminadas." : "Fila eliminada.");
    }
}
