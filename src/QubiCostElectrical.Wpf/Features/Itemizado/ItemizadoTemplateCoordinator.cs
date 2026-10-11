using System;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public static class ItemizadoTemplateCoordinator
{
    public static void LoadTemplate(
        BudgetProjectSession session,
        string? templateId,
        WpfUndoService undo,
        ProjectAuditRecorder audit,
        Action onLoaded,
        Action<string> setStatusMessage)
    {
        if (session.IsReadOnly)
        {
            ModuleFeedback.NotifyReadOnly("Cargar plantilla", session.ReadOnlyReason);
            return;
        }

        var template = ProjectTemplateCatalog.All.FirstOrDefault(t =>
            string.Equals(t.Id, templateId, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(templateId) && t.Nombre.Contains(templateId, StringComparison.OrdinalIgnoreCase)));

        if (template == null || template.EsEnBlanco)
        {
            WpfDialogService.Warning("Plantilla", "No se encontró la plantilla seleccionada.");
            return;
        }

        if (session.Itemizado.Count > 0)
        {
            bool confirm = WpfDialogService.Confirm(
                "Cargar plantilla RIC",
                $"El itemizado actual contiene {session.Itemizado.Count} filas.\n\n¿Desea agregar las partidas de la plantilla «{template.Nombre}» al final del presupuesto?",
                isWarning: false);
            if (!confirm)
                return;
        }

        try
        {
            undo.PushUndoPoint(session, $"Cargar plantilla {template.Nombre}");
            var newRows = template.CreateInstanceRows();
            foreach (var row in newRows)
            {
                session.Itemizado.Add(row);
            }

            audit.RecordLifecycle("itemizado_template", $"Plantilla {template.Nombre} cargada ({newRows.Count} filas)");
            session.MarkDirty("itemizado_template");
            onLoaded();
            setStatusMessage($"Plantilla «{template.Nombre}» cargada ({newRows.Count} partidas/títulos).");
            ShellNotificationService.Instance.ShowSuccess(
                "Plantilla RIC cargada",
                $"Plantilla «{template.Nombre}» cargada con éxito ({newRows.Count} partidas/títulos).",
                actionLabel: "Deshacer",
                action: () => undo.Undo(session));
        }
        catch (Exception ex)
        {
            AppLog.Error("Itemizado", "Falló al cargar plantilla", ex);
            WpfDialogService.Error("Cargar plantilla", OperationFailureMessages.ToUserMessage(ex));
        }
    }
}
