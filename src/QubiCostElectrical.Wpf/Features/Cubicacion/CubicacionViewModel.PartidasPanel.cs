using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed partial class CubicacionViewModel
{
    public ICommand AddPartidaCommand { get; private set; } = null!;
    public ICommand DeletePartidaCommand { get; private set; } = null!;
    public ICommand NavigateToItemizadoCommand { get; private set; } = null!;
    public ICommand ClearPartidaFilterCommand { get; private set; } = null!;

    private void InitializePartidaPanelCommands()
    {
        AddPartidaCommand = new RelayCommand(AddPartidaBelowSelected, () => !session.IsReadOnly);
        DeletePartidaCommand = new RelayCommand(DeleteSelectedPartida, () =>
            !session.IsReadOnly && SelectedPartida?.EsPartida == true);
        NavigateToItemizadoCommand = new RelayCommand(() => shell.RequestNavigate(ShellRoute.Itemized));
        ClearPartidaFilterCommand = new RelayCommand(() => PartidaFilter = string.Empty, () => HasPartidaFilter);
    }

    /// <summary>Ctrl++ en el panel PARTIDAS: agrega una partida bajo la selección.</summary>
    private void AddPartidaBelowSelected()
    {
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Agregar partida", session.ReadOnlyReason);
            return;
        }

        try
        {
            undo.PushUndoPoint(session, "Agregar partida");
            int index = SelectedPartida != null
                ? session.Itemizado.FindIndex(x =>
                    string.Equals(x.Id, SelectedPartida.Id, StringComparison.OrdinalIgnoreCase))
                : -1;

            var result = itemizadoPresenter.AddBelow(index, "Nueva partida", "GL");
            var newItem = session.Itemizado.ElementAtOrDefault(result.SelectedIndex);
            if (newItem != null
                && session.ProjectInfo.CommercialModel != CommercialCostingModel.ApuDirectCostWithCommercialFactors)
            {
                session.ProjectInfo.CommercialPartidaRules ??= new List<CommercialPartidaRule>();
                session.ProjectInfo.CommercialPartidaRules.Add(new CommercialPartidaRule
                {
                    PartidaId = newItem.PartidaId,
                    UseProjectModel = false,
                    Model = session.ProjectInfo.CommercialModel
                });
            }

            string? newId = newItem?.Id;
            // MarkDirty ya recargó Partidas vía Session.Changed.
            if (!string.IsNullOrEmpty(newId))
            {
                var created = Partidas.FirstOrDefault(p =>
                    string.Equals(p.Id, newId, StringComparison.OrdinalIgnoreCase) && p.EsPartida);
                if (created == null)
                {
                    LoadPartidas();
                    created = Partidas.FirstOrDefault(p =>
                        string.Equals(p.Id, newId, StringComparison.OrdinalIgnoreCase) && p.EsPartida);
                }
                if (created != null)
                    SelectPartida(created);
            }

            SaveStatusText = "Partida agregada (Ctrl++).";
        }
        catch (Exception ex)
        {
            AppLog.Error("Cubicacion", "Falló agregar partida desde el panel", ex);
            WpfDialogService.Error("Agregar partida", OperationFailureMessages.ToUserMessage(ex));
        }
    }

    /// <summary>Ctrl+- en el panel PARTIDAS: elimina la partida activa (y su cubicación/APU).</summary>
    private void DeleteSelectedPartida()
    {
        if (session.IsReadOnly || SelectedPartida?.EsPartida != true)
            return;

        int index = session.Itemizado.FindIndex(x =>
            string.Equals(x.Id, SelectedPartida.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return;

        int affected = itemizadoPresenter.CountAffectedRows(index);
        var row = session.Itemizado[index];
        string description = string.IsNullOrWhiteSpace(row.Item)
            ? (row.Descripcion ?? "Partida")
            : $"{row.Item} — {row.Descripcion}";

        if (!WpfDialogService.Confirm(
                "Eliminar partida",
                itemizadoPresenter.BuildDeleteConfirmationMessage(description, affected),
                isWarning: true))
            return;

        try
        {
            undo.PushUndoPoint(session, affected > 1 ? "Eliminar filas" : "Eliminar partida");
            var result = itemizadoPresenter.DeleteAt(index);
            string? nextId = result.SelectedIndex >= 0
                ? session.Itemizado.ElementAtOrDefault(result.SelectedIndex)?.Id
                : null;

            if (!string.IsNullOrEmpty(nextId))
            {
                var next = Partidas.FirstOrDefault(p =>
                    string.Equals(p.Id, nextId, StringComparison.OrdinalIgnoreCase) && p.EsPartida);
                if (next != null)
                    SelectPartida(next);
                else
                    SelectFirstPartida();
            }
            else
            {
                SelectFirstPartida();
            }

            SaveStatusText = affected > 1
                ? $"{affected:N0} filas eliminadas (Ctrl+-)."
                : "Partida eliminada (Ctrl+-).";
        }
        catch (Exception ex)
        {
            AppLog.Error("Cubicacion", "Falló eliminar partida desde el panel", ex);
            WpfDialogService.Error("Eliminar partida", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
