namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Política única de feedback por módulo. Evita toast + StatusMessage + modal duplicados.
/// Ver <c>docs/UI_SCREEN_CONTRACT.md</c> sección Feedback.
/// </summary>
public static class ModuleFeedback
{
    /// <summary>Bloqueo por solo lectura: toast (no modal, no StatusMessage).</summary>
    public static void NotifyReadOnly(string operationTitle, string? reason = null)
    {
        var message = string.IsNullOrWhiteSpace(reason)
            ? "El proyecto está en modo solo lectura."
            : reason.Trim();
        ShellNotificationService.Instance.ShowWarning(operationTitle, message);
    }

    /// <summary>Éxito global o con acción secundaria: toast.</summary>
    public static void NotifySuccess(string title, string message, Action? secondaryAction = null, string? secondaryLabel = null)
    {
        if (secondaryAction != null && !string.IsNullOrWhiteSpace(secondaryLabel))
            ShellNotificationService.Instance.ShowSuccess(title, message, secondaryLabel, secondaryAction);
        else
            ShellNotificationService.Instance.ShowSuccess(title, message);
    }

    /// <summary>Error que requiere atención: modal (no toast).</summary>
    public static void NotifyError(string title, string message)
        => WpfDialogService.Error(title, message);
}
