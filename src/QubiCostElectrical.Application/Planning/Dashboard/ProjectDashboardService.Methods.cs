using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ProjectDashboardService
{
    private async Task<ProjectExecutiveDashboard> BuildAsync(ScheduleId scheduleId, DashboardFilter? filter, CancellationToken ct)
    {
        filter ??= new DashboardFilter();
        var thresholds = new DashboardTrafficLightThresholds();
        var schedule = await _scheduleRepo.GetByIdAsync(scheduleId, ct)
            ?? throw new InvalidOperationException("Cronograma no encontrado.");
        var activities = (await _activityRepo.GetByScheduleAsync(scheduleId, ct)).Where(a => !a.IsDeleted).ToList();
        var wbs = await _wbsRepo.GetByScheduleAsync(scheduleId, ct);
        var status = await _statusDateRepo.GetByScheduleAsync(scheduleId, ct);
        var progressList = await _progressRepo.GetByScheduleAsync(scheduleId, ct);
        var progressMap = progressList.ToDictionary(p => p.ActivityId.Value, p => p);
        var baselines = await _baselineRepo.GetByScheduleAsync(scheduleId, ct);
        var official = baselines.FirstOrDefault(b => b.IsOfficial);
        var evm = await _evm.GetLatestAsync(scheduleId, ct);
        var history = await _evm.GetHistoryAsync(scheduleId, 200, ct);
        var previous = history.Skip(1).FirstOrDefault();

        ScheduleProgressSummary? summary = null;
        if (_progressTracking is not null)
        {
            try { summary = await _progressTracking.GetSummaryAsync(scheduleId, ct); }
            catch { /* sin resumen */ }
        }

        var quality = ResolveQuality(official is not null, status is not null, evm is not null, progressList.Count > 0);
        var asOf = status?.StatusDate ?? filter.StatusDate;
        var kpis = new List<DashboardKpi>();
        kpis.AddRange(DashboardKpiFactory.FromEvm(evm, previous, thresholds, quality, asOf));
        kpis.Add(DashboardKpiFactory.ScheduleKpi("BASE_FINISH", "Fin baseline", schedule.TargetEndDate, quality, asOf));
        kpis.Add(DashboardKpiFactory.ScheduleKpi("PLAN_FINISH", "Fin plan vigente", schedule.TargetEndDate, quality, asOf));
        kpis.Add(DashboardKpiFactory.ScheduleKpi("FCST_FINISH", "Fin forecast", summary?.ForecastProjectFinish ?? evm?.Forecast.ForecastFinish, quality, asOf));

        var critical = activities.Count(a => a.IsCritical);
        var almostCritical = activities.Count(a => !a.IsCritical && (a.TotalFloat ?? 99) <= 5);
        var completed = activities.Count(a => (progressMap.TryGetValue(a.Id.Value, out var p) ? p.PercentComplete : 0m) >= 100m);
        var delayed = CountDelayed(activities, progressMap, asOf);
        var negFloat = activities.Count(a => (a.TotalFloat ?? 0) < 0);
        var avgFloat = activities.Count == 0 ? 0m : (decimal)activities.Average(a => a.TotalFloat ?? 0d);
        kpis.Add(DashboardKpiFactory.CountKpi("CRIT", "Actividades críticas", critical, critical > 0 ? DashboardKpiStatus.Warning : DashboardKpiStatus.Good, quality, asOf));
        kpis.Add(DashboardKpiFactory.CountKpi("NEAR_CRIT", "Casi críticas", almostCritical, DashboardKpiStatus.Neutral, quality, asOf));
        kpis.Add(DashboardKpiFactory.CountKpi("DELAYED", "Atrasadas", delayed, delayed > 0 ? DashboardKpiStatus.Critical : DashboardKpiStatus.Good, quality, asOf));
        kpis.Add(DashboardKpiFactory.CountKpi("DONE", "Completadas", completed, DashboardKpiStatus.Good, quality, asOf));
        kpis.Add(DashboardKpiFactory.CountKpi("NEG_FLOAT", "Holgura negativa", negFloat, negFloat > 0 ? DashboardKpiStatus.Critical : DashboardKpiStatus.Good, quality, asOf));
        kpis.Add(new DashboardKpi
        {
            Code = "AVG_FLOAT",
            Title = "Holgura media",
            Category = "Plazo",
            Value = Math.Round(avgFloat, 2, MidpointRounding.ToEven),
            DisplayValue = avgFloat.ToString("0.##", CultureInfo.InvariantCulture),
            Unit = "días",
            Status = DashboardKpiStatus.Neutral,
            Interpretation = "Promedio de total float",
            AsOfDate = asOf,
            Source = "CPM",
            DataQuality = quality
        });

        var gran = filter.Granularity;
        var mode = filter.SeriesMode;
        var unit = filter.SeriesUnit;
        var currency = filter.CurrencyCode;
        var series = new List<DashboardSeries>
        {
            DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.PvCumulative, "PV",
                history.Select(h => (h.StatusDate, h.Pv)), gran, mode, unit, currency, filter.PeriodFrom, filter.PeriodTo),
            DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.EvCumulative, "EV",
                history.Select(h => (h.StatusDate, h.Ev)), gran, mode, unit, currency, filter.PeriodFrom, filter.PeriodTo),
            DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.AcCumulative, "AC",
                history.Select(h => (h.StatusDate, h.Ac)), gran, mode, unit, currency, filter.PeriodFrom, filter.PeriodTo),
            DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.ForecastCumulative, "Forecast",
                history.Select(h => (h.StatusDate, h.Eac)), gran, mode, unit, currency, filter.PeriodFrom, filter.PeriodTo)
        };

        if (evm is not null)
        {
            series.Add(DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.BaselineCumulative, "Baseline BAC",
                new[] { (asOf ?? DateTime.Today, evm.Indicators.Bac) }, gran, DashboardSeriesMode.Cumulative, unit, currency, null, null));
            series.Add(DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.CommittedCumulative, "Comprometido",
                new[] { (asOf ?? DateTime.Today, progressList.Sum(p => p.CommittedCost ?? 0m)) }, gran, mode, unit, currency, null, null));
            series.Add(DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.PhysicalProgressCumulative, "% Físico",
                new[] { (asOf ?? DateTime.Today, evm.Indicators.PercentEarned) }, gran, mode, DashboardSeriesUnit.Percent, currency, null, null));
            series.Add(DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.PlannedProgressCumulative, "% Plan",
                new[] { (asOf ?? DateTime.Today, evm.Indicators.PercentPlanned) }, gran, mode, DashboardSeriesUnit.Percent, currency, null, null));
        }

        var alerts = DashboardAlertBuilder.Build(scheduleId, evm, summary, official is not null, asOf, activities, progressMap, DateTime.UtcNow);
        if (filter.MinSeverity.HasValue)
            alerts = alerts.Where(a => a.Severity <= filter.MinSeverity.Value).ToList();
        if (!string.IsNullOrWhiteSpace(filter.AlertCode))
            alerts = alerts.Where(a => a.Code == filter.AlertCode).ToList();

        var ops = BuildOperational(activities, progressMap, alerts, asOf);
        var deviations = BuildDeviations(activities, progressMap, evm);
        var wbsNodes = BuildWbs(wbs, activities, progressMap, alerts);
        var byResp = BuildResponsible(activities, progressMap, alerts);

        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{scheduleId.Value}|{asOf:O}|{evm?.Indicators.Spi}|{evm?.Indicators.Cpi}|{activities.Count}|{progressList.Count}")))
            .ToLowerInvariant();

        var summaryText = BuildExecutiveSummary(evm, delayed, critical, quality);

        return new ProjectExecutiveDashboard
        {
            ScheduleId = scheduleId,
            ProjectName = schedule.Name,
            StatusDate = asOf,
            ActiveBaselineId = official?.BaselineId,
            ActiveBaselineName = official?.Name,
            BaselineFinish = schedule.TargetEndDate,
            PlanFinish = schedule.TargetEndDate,
            ForecastFinish = summary?.ForecastProjectFinish ?? evm?.Forecast.ForecastFinish,
            CurrencyCode = currency,
            DataQuality = quality,
            GeneratedUtc = DateTime.UtcNow,
            Kpis = kpis,
            Series = series,
            Alerts = alerts,
            Deviations = deviations,
            WbsNodes = wbsNodes,
            ByResponsible = byResp,
            OperationalRows = ops,
            ExecutiveSummary = summaryText,
            SourceHashes = sourceHash
        };
    }

    private static DashboardDataQuality ResolveQuality(bool hasBaseline, bool hasStatus, bool hasEvm, bool hasProgress)
    {
        if (!hasBaseline) return DashboardDataQuality.NoBaseline;
        if (!hasProgress) return DashboardDataQuality.NoProgress;
        if (!hasStatus) return DashboardDataQuality.Outdated;
        if (!hasEvm) return DashboardDataQuality.Partial;
        return DashboardDataQuality.Complete;
    }

    private static int CountDelayed(
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progress,
        DateTime? statusDate)
    {
        if (!statusDate.HasValue) return 0;
        var n = 0;
        foreach (var a in activities)
        {
            var pct = progress.TryGetValue(a.Id.Value, out var p) ? p.PercentComplete : 0m;
            var finish = a.EarlyFinish ?? a.PlannedFinish;
            if (finish.HasValue && finish.Value.Date < statusDate.Value.Date && pct < 100m)
                n++;
        }
        return n;
    }

    private static List<DashboardOperationalRow> BuildOperational(
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progress,
        IReadOnlyList<DashboardAlert> alerts,
        DateTime? statusDate)
    {
        var alertCount = alerts.GroupBy(a => a.EntityId).ToDictionary(g => g.Key, g => g.Count());
        var rows = new List<DashboardOperationalRow>();
        foreach (var a in activities.OrderBy(x => x.Code, StringComparer.Ordinal))
        {
            progress.TryGetValue(a.Id.Value, out var p);
            var pct = p?.PercentComplete ?? 0m;
            var finish = a.EarlyFinish ?? a.PlannedFinish;
            decimal? varDays = null;
            if (statusDate.HasValue && finish.HasValue && pct < 100m)
                varDays = (decimal)(statusDate.Value.Date - finish.Value.Date).TotalDays;
            rows.Add(new DashboardOperationalRow
            {
                ActivityId = a.Id.Value,
                Code = a.Code,
                Name = a.Name,
                WbsCode = a.WbsNodeId.Value,
                Responsible = a.ResponsibleId,
                IsCritical = a.IsCritical,
                PercentComplete = pct,
                ScheduleVarianceDays = varDays,
                ActualCost = p?.ActualCost ?? 0m,
                CommittedCost = p?.CommittedCost,
                ForecastFinish = finish,
                OutOfSequence = alerts.Any(x => x.EntityId == a.Id.Value && x.Code == "OOS"),
                WithoutUpdate = pct <= 0m,
                IsBlocked = false,
                AlertCount = alertCount.TryGetValue(a.Id.Value, out var c) ? c : 0
            });
        }
        return rows;
    }

    private static IReadOnlyList<DashboardOperationalRow> ApplyOperationalFilter(
        IReadOnlyList<DashboardOperationalRow> rows, DashboardFilter filter)
    {
        IEnumerable<DashboardOperationalRow> q = rows;
        if (filter.CriticalOnly == true) q = q.Where(r => r.IsCritical);
        if (filter.DelayedOnly == true) q = q.Where(r => (r.ScheduleVarianceDays ?? 0m) > 0m);
        if (filter.OverCostOnly == true) q = q.Where(r => r.ActualCost > 0m && r.PercentComplete < 100m);
        if (filter.OutOfSequenceOnly == true) q = q.Where(r => r.OutOfSequence);
        if (filter.WithoutUpdateOnly == true) q = q.Where(r => r.WithoutUpdate);
        if (!string.IsNullOrWhiteSpace(filter.Responsible))
            q = q.Where(r => string.Equals(r.Responsible, filter.Responsible, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.WbsNodeId))
            q = q.Where(r => r.WbsCode == filter.WbsNodeId);
        return q.ToList();
    }

    private static List<DashboardDeviationItem> BuildDeviations(
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progress,
        EarnedValueResult? evm)
    {
        var list = new List<DashboardDeviationItem>();
        foreach (var a in activities)
        {
            progress.TryGetValue(a.Id.Value, out var p);
            var delay = 100m - (p?.PercentComplete ?? 0m);
            if (delay > 0m && a.IsCritical)
            {
                list.Add(new DashboardDeviationItem
                {
                    RankKey = "DELAY:" + a.Id.Value,
                    Title = a.Code + " · " + a.Name,
                    EntityType = "Activity",
                    EntityId = a.Id.Value,
                    AbsoluteImpact = delay,
                    PercentImpact = delay,
                    Metric = "Atraso % restante",
                    Responsible = a.ResponsibleId
                });
            }
            var ac = p?.ActualCost ?? 0m;
            if (ac > 0m)
            {
                list.Add(new DashboardDeviationItem
                {
                    RankKey = "COST:" + a.Id.Value,
                    Title = a.Code,
                    EntityType = "Activity",
                    EntityId = a.Id.Value,
                    AbsoluteImpact = ac,
                    Metric = "AC",
                    Responsible = a.ResponsibleId
                });
            }
        }
        if (evm is not null && evm.Indicators.Cv < 0m)
        {
            list.Add(new DashboardDeviationItem
            {
                RankKey = "CV",
                Title = "Variación de costo proyecto",
                EntityType = "EVM",
                EntityId = evm.ScheduleId.Value,
                AbsoluteImpact = Math.Abs(evm.Indicators.Cv),
                PercentImpact = evm.Indicators.Bac == 0m ? null : Math.Abs(evm.Indicators.Cv) / evm.Indicators.Bac * 100m,
                Metric = "CV"
            });
        }
        return list
            .OrderByDescending(d => d.AbsoluteImpact)
            .ThenBy(d => d.RankKey, StringComparer.Ordinal)
            .Take(50)
            .ToList();
    }

    private static List<DashboardWbsNode> BuildWbs(
        IReadOnlyList<WbsPlanningNode> nodes,
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progress,
        IReadOnlyList<DashboardAlert> alerts)
    {
        var byWbs = activities.GroupBy(a => a.WbsNodeId.Value).ToDictionary(g => g.Key, g => g.ToList());
        var result = new List<DashboardWbsNode>();
        foreach (var n in nodes.OrderBy(x => x.Level).ThenBy(x => x.WbsCode, StringComparer.Ordinal))
        {
            byWbs.TryGetValue(n.Id.Value, out var acts);
            acts ??= new List<Activity>();
            decimal weight = acts.Count == 0 ? 0m : acts.Count;
            decimal phys = 0m, plan = 0m, ac = 0m;
            int crit = 0;
            foreach (var a in acts)
            {
                progress.TryGetValue(a.Id.Value, out var p);
                var pct = p?.PercentComplete ?? 0m;
                phys += pct;
                plan += a.PlannedProgressPct;
                ac += p?.ActualCost ?? 0m;
                if (a.IsCritical) crit++;
            }
            var count = acts.Count;
            var physAvg = count == 0 ? 0m : phys / count;
            var planAvg = count == 0 ? 0m : plan / count;
            decimal? spi = planAvg <= 0m ? null : Math.Round(physAvg / planAvg, 4, MidpointRounding.ToEven);
            result.Add(new DashboardWbsNode
            {
                NodeId = n.Id.Value,
                ParentId = n.ParentId?.Value,
                Code = n.WbsCode,
                Name = n.Name,
                Level = n.Level,
                PlannedProgressPct = Math.Round(planAvg, 2, MidpointRounding.ToEven),
                PhysicalProgressPct = Math.Round(physAvg, 2, MidpointRounding.ToEven),
                FinancialProgressPct = 0m,
                Pv = 0m,
                Ev = 0m,
                Ac = ac,
                Spi = spi,
                Cpi = null,
                ForecastCost = ac,
                CriticalCount = crit,
                AlertCount = alerts.Count(a => a.WbsCode == n.Id.Value || a.WbsCode == n.WbsCode),
                Trend = DashboardTrendDirection.Unknown,
                Weight = weight
            });
        }
        return result;
    }

    private static List<DashboardResponsibleSummary> BuildResponsible(
        IReadOnlyList<Activity> activities,
        IReadOnlyDictionary<string, ActivityProgressUpdate> progress,
        IReadOnlyList<DashboardAlert> alerts)
    {
        return activities
            .GroupBy(a => string.IsNullOrWhiteSpace(a.ResponsibleId) ? "Sin asignar" : a.ResponsibleId!)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var list = g.ToList();
                var completed = list.Count(a => progress.TryGetValue(a.Id.Value, out var p) && p.PercentComplete >= 100m);
                var delayed = list.Count(a =>
                {
                    var pct = progress.TryGetValue(a.Id.Value, out var p) ? p.PercentComplete : 0m;
                    return pct < 100m && a.IsCritical;
                });
                var phys = list.Count == 0 ? 0m : list.Average(a => progress.TryGetValue(a.Id.Value, out var p) ? p.PercentComplete : 0m);
                var ac = list.Sum(a => progress.TryGetValue(a.Id.Value, out var p) ? p.ActualCost ?? 0m : 0m);
                return new DashboardResponsibleSummary
                {
                    Responsible = g.Key,
                    AssignedCount = list.Count,
                    CompletedCount = completed,
                    DelayedCount = delayed,
                    CriticalCount = list.Count(a => a.IsCritical),
                    PhysicalProgressPct = Math.Round((decimal)phys, 2, MidpointRounding.ToEven),
                    CostAc = ac,
                    AlertCount = alerts.Count(a => string.Equals(a.Responsible, g.Key, StringComparison.OrdinalIgnoreCase)),
                    PendingCount = list.Count - completed
                };
            })
            .ToList();
    }

    private static string BuildExecutiveSummary(EarnedValueResult? evm, int delayed, int critical, DashboardDataQuality quality)
    {
        if (quality is DashboardDataQuality.NoBaseline or DashboardDataQuality.NoProgress)
            return "Datos incompletos: no interpretar KPIs como fiables.";
        if (evm is null)
            return $"Sin EVM. Críticas={critical}; atrasadas={delayed}.";
        var spi = evm.Indicators.Spi?.ToString("0.####") ?? "N/D";
        var cpi = evm.Indicators.Cpi?.ToString("0.####") ?? "N/D";
        return $"SPI={spi}; CPI={cpi}; VAC={evm.Indicators.Vac:N0}. Críticas={critical}; atrasadas={delayed}. {quality}.";
    }

    private async Task SetLifecycleAsync(
        string alertId, string actor, DashboardAlertLifecycle life, string? note, CancellationToken ct)
    {
        var alert = await _alertRepo.GetByIdAsync(alertId, ct)
            ?? throw new InvalidOperationException("Alerta no encontrada.");
        Ensure(PlanningPermissions.DashboardAlertManage, actor, alert.ScheduleId, "DashboardAlert", life.ToString());
        var updated = new DashboardAlert
        {
            AlertId = alert.AlertId,
            ScheduleId = alert.ScheduleId,
            Code = alert.Code,
            Severity = alert.Severity,
            Title = alert.Title,
            Message = alert.Message,
            EntityType = alert.EntityType,
            EntityId = alert.EntityId,
            RaisedUtc = alert.RaisedUtc,
            Responsible = alert.Responsible,
            RecommendedAction = alert.RecommendedAction,
            Source = alert.Source,
            ImpactScore = alert.ImpactScore,
            WbsCode = alert.WbsCode,
            Lifecycle = life,
            LifecycleNote = note,
            LifecycleBy = actor,
            LifecycleUtc = DateTime.UtcNow
        };
        await _alertRepo.SaveAsync(updated, ct);
        await AuditAsync(alert.ScheduleId, "DashboardAlert", alertId, life.ToString(), actor, alert.Lifecycle.ToString(), life.ToString(), note, "Ok", ct);
    }

    private void Ensure(string permission, string actor, ScheduleId scheduleId, string entityType, string action)
    {
        if (_permissions.Has(permission)) return;
        _ = AuditAsync(scheduleId, entityType, scheduleId.Value, "Denied", actor, null, permission, action, "Denied", CancellationToken.None);
        throw new UnauthorizedAccessException($"Permiso denegado: {permission}");
    }

    private Task AuditAsync(
        ScheduleId scheduleId, string entityType, string entityId, string action, string actor,
        string? previous, string? next, string? comment, string result, CancellationToken ct) =>
        _auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId.Value,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            UtcTimestamp = DateTime.UtcNow,
            PreviousState = previous,
            NewState = next,
            Comment = comment ?? string.Empty,
            Result = result
        }, ct);
}
