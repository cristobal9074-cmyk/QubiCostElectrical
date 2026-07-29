using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel
{
    private void AddPartida()
    {
        if (host.Session.IsReadOnly)
        {
            var reason = host.Session.ReadOnlyReason;
            StatusMessage = string.IsNullOrWhiteSpace(reason)
                ? "Proyecto en modo solo lectura."
                : $"Solo lectura: {reason}";
            WpfDialogService.Warning("Agregar partida", StatusMessage);
            return;
        }
        try
        {
            host.UndoService.PushUndoPoint(host.Session, "Agregar partida");
            int index = IsSelectionVisible && SelectedRow != null
                ? host.Session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id)
                : -1;
            var result = presenter.AddBelow(index, "Nueva partida", "GL");
            string? newId = host.Session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
            pendingSelectionId = newId;
            RefreshAfterStructureChange();
            StatusMessage = "Partida agregada.";
            if (!string.IsNullOrEmpty(newId))
                RowAdded?.Invoke(newId);
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló agregar partida", ex);
            WpfDialogService.Error("Agregar partida", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void AddTitle()
    {
        if (host.Session.IsReadOnly)
        {
            var reason = host.Session.ReadOnlyReason;
            StatusMessage = string.IsNullOrWhiteSpace(reason)
                ? "Proyecto en modo solo lectura."
                : $"Solo lectura: {reason}";
            WpfDialogService.Warning("Agregar título", StatusMessage);
            return;
        }
        try
        {
            host.UndoService.PushUndoPoint(host.Session, "Agregar título");
            int index = IsSelectionVisible && SelectedRow != null
                ? host.Session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id)
                : -1;
            var result = presenter.AddTitle(index);
            string? newId = host.Session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
            pendingSelectionId = newId;
            RefreshAfterStructureChange();
            StatusMessage = "Título agregado.";
            if (!string.IsNullOrEmpty(newId))
                RowAdded?.Invoke(newId);
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló agregar título", ex);
            WpfDialogService.Error("Agregar título", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    private void MoveUp()
    {
        if (host.Session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null)
            return;
        host.UndoService.PushUndoPoint(host.Session, "Mover partida");
        int index = host.Session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        var result = presenter.Move(index, -1);
        pendingSelectionId = host.Session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
        RefreshAfterStructureChange();
    }

    private void MoveDown()
    {
        if (host.Session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null)
            return;
        host.UndoService.PushUndoPoint(host.Session, "Mover partida");
        int index = host.Session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        var result = presenter.Move(index, 1);
        pendingSelectionId = host.Session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
        RefreshAfterStructureChange();
    }

    private void DuplicateSelected()
    {
        if (host.Session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null || SelectedRow.EsTitulo)
            return;
        host.UndoService.PushUndoPoint(host.Session, "Duplicar partida");
        int index = host.Session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        var result = presenter.DuplicateAt(index);
        pendingSelectionId = host.Session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
        RefreshAfterStructureChange();
        StatusMessage = "Partida duplicada.";
    }

    private void DeleteSelected()
    {
        if (host.Session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null)
            return;
        int index = host.Session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        if (index < 0)
            return;

        int affected = workspace.CountDeleteAffectedRows(presenter, index);
        string description = workspace.BuildDeleteDescription(host.Session, index);
        if (!WpfDialogService.Confirm("Eliminar",
                presenter.BuildDeleteConfirmationMessage(description, affected), isWarning: true))
            return;

        host.UndoService.PushUndoPoint(host.Session, affected > 1 ? "Eliminar filas" : "Eliminar partida");
        var result = presenter.DeleteAt(index);
        pendingSelectionId = result.SelectedIndex >= 0
            ? host.Session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id
            : null;
        RefreshAfterStructureChange();
        StatusMessage = affected > 1 ? $"{affected:N0} filas eliminadas." : "Fila eliminada.";
    }

    private void ClearAll()
    {
        if (host.Session.IsReadOnly)
            return;
        int count = presenter.CountRows();
        if (!WpfDialogService.Confirm("Vaciar itemizado",
                presenter.BuildClearConfirmationMessage(count), isWarning: true))
            return;

        host.UndoService.PushUndoPoint(host.Session, "Vaciar itemizado");
        presenter.ClearAll();
        RefreshAfterStructureChange();
        StatusMessage = "Itemizado vaciado.";
    }

    private bool CanCubicar() => !host.Session.IsReadOnly
        && (GetSelectedPartidaIds().Count > 0 || (IsSelectionVisible && SelectedRow != null && !SelectedRow.EsTitulo));

    private bool CanDuplicate() => !host.Session.IsReadOnly && IsSelectionVisible && SelectedRow != null && !SelectedRow.EsTitulo && !ShowEmptyState;

    private List<string> GetSelectedPartidaIds()
        => allRows
            .Where(x => x.IsSelected && !x.EsTitulo && PassesCurrentFilter(x))
            .Select(x => x.Id)
            .ToList();

    private async Task CubicarSelectedAsync()
    {
        if (host.Session.IsReadOnly)
            return;
        var ids = GetSelectedPartidaIds();
        if (ids.Count == 0 && IsSelectionVisible && SelectedRow != null && !SelectedRow.EsTitulo)
            ids.Add(SelectedRow.Id);

        if (ids.Count == 0)
        {
            StatusMessage = "Selecciona al menos una partida para cubicar.";
            return;
        }

        if (ids.Count > 1)
            StatusMessage = $"Se abrirá cubicación de la primera de {ids.Count:N0} partidas marcadas.";

        string targetId = ids[0];
        string? validation = presenter.ValidateCubicacionNavigation(targetId);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            WpfDialogService.Info("Cubicar", validation);
            return;
        }

        var navigation = presenter.TryBuildCubicacionNavigation(targetId);
        if (navigation == null)
            return;

        host.Session.PendingTakeoffNavigation = new TakeoffNavigationContext(
            navigation.ItemId, navigation.ItemCode, navigation.Description);
        host.Session.PendingItemizadoSelectionId = navigation.ItemId;
        shell.RequestNavigate(ShellRoute.Takeoff, host.Session.PendingTakeoffNavigation);
    }

    private void RaiseCommandStates()
    {
        (ImportExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PasteExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddPartidaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddTitleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CubicarCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CubicarButtonLabel));
        (MoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DuplicateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
