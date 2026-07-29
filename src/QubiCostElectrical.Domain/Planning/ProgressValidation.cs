using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

/// <summary>Validación de reglas de avance real (Domain puro).</summary>
public static class ProgressValidation
{
    public static IReadOnlyList<ProgressValidationIssue> Validate(
        ActivityProgressUpdate update,
        Activity activity,
        ProgressPolicy policy,
        ActivityProgressUpdate? previous = null)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(policy);

        var issues = new List<ProgressValidationIssue>();

        if (update.PercentComplete < 0m || update.PercentComplete > 100m)
            issues.Add(Error("PCT_RANGE", "El porcentaje completado debe estar entre 0 y 100."));

        if (update.PhysicalProgress < 0m || update.PhysicalProgress > 100m)
            issues.Add(Error("PHY_RANGE", "El avance físico debe estar entre 0 y 100."));

        if (update.FinancialProgress < 0m || update.FinancialProgress > 100m)
            issues.Add(Error("FIN_RANGE", "El avance financiero debe estar entre 0 y 100."));

        if (update.ActualFinish.HasValue && !update.ActualStart.HasValue)
            issues.Add(Error("FINISH_WITHOUT_START", "No puede existir término real sin inicio real."));

        if (update.ActualStart.HasValue && update.ActualFinish.HasValue &&
            update.ActualFinish.Value < update.ActualStart.Value)
            issues.Add(Error("FINISH_BEFORE_START", "El término real no puede ser anterior al inicio real."));

        if (update.ActualFinish.HasValue && update.ActualFinish.Value.Date > update.StatusDate.Date &&
            !policy.AllowFutureActualFinishVsStatusDate)
            issues.Add(Error("FUTURE_FINISH", "El término real no puede ser posterior a la fecha de estado."));

        if (update.PercentComplete > 0m && !update.ActualStart.HasValue &&
            !policy.AllowPositiveProgressWithoutActualStart)
            issues.Add(Error("PROGRESS_WITHOUT_START", "Actividad no iniciada no puede tener avance positivo."));

        if (update.ProgressStatus == ProgressTrackingStatus.Completed)
        {
            if (update.PercentComplete != 100m)
                issues.Add(Error("COMPLETE_PCT", "Una actividad completada debe tener 100% de avance."));
            if (activity.Type == ActivityType.Milestone && !update.ActualFinish.HasValue && !update.ActualStart.HasValue)
                issues.Add(Error("MILESTONE_DATE", "Un hito completado debe tener fecha real."));
            if (activity.Type != ActivityType.Milestone && !update.ActualFinish.HasValue)
                issues.Add(Error("COMPLETE_FINISH", "Completar una actividad requiere término real."));
        }

        if (update.PercentComplete == 100m && update.ProgressStatus != ProgressTrackingStatus.Completed &&
            update.ProgressStatus != ProgressTrackingStatus.Cancelled)
            issues.Add(Warn("PCT100_STATUS", "100% debería corresponder a estado Completada."));

        if (update.ExecutedQuantity.HasValue && update.ExecutedQuantity.Value < 0m)
            issues.Add(Error("QTY_NEGATIVE", "La cantidad ejecutada no puede ser negativa."));

        if (update.ExecutedQuantity.HasValue && update.PlannedQuantity.HasValue &&
            update.ExecutedQuantity.Value > update.PlannedQuantity.Value && !policy.AllowOverProgress)
            issues.Add(Error("QTY_OVER", "La ejecución supera lo planificado (sobreavance no permitido)."));

        if (update.ExecutedQuantity.HasValue && !update.PlannedQuantity.HasValue &&
            activity.PlannedQuantity is null)
            issues.Add(Error("QTY_NO_PLAN", "No hay cantidad planificada para registrar ejecución."));

        if (!string.IsNullOrWhiteSpace(update.QuantityUnit) &&
            !string.IsNullOrWhiteSpace(activity.QuantityUnit) &&
            !string.Equals(update.QuantityUnit, activity.QuantityUnit, StringComparison.OrdinalIgnoreCase))
            issues.Add(Error("QTY_UNIT", "Unidad de cantidad incompatible con la actividad."));

        if (update.ActualCost.HasValue && update.ActualCost.Value < 0m)
            issues.Add(Error("COST_NEGATIVE", "El costo real no puede ser negativo."));

        if (update.CommittedCost.HasValue && update.CommittedCost.Value < 0m)
            issues.Add(Error("COMMITTED_NEGATIVE", "El costo comprometido no puede ser negativo."));

        if (update.RemainingDuration is not null && update.RemainingDuration.Value < 0)
            issues.Add(Error("REMAINING_NEG", "La duración restante no puede ser negativa."));

        if (update.ActualHours.HasValue && update.ActualHours.Value < 0m)
            issues.Add(Error("HOURS_NEG", "Las horas reales no pueden ser negativas."));

        if (previous is not null && update.PercentComplete < previous.PercentComplete)
        {
            if (string.IsNullOrWhiteSpace(update.Comment) && !policy.AllowPercentDecreaseWithoutComment)
                issues.Add(Error("PCT_DECREASE", "Disminuir el porcentaje requiere comentario y permiso."));
        }

        return issues;
    }

    private static ProgressValidationIssue Error(string code, string message) =>
        new() { Code = code, Message = message, IsError = true };

    private static ProgressValidationIssue Warn(string code, string message) =>
        new() { Code = code, Message = message, IsError = false };

    public static bool HasErrors(IReadOnlyList<ProgressValidationIssue> issues)
    {
        foreach (var i in issues)
            if (i.IsError) return true;
        return false;
    }
}
