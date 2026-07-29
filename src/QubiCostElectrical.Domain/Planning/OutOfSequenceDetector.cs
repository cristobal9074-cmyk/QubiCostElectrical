using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>Detecta progreso fuera de secuencia. No corrige automáticamente.</summary>
public static class OutOfSequenceDetector
{
    public static IReadOnlyList<OutOfSequenceWarning> Detect(
        DateTime statusDate,
        IReadOnlyList<Activity> activities,
        IReadOnlyList<ActivityDependency> dependencies,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressByActivityId,
        Func<DateTime, bool>? isWorkingDay = null)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(progressByActivityId);

        var warnings = new List<OutOfSequenceWarning>();

        foreach (var dep in dependencies)
        {
            if (!progressByActivityId.TryGetValue(dep.SuccessorActivityId.Value, out var succProg))
                continue;
            progressByActivityId.TryGetValue(dep.PredecessorActivityId.Value, out var predProg);

            switch (dep.Type)
            {
                case DependencyType.FinishToStart:
                    if (succProg.ActualStart.HasValue &&
                        (predProg is null || !predProg.ActualFinish.HasValue) &&
                        (predProg is null || predProg.PercentComplete < 100m))
                    {
                        warnings.Add(Warn(
                            OutOfSequenceKind.SuccessorStartedBeforeFsPredecessorComplete,
                            dep.SuccessorActivityId,
                            dep.PredecessorActivityId,
                            statusDate,
                            "Sucesora iniciada antes de completar predecesora FS",
                            $"La actividad {dep.SuccessorActivityId.Value} inició sin que {dep.PredecessorActivityId.Value} estuviera completa (FS)."));
                    }
                    break;

                case DependencyType.StartToStart:
                    if (succProg.ActualStart.HasValue &&
                        (predProg is null || !predProg.ActualStart.HasValue))
                    {
                        warnings.Add(Warn(
                            OutOfSequenceKind.SuccessorStartedBeforeSsPredecessorStart,
                            dep.SuccessorActivityId,
                            dep.PredecessorActivityId,
                            statusDate,
                            "Sucesora iniciada antes del inicio SS de predecesora",
                            $"SS violada: {dep.SuccessorActivityId.Value} inició antes que {dep.PredecessorActivityId.Value}."));
                    }
                    break;

                case DependencyType.FinishToFinish:
                    if (succProg.ActualFinish.HasValue &&
                        (predProg is null || !predProg.ActualFinish.HasValue))
                    {
                        warnings.Add(Warn(
                            OutOfSequenceKind.FinishBeforeFfPredecessorFinish,
                            dep.SuccessorActivityId,
                            dep.PredecessorActivityId,
                            statusDate,
                            "Sucesora terminó antes que predecesora FF",
                            $"FF violada: {dep.SuccessorActivityId.Value} terminó sin término de {dep.PredecessorActivityId.Value}."));
                    }
                    break;
            }
        }

        foreach (var act in activities)
        {
            if (!progressByActivityId.TryGetValue(act.Id.Value, out var prog)) continue;

            if (prog.ActualStart.HasValue &&
                act.ConstraintType == ConstraintType.StartNoEarlierThan &&
                act.ConstraintDate.HasValue &&
                prog.ActualStart.Value.Date < act.ConstraintDate.Value.Date)
            {
                warnings.Add(Warn(
                    OutOfSequenceKind.StartedBeforeConstraint,
                    act.Id,
                    null,
                    statusDate,
                    "Inicio real anterior a restricción SNET",
                    $"Inicio real {prog.ActualStart:yyyy-MM-dd} antes de restricción {act.ConstraintDate:yyyy-MM-dd}."));
            }

            if (act.Type == ActivityType.Milestone && prog.ActualFinish.HasValue)
            {
                foreach (var dep in dependencies.Where(d => d.SuccessorActivityId.Value == act.Id.Value))
                {
                    progressByActivityId.TryGetValue(dep.PredecessorActivityId.Value, out var pred);
                    if (dep.Type == DependencyType.FinishToStart && (pred is null || pred.PercentComplete < 100m))
                    {
                        warnings.Add(Warn(
                            OutOfSequenceKind.MilestoneReachedBeforeDependency,
                            act.Id,
                            dep.PredecessorActivityId,
                            statusDate,
                            "Hito alcanzado antes de dependencia",
                            $"Hito {act.Code} alcanzado sin completar predecesora {dep.PredecessorActivityId.Value}."));
                    }
                }
            }

            if (isWorkingDay is not null && prog.ActualStart.HasValue && !isWorkingDay(prog.ActualStart.Value.Date))
            {
                warnings.Add(Warn(
                    OutOfSequenceKind.WorkOnNonWorkingDay,
                    act.Id,
                    null,
                    statusDate,
                    "Trabajo en día no laborable",
                    $"Inicio real {prog.ActualStart.Value:yyyy-MM-dd} cae en día no laborable del calendario."));
            }
        }

        return warnings;
    }

    private static OutOfSequenceWarning Warn(
        OutOfSequenceKind kind,
        ActivityId activityId,
        ActivityId? related,
        DateTime statusDate,
        string impact,
        string explanation) =>
        new()
        {
            Kind = kind,
            ActivityId = activityId,
            RelatedActivityId = related,
            DetectedAtStatusDate = statusDate,
            Impact = impact,
            Explanation = explanation
        };
}
