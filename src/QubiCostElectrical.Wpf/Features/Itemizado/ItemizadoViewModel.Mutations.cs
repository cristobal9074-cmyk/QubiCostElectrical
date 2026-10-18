using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public sealed partial class ItemizadoViewModel
{
    private void AddPartida()
    {
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Agregar partida", session.ReadOnlyReason);
            return;
        }
        try
        {
            undo.PushUndoPoint(session, "Agregar partida");
            int index = IsSelectionVisible && SelectedRow != null
                ? session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id)
                : -1;
            var result = presenter.AddBelow(index, "Nueva partida", "GL");
            string? newId = session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
            var newItem = session.Itemizado.ElementAtOrDefault(result.SelectedIndex);
            if (newItem != null && session.ProjectInfo.CommercialModel != CommercialCostingModel.ApuDirectCostWithCommercialFactors)
            {
                session.ProjectInfo.CommercialPartidaRules ??= new List<CommercialPartidaRule>();
                session.ProjectInfo.CommercialPartidaRules.Add(new CommercialPartidaRule { PartidaId = newItem.PartidaId, UseProjectModel = false, Model = session.ProjectInfo.CommercialModel });
            }
            pendingSelectionId = newId;
            RefreshAfterStructureChange();
            StatusMessage = "Partida agregada.";
            var itemCode = newItem?.Item;
            var desc = newItem?.Descripcion ?? "Nueva partida";
            var label = string.IsNullOrWhiteSpace(itemCode) ? desc : $"{itemCode} · {desc}";
            ShellNotificationService.Instance.ShowSuccess("Partida agregada", $"Se agregó {label} al presupuesto.");
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
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Agregar título", session.ReadOnlyReason);
            return;
        }
        try
        {
            undo.PushUndoPoint(session, "Agregar título");
            int index = IsSelectionVisible && SelectedRow != null
                ? session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id)
                : -1;
            var result = presenter.AddTitle(index);
            string? newId = session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
            pendingSelectionId = newId;
            RefreshAfterStructureChange();
            StatusMessage = "Título agregado.";
            ShellNotificationService.Instance.ShowSuccess("Capítulo agregado", "Nuevo título / capítulo WBS agregado al presupuesto.");
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
        if (session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null)
            return;
        undo.PushUndoPoint(session, "Mover partida");
        int index = session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        var result = presenter.Move(index, -1);
        pendingSelectionId = session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
        RefreshAfterStructureChange();
    }

    private void MoveDown()
    {
        if (session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null)
            return;
        undo.PushUndoPoint(session, "Mover partida");
        int index = session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        var result = presenter.Move(index, 1);
        pendingSelectionId = session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
        RefreshAfterStructureChange();
    }

    private void DuplicateSelected()
    {
        if (session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null || SelectedRow.EsTitulo)
            return;
        undo.PushUndoPoint(session, "Duplicar partida");
        int index = session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        var result = presenter.DuplicateAt(index);
        pendingSelectionId = session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id;
        RefreshAfterStructureChange();
        StatusMessage = "Partida duplicada.";
        ShellNotificationService.Instance.ShowSuccess("Partida duplicada", "Partida y su estructura duplicadas con éxito.");
    }

    private void DeleteSelected()
    {
        if (session.IsReadOnly)
            return;
        if (!IsSelectionVisible || SelectedRow == null)
            return;
        int index = session.Itemizado.FindIndex(x => x.Id == SelectedRow.Id);
        if (index < 0)
            return;

        int affected = workspace.CountDeleteAffectedRows(presenter, index);
        string description = workspace.BuildDeleteDescription(session, index);
        if (!WpfDialogService.Confirm("Eliminar",
                presenter.BuildDeleteConfirmationMessage(description, affected), isWarning: true))
            return;

        string undoDesc = affected > 1
            ? $"Eliminación de {affected} filas"
            : (!string.IsNullOrWhiteSpace(SelectedRow.Item) ? $"Eliminación de partida {SelectedRow.Item}" : "Eliminación de partida");
        undo.PushUndoPoint(session, undoDesc);
        var result = presenter.DeleteAt(index);
        pendingSelectionId = result.SelectedIndex >= 0
            ? session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id
            : null;
        RefreshAfterStructureChange();
        StatusMessage = affected > 1 ? $"{affected:N0} filas eliminadas." : "Fila eliminada.";
        ShellNotificationService.Instance.ShowInfo("Itemizado", affected > 1 ? $"{affected:N0} filas eliminadas." : "Fila eliminada.");
    }

    private void ClearAll()
    {
        if (session.IsReadOnly)
            return;
        int count = presenter.CountRows();
        if (!WpfDialogService.Confirm("Vaciar itemizado",
                presenter.BuildClearConfirmationMessage(count), isWarning: true))
            return;

        undo.PushUndoPoint(session, "Vaciar itemizado");
        presenter.ClearAll();
        RefreshAfterStructureChange();
        StatusMessage = "Itemizado vaciado.";
    }

    private bool CanCubicar() =>
        ItemizadoTakeoffNavigationCoordinator.CanCubicar(session, presenter, allRows, IsSelectionVisible, SelectedRow, PassesCurrentFilter);

    private bool CanDuplicate() =>
        ItemizadoTakeoffNavigationCoordinator.CanDuplicate(session, IsSelectionVisible, SelectedRow, ShowEmptyState, allRows);

    private List<string> GetSelectedPartidaIds() =>
        ItemizadoTakeoffNavigationCoordinator.GetSelectedPartidaIds(presenter, allRows, PassesCurrentFilter);

    private Task CubicarSelectedAsync() =>
        ItemizadoTakeoffNavigationCoordinator.CubicarSelectedAsync(
            session, shell, presenter, allRows, IsSelectionVisible, SelectedRow, PassesCurrentFilter, msg => StatusMessage = msg);

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
        (ConfigureCommercialModelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadStandardTemplateCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
    }

    private void LoadStandardTemplate(string? templateId) =>
        ItemizadoTemplateCoordinator.LoadTemplate(session, templateId, undo, audit, () => RefreshPresentation(includeCosts: false), msg => StatusMessage = msg);

    private void ConfigureCommercialModel() =>
        ItemizadoCommercialRuleCoordinator.Configure(
            session, undo, audit, SelectedRow, () => RefreshPresentation(includeCosts: true), msg => StatusMessage = msg);
}
