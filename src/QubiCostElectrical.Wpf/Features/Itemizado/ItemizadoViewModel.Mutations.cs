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

    private bool CanCubicar() => !session.IsReadOnly
        && (GetSelectedPartidaIds().Count > 0 || (IsSelectionVisible && SelectedRow != null && (!SelectedRow.EsTitulo || !presenter.HasChildPartidas(SelectedRow.Id))));

    private bool CanDuplicate() => !session.IsReadOnly && IsSelectionVisible && SelectedRow != null && !SelectedRow.EsTitulo && !ShowEmptyState && allRows.Count(r => r.IsSelected) <= 1;

    private List<string> GetSelectedPartidaIds()
        => allRows
            .Where(x => x.IsSelected && (!x.EsTitulo || !presenter.HasChildPartidas(x.Id)) && PassesCurrentFilter(x))
            .Select(x => x.Id)
            .ToList();

    private async Task CubicarSelectedAsync()
    {
        if (session.IsReadOnly)
            return;
        var ids = GetSelectedPartidaIds();
        if (ids.Count == 0 && IsSelectionVisible && SelectedRow != null && (!SelectedRow.EsTitulo || !presenter.HasChildPartidas(SelectedRow.Id)))
            ids.Add(SelectedRow.Id);

        if (ids.Count == 0)
        {
            StatusMessage = "Selecciona al menos una partida para cubicar.";
            return;
        }

        var valid = new List<(string Id, ItemizadoCubicacionNavigation Nav)>();
        string? firstError = null;
        foreach (var id in ids)
        {
            string? validation = presenter.ValidateCubicacionNavigation(id);
            if (!string.IsNullOrWhiteSpace(validation))
            {
                firstError ??= validation;
                continue;
            }

            var navigation = presenter.TryBuildCubicacionNavigation(id);
            if (navigation == null)
                continue;

            valid.Add((id, navigation));
        }

        if (SelectedRow != null && SelectedRow.EsTitulo != SelectedRow.Model.EsTitulo)
        {
            SelectedRow.IsTitleRow = SelectedRow.Model.EsTitulo;
            SelectedRow.UpdateModel(SelectedRow.Model);
        }

        if (valid.Count == 0)
        {
            WpfDialogService.Info("Cubicar", firstError ?? "Ninguna de las partidas seleccionadas se puede cubicar.");
            return;
        }

        if (valid.Count > 1 &&
            !WpfDialogService.Confirm(
                "Cubicar",
                $"¿Abrir cubicación de {valid.Count:N0} partidas en secuencia?\n\nSe abrirá la primera; el resto quedará en cola con aviso «Siguiente».",
                isWarning: false))
            return;

        session.SetPendingTakeoffQueue(
            valid.Count > 1 ? valid.Skip(1).Select(v => v.Id).ToList() : null);

        var first = valid[0].Nav;
        session.PendingTakeoffNavigation = new TakeoffNavigationContext(
            first.ItemId, first.ItemCode, first.Description);
        session.PendingItemizadoSelectionId = first.ItemId;
        shell.RequestNavigate(ShellRoute.Takeoff, session.PendingTakeoffNavigation);
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
        (ConfigureCommercialModelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadStandardTemplateCommand as RelayCommand<string>)?.RaiseCanExecuteChanged();
    }

    private void LoadStandardTemplate(string? templateId) =>
        ItemizadoTemplateCoordinator.LoadTemplate(session, templateId, undo, audit, () => RefreshPresentation(includeCosts: false), msg => StatusMessage = msg);

    private void ConfigureCommercialModel()
    {
        if (SelectedRow?.Model is not { } item || item.EsTitulo || session.IsReadOnly) return;
        var project = session.ProjectInfo;
        if (!project.AllowPartidaCommercialOverrides) { WpfDialogService.Info("Modelo comercial", "Las excepciones por partida están desactivadas en Proyecto → Comercial."); return; }
        project.CommercialPartidaRules ??= new List<CommercialPartidaRule>();
        var existing = project.CommercialPartidaRules.LastOrDefault(x => x.PartidaId == item.PartidaId);
        var draft = existing?.Clone() ?? new CommercialPartidaRule { PartidaId = item.PartidaId, UseProjectModel = true, Model = project.CommercialModel };
        var dialog = new CommercialPartidaRuleDialog(item, draft) { Owner = Application.Current?.MainWindow };
        if (dialog.ShowDialog() != true) return;
        undo.PushUndoPoint(session, "Modelo comercial de partida");
        var result = dialog.Result;
        if (existing == null) project.CommercialPartidaRules.Add(result); else { int index = project.CommercialPartidaRules.IndexOf(existing); project.CommercialPartidaRules[index] = result; }
        audit.RecordPartidaChange(item.Id, "Modelo comercial", existing?.Model.DisplayName() ?? "Heredado", result.UseProjectModel ? "Heredado" : result.Model.DisplayName());
        session.MarkDirty("commercial_partida_rule");
        RefreshPresentation(includeCosts: true); StatusMessage = result.UseProjectModel ? "La partida vuelve a heredar el modelo del proyecto." : $"Modelo comercial aplicado: {result.Model.DisplayName()}.";
    }
}
