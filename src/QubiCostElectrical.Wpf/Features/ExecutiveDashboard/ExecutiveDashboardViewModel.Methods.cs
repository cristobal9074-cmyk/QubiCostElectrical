using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.ExecutiveDashboard;

public sealed partial class ExecutiveDashboardViewModel
{
    public async Task LoadAsync()
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            if (_host.Services.ProjectDashboard is null || _host.Services.ScheduleRepository is null)
            {
                StatusMessage = "Servicios de dashboard no inicializados.";
                return;
            }

            var schedules = await _host.Services.ScheduleRepository.GetByProjectAsync(_host.Session.CurrentProjectPath ?? string.Empty);
            var schedule = schedules.FirstOrDefault();
            if (schedule is null)
            {
                StatusMessage = "No hay cronograma.";
                return;
            }

            _scheduleId = schedule.Id;
            var gran = Granularity switch
            {
                "Daily" => DashboardSeriesGranularity.Daily,
                "Monthly" => DashboardSeriesGranularity.Monthly,
                _ => DashboardSeriesGranularity.Weekly
            };
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var dash = await _host.Services.ProjectDashboard.GetExecutiveAsync(
                schedule.Id,
                new DashboardFilter { Granularity = gran },
                actor);

            Header = $"{dash.ProjectName} · Estado {dash.StatusDate:dd/MM/yyyy} · Baseline {dash.ActiveBaselineName ?? "—"} · {dash.CurrencyCode}";
            Summary = dash.ExecutiveSummary;
            Quality = dash.DataQuality.ToString();

            _kpis.Clear();
            foreach (var k in dash.Kpis)
            {
                _kpis.Add(new DashboardKpiRow
                {
                    Code = k.Code,
                    Title = k.Title,
                    DisplayValue = k.DisplayValue + (string.IsNullOrEmpty(k.Unit) ? "" : " " + k.Unit),
                    Status = k.Status.ToString(),
                    Interpretation = k.Interpretation,
                    Trend = k.Trend.ToString(),
                    Icon = k.Status switch
                    {
                        DashboardKpiStatus.Good => "✓",
                        DashboardKpiStatus.Warning => "!",
                        DashboardKpiStatus.Critical => "✕",
                        DashboardKpiStatus.NoData => "?",
                        _ => "●"
                    }
                });
            }

            _alerts.Clear();
            foreach (var a in dash.Alerts) _alerts.Add(a);
            _deviations.Clear();
            foreach (var d in dash.Deviations.Take(20)) _deviations.Add(d);
            _wbs.Clear();
            foreach (var w in dash.WbsNodes) _wbs.Add(w);

            BindSCurve(dash);
            _snapshots.Clear();
            foreach (var s in await _host.Services.ProjectDashboard.ListSnapshotsAsync(schedule.Id))
                _snapshots.Add(s);

            StatusMessage = $"Generado {dash.GeneratedUtc:HH:mm:ss} · calidad {dash.DataQuality}";
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { _isBusy = false; }
    }

    private void BindSCurve(ProjectExecutiveDashboard dash)
    {
        _sCurve.Clear();
        var pv = dash.Series.FirstOrDefault(s => s.Kind == DashboardSeriesKind.PvCumulative);
        var ev = dash.Series.FirstOrDefault(s => s.Kind == DashboardSeriesKind.EvCumulative);
        var ac = dash.Series.FirstOrDefault(s => s.Kind == DashboardSeriesKind.AcCumulative);
        if (pv is null || pv.Points.Count == 0)
        {
            StatusMessage += " · Curva S: sin puntos (sin historial EVM).";
            return;
        }

        var max = pv.Points.Concat(ev?.Points ?? Array.Empty<DashboardDataPoint>())
            .Concat(ac?.Points ?? Array.Empty<DashboardDataPoint>())
            .Where(p => p.HasData && p.Value.HasValue)
            .Select(p => p.Value!.Value)
            .DefaultIfEmpty(1m)
            .Max();
        if (max <= 0m) max = 1m;

        var count = pv.Points.Count;
        for (var i = 0; i < count; i++)
        {
            var p = pv.Points[i];
            var evVal = ev is not null && i < ev.Points.Count ? ev.Points[i].Value ?? 0m : 0m;
            var acVal = ac is not null && i < ac.Points.Count ? ac.Points[i].Value ?? 0m : 0m;
            var pvVal = p.Value ?? 0m;
            _sCurve.Add(new SCurveBarVm
            {
                Label = p.PeriodStart.ToString("dd/MM"),
                PvHeight = (double)(pvVal / max * 120m),
                EvHeight = (double)(evVal / max * 120m),
                AcHeight = (double)(acVal / max * 120m),
                Tooltip = $"PV={pvVal:N0} EV={evVal:N0} AC={acVal:N0}"
            });
        }
    }

    private async Task CreateSnapshotAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var snap = await _host.Services.ProjectDashboard.CreateSnapshotAsync(_scheduleId, "Snapshot ejecutivo", actor);
            StatusMessage = $"Snapshot v{snap.Version} · SHA {snap.SHA256[..12]}…";
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task CompareAsync()
    {
        if (SnapshotA is null || SnapshotB is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var cmp = await _host.Services.ProjectDashboard.CompareSnapshotsAsync(SnapshotA.SnapshotId, SnapshotB.SnapshotId, actor);
            ComparisonText = string.Join(" · ", cmp.Findings);
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task ExportAsync()
    {
        if (_scheduleId is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            var dash = await _host.Services.ProjectDashboard.GetExecutiveAsync(_scheduleId, null, actor);
            var paths = DashboardExportHelper.ExportAll(dash);
            StatusMessage = "Exportado: " + paths;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task AckAsync()
    {
        if (SelectedAlert is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProjectDashboard.AcknowledgeAlertAsync(SelectedAlert.AlertId, actor, "Reconocida desde dashboard");
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private async Task ResolveAsync()
    {
        if (SelectedAlert is null) return;
        try
        {
            var actor = _host.Session.ProjectInfo.UsuarioNombre ?? "System";
            await _host.Services.ProjectDashboard.ResolveAlertAsync(SelectedAlert.AlertId, actor, "Resuelta");
            await LoadAsync();
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
    }

    private void DrillDown()
    {
        if (SelectedAlert is null) return;
        var route = SelectedAlert.Source switch
        {
            "EVM" or "Forecast" => ShellRoute.PlanningEarnedValue,
            "Progress" => ShellRoute.PlanningProgress,
            "Baseline" => ShellRoute.PlanningBaselines,
            _ => ShellRoute.PlanningProgress
        };
        _shell.RequestNavigate(route);
    }
}
