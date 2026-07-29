using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class DashboardAlertBuilder
{
    public static List<DashboardAlert> Build(
        ScheduleId scheduleId,
        EarnedValueResult? evm,
        ScheduleProgressSummary? progress,
        bool hasOfficialBaseline,
        DateTime? statusDate,
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progressByActivity,
        DateTime utcNow)
    {
        var alerts = new List<DashboardAlert>();

        if (!hasOfficialBaseline)
            alerts.Add(Make(scheduleId, "NO_BASELINE", DashboardAlertSeverity.Critical,
                "Baseline inexistente", "No hay baseline oficial; los KPIs de plan no son fiables.",
                "Schedule", scheduleId.Value, utcNow, null, "Crear y marcar baseline oficial.", "Baseline", 100, null));

        if (!statusDate.HasValue)
            alerts.Add(Make(scheduleId, "NO_STATUS_DATE", DashboardAlertSeverity.Critical,
                "Sin fecha de estado", "No se puede consolidar avance ni EVM sin fecha de estado.",
                "Schedule", scheduleId.Value, utcNow, null, "Definir fecha de estado en Seguimiento.", "Progress", 95, null));

        if (evm is null)
        {
            alerts.Add(Make(scheduleId, "NO_EVM", DashboardAlertSeverity.High,
                "Sin cálculo EVM", "No hay resultado EVM persistido.",
                "Schedule", scheduleId.Value, utcNow, null, "Calcular EVM en Valor ganado.", "EVM", 80, null));
        }
        else
        {
            if (evm.Indicators.Spi is < 0.9m)
                alerts.Add(Make(scheduleId, "SPI_LOW", DashboardAlertSeverity.Critical,
                    "SPI bajo", $"SPI={evm.Indicators.Spi:0.####}: atraso significativo.",
                    "EVM", scheduleId.Value, utcNow, null, "Revisar camino crítico y reprogramación.", "EVM", 90, null));
            else if (evm.Indicators.Spi is < 1m)
                alerts.Add(Make(scheduleId, "SPI_WARN", DashboardAlertSeverity.Medium,
                    "SPI bajo umbral", $"SPI={evm.Indicators.Spi:0.####}.",
                    "EVM", scheduleId.Value, utcNow, null, "Monitorear avance físico.", "EVM", 60, null));

            if (evm.Indicators.Cpi is < 0.9m)
                alerts.Add(Make(scheduleId, "CPI_LOW", DashboardAlertSeverity.Critical,
                    "CPI bajo", $"CPI={evm.Indicators.Cpi:0.####}: sobre costo significativo.",
                    "EVM", scheduleId.Value, utcNow, null, "Revisar costos reales vs EV.", "EVM", 90, null));
            else if (evm.Indicators.Cpi is < 1m)
                alerts.Add(Make(scheduleId, "CPI_WARN", DashboardAlertSeverity.Medium,
                    "CPI bajo umbral", $"CPI={evm.Indicators.Cpi:0.####}.",
                    "EVM", scheduleId.Value, utcNow, null, "Controlar costos comprometidos.", "EVM", 60, null));

            if (evm.Indicators.Vac < 0m)
                alerts.Add(Make(scheduleId, "VAC_NEG", DashboardAlertSeverity.High,
                    "VAC negativo", $"VAC={evm.Indicators.Vac:N0}: se prevé terminar sobre BAC.",
                    "EVM", scheduleId.Value, utcNow, null, "Actualizar forecast de costo.", "EVM", 75, null));

            if (evm.Forecast.ForecastFinishVarianceDays is > 0m)
                alerts.Add(Make(scheduleId, "FORECAST_LATE", DashboardAlertSeverity.High,
                    "Forecast fuera de fecha",
                    $"Variación de término forecast: +{evm.Forecast.ForecastFinishVarianceDays:0.##} días.",
                    "Forecast", scheduleId.Value, utcNow, null, "Revisar actividades críticas atrasadas.", "Forecast", 70, null));
        }

        foreach (var act in activities.Where(a => a.IsCritical))
        {
            progressByActivity.TryGetValue(act.Id.Value, out var prog);
            var pct = prog?.PercentComplete ?? 0m;
            var finish = act.EarlyFinish ?? act.LateFinish;
            if (statusDate.HasValue && finish.HasValue && finish.Value.Date < statusDate.Value.Date && pct < 100m)
            {
                alerts.Add(Make(scheduleId, "CRIT_LATE", DashboardAlertSeverity.Critical,
                    "Actividad crítica atrasada", $"{act.Code} · {act.Name}",
                    "Activity", act.Id.Value, utcNow, act.ResponsibleId,
                    "Actualizar avance o reprogramar.", "CPM", 85, act.WbsNodeId.Value));
            }

            if ((act.TotalFloat ?? 0d) < 0d)
            {
                alerts.Add(Make(scheduleId, "NEG_FLOAT", DashboardAlertSeverity.High,
                    "Holgura negativa", $"{act.Code}: float={act.TotalFloat}",
                    "Activity", act.Id.Value, utcNow, act.ResponsibleId,
                    "Revisar restricciones y dependencias.", "CPM", 70, act.WbsNodeId.Value));
            }
        }

        foreach (var act in activities)
        {
            progressByActivity.TryGetValue(act.Id.Value, out var prog);
            if (prog is null || prog.PercentComplete <= 0m)
            {
                if (statusDate.HasValue && act.EarlyStart.HasValue && act.EarlyStart.Value.Date <= statusDate.Value.Date)
                {
                    alerts.Add(Make(scheduleId, "NO_UPDATE", DashboardAlertSeverity.Low,
                        "Actividad sin actualización", $"{act.Code} · {act.Name}",
                        "Activity", act.Id.Value, utcNow, act.ResponsibleId,
                        "Registrar avance en Seguimiento.", "Progress", 30, act.WbsNodeId.Value));
                }
            }
            else
            {
                if ((prog.ActualCost ?? 0m) > 0m && prog.PercentComplete <= 0m)
                    alerts.Add(Make(scheduleId, "COST_NO_PROGRESS", DashboardAlertSeverity.Medium,
                        "Costo real sin avance", $"{act.Code}",
                        "Activity", act.Id.Value, utcNow, act.ResponsibleId,
                        "Conciliar costos y % completado.", "Progress", 50, act.WbsNodeId.Value));
                if (prog.PercentComplete > 0m && (prog.ActualCost ?? 0m) <= 0m && (prog.CommittedCost ?? 0m) <= 0m)
                    alerts.Add(Make(scheduleId, "PROGRESS_NO_COST", DashboardAlertSeverity.Low,
                        "Avance sin costo", $"{act.Code}",
                        "Activity", act.Id.Value, utcNow, act.ResponsibleId,
                        "Registrar AC o costo comprometido.", "Progress", 25, act.WbsNodeId.Value));
            }
        }

        if (progress is { OutOfSequenceWarnings.Count: > 0 })
            alerts.Add(Make(scheduleId, "OOS", DashboardAlertSeverity.Medium,
                "Progreso fuera de secuencia",
                $"{progress.OutOfSequenceWarnings.Count} actividades con advertencia OOS.",
                "Schedule", scheduleId.Value, utcNow, null, "Revisar dependencias y evidencias.", "Progress", 55, null));

        return Prioritize(alerts);
    }

    public static List<DashboardAlert> Prioritize(IEnumerable<DashboardAlert> alerts) =>
        alerts
            .OrderBy(a => (int)a.Severity)
            .ThenByDescending(a => a.ImpactScore)
            .ThenByDescending(a => a.RaisedUtc)
            .ThenBy(a => a.WbsCode ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(a => a.EntityId, StringComparer.Ordinal)
            .ThenBy(a => a.Code, StringComparer.Ordinal)
            .ToList();

    private static DashboardAlert Make(
        ScheduleId scheduleId, string code, DashboardAlertSeverity sev,
        string title, string message, string entityType, string entityId,
        DateTime utc, string? responsible, string action, string source, int impact, string? wbs) =>
        new()
        {
            AlertId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            Code = code,
            Severity = sev,
            Title = title,
            Message = message,
            EntityType = entityType,
            EntityId = entityId,
            RaisedUtc = utc,
            Responsible = responsible,
            RecommendedAction = action,
            Source = source,
            ImpactScore = impact,
            WbsCode = wbs,
            Lifecycle = DashboardAlertLifecycle.Open
        };
}
