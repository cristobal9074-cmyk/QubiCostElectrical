using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public static class ItemizadoCommercialRuleCoordinator
{
    public static void Configure(
        BudgetProjectSession session,
        WpfUndoService undo,
        ProjectAuditRecorder audit,
        ItemizadoRowVm? selectedRow,
        Action refreshPresentation,
        Action<string> setStatusMessage)
    {
        if (selectedRow?.Model is not { } item || item.EsTitulo || session.IsReadOnly)
            return;

        var project = session.ProjectInfo;
        if (!project.AllowPartidaCommercialOverrides)
        {
            WpfDialogService.Info("Modelo comercial", "Las excepciones por partida están desactivadas en Proyecto → Comercial.");
            return;
        }

        project.CommercialPartidaRules ??= new List<CommercialPartidaRule>();
        var existing = project.CommercialPartidaRules.LastOrDefault(x => x.PartidaId == item.PartidaId);
        var draft = existing?.Clone() ?? new CommercialPartidaRule
        {
            PartidaId = item.PartidaId,
            UseProjectModel = true,
            Model = project.CommercialModel
        };

        var dialog = new CommercialPartidaRuleDialog(item, draft) { Owner = Application.Current?.MainWindow };
        if (dialog.ShowDialog() != true)
            return;

        undo.PushUndoPoint(session, "Modelo comercial de partida");
        var result = dialog.Result;
        if (existing == null)
        {
            project.CommercialPartidaRules.Add(result);
        }
        else
        {
            int index = project.CommercialPartidaRules.IndexOf(existing);
            project.CommercialPartidaRules[index] = result;
        }

        audit.RecordPartidaChange(
            item.Id,
            "Modelo comercial",
            existing?.Model.DisplayName() ?? "Heredado",
            result.UseProjectModel ? "Heredado" : result.Model.DisplayName());

        session.MarkDirty("commercial_partida_rule");
        refreshPresentation();
        setStatusMessage(result.UseProjectModel
            ? "La partida vuelve a heredar el modelo del proyecto."
            : $"Modelo comercial aplicado: {result.Model.DisplayName()}.");
    }
}
