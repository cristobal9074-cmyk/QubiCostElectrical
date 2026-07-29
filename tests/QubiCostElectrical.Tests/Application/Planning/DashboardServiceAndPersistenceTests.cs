using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Planning;

public sealed class DashboardServiceAndPersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ScheduleId _sid = ScheduleId.New();
    private readonly CalendarId _cal = CalendarId.New();
    private readonly FakeScheduleRepo _schedules = new();
    private readonly FakeActivityRepo _activities = new();
    private readonly FakeWbsRepo _wbs = new();
    private readonly FakeProgressRepo _progress = new();
    private readonly FakeStatusDateRepo _status = new();
    private readonly FakeBaselineRepo _baselines = new();
    private readonly FakeEvm _evm;
    private readonly FakeAuditRepo _audit = new();

    public DashboardServiceAndPersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dash_{Guid.NewGuid():N}.db");
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        _evm = new FakeEvm(_sid);
        _schedules.Items[_sid.Value] = new Schedule(_sid, "Dash", "UTC", _cal, "t") { TargetEndDate = DateTime.Today.AddDays(30) };
        _status.Items[_sid.Value] = new ScheduleStatusDate
        {
            Id = "1", ScheduleId = _sid, StatusDate = DateTime.Today,
            TimeZoneId = "UTC", UpdatedUtc = DateTime.UtcNow, UpdatedBy = "t", RowVersion = "1"
        };
        _baselines.Items.Add(new ScheduleBaseline
        {
            BaselineId = "b1", ScheduleId = _sid, Version = 0, IsOfficial = true, Name = "BL",
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", SHA256 = "x", SnapshotDate = DateTime.UtcNow
        });
        var actId = ActivityId.New();
        _progress.ByActivity[actId.Value] = new ActivityProgressUpdate
        {
            ProgressUpdateId = "p1", ScheduleId = _sid, ActivityId = actId,
            StatusDate = DateTime.Today, PercentComplete = 40m, ActualCost = 100m,
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedUtc = DateTime.UtcNow, ModifiedBy = "t", RowVersion = "1"
        };
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private ProjectDashboardService Create(IPlanningPermissionChecker? perms = null) =>
        new(_schedules, _activities, _wbs, _status, _progress, _baselines, _evm,
            new SqliteDashboardSnapshotRepository(_dbPath),
            new SqliteDashboardAlertRepository(_dbPath),
            new SqliteDashboardPreferenceRepository(_dbPath),
            _audit, perms ?? new AllowAllPlanningPermissionChecker());

    [Fact]
    public async Task Executive_builds_kpis()
    {
        var dash = await Create().GetExecutiveAsync(_sid, null, "u");
        Assert.Contains(dash.Kpis, k => k.Code == "SPI");
        Assert.Equal(DashboardDataQuality.Complete, dash.DataQuality);
        Assert.NotEmpty(dash.Series);
    }

    [Fact]
    public async Task Snapshot_roundtrip_and_verify()
    {
        var svc = Create();
        var snap = await svc.CreateSnapshotAsync(_sid, "c1", "u");
        Assert.True(await svc.VerifySnapshotAsync(snap.SnapshotId));
        Assert.Single(await svc.ListSnapshotsAsync(_sid));
    }

    [Fact]
    public async Task Compare_snapshots()
    {
        var svc = Create();
        var a = await svc.CreateSnapshotAsync(_sid, "a", "u");
        _evm.Bump();
        var b = await svc.CreateSnapshotAsync(_sid, "b", "u");
        var cmp = await svc.CompareSnapshotsAsync(a.SnapshotId, b.SnapshotId, "u");
        Assert.NotNull(cmp);
    }

    [Fact]
    public async Task Alert_lifecycle()
    {
        var svc = Create();
        var dash = await svc.GetExecutiveAsync(_sid, null, "u");
        var alert = dash.Alerts.First();
        await svc.AcknowledgeAlertAsync(alert.AlertId, "u", "ok");
        Assert.Equal(DashboardAlertLifecycle.Acknowledged, (await svc.GetAlertsAsync(_sid)).First(a => a.AlertId == alert.AlertId).Lifecycle);
        await svc.ResolveAlertAsync(alert.AlertId, "u", "done");
        Assert.Equal(DashboardAlertLifecycle.Resolved, (await svc.GetAlertsAsync(_sid)).First(a => a.AlertId == alert.AlertId).Lifecycle);
        await svc.DismissAlertAsync(alert.AlertId, "u", "n/a");
        Assert.Equal(DashboardAlertLifecycle.Dismissed, (await svc.GetAlertsAsync(_sid)).First(a => a.AlertId == alert.AlertId).Lifecycle);
    }

    [Fact]
    public async Task Preferences_roundtrip()
    {
        var svc = Create();
        await svc.SavePreferencesAsync(new DashboardUserPreferences
        {
            UserKey = "u1", ScheduleId = _sid,
            Thresholds = new DashboardTrafficLightThresholds { SpiAmberMin = 0.85m },
            Filter = new DashboardFilter { Granularity = DashboardSeriesGranularity.Monthly }
        }, "u1");
        var loaded = await svc.GetPreferencesAsync("u1", _sid);
        Assert.Equal(0.85m, loaded!.Thresholds.SpiAmberMin);
    }

    [Fact]
    public async Task Denied_without_permission()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Create(new DenyAllPlanningPermissionChecker()).GetExecutiveAsync(_sid, null, "u"));
    }

    [Fact]
    public async Task Operational_returns_rows()
    {
        var rows = await Create().GetOperationalAsync(_sid, new DashboardFilter { CriticalOnly = true }, "u");
        Assert.NotNull(rows);
    }

    [Fact]
    public void Migration_v40_tables()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, Convert.ToInt32(cmd.ExecuteScalar()));
        foreach (var t in new[] { "planning_dashboard_snapshots", "planning_dashboard_alerts", "planning_dashboard_preferences" })
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{t}';";
            Assert.Equal(1, Convert.ToInt32(cmd.ExecuteScalar()));
        }
    }

    [Theory]
    [InlineData(nameof(PlanningPermissions.DashboardView), "Planning.DashboardView")]
    [InlineData(nameof(PlanningPermissions.DashboardExecutiveView), "Planning.DashboardExecutiveView")]
    [InlineData(nameof(PlanningPermissions.DashboardSnapshotCreate), "Planning.DashboardSnapshotCreate")]
    [InlineData(nameof(PlanningPermissions.DashboardSnapshotCompare), "Planning.DashboardSnapshotCompare")]
    [InlineData(nameof(PlanningPermissions.DashboardAlertManage), "Planning.DashboardAlertManage")]
    [InlineData(nameof(PlanningPermissions.DashboardExport), "Planning.DashboardExport")]
    [InlineData(nameof(PlanningPermissions.DashboardConfigureThresholds), "Planning.DashboardConfigureThresholds")]
    public void Dashboard_permission_constants(string field, string expected) =>
        Assert.Equal(expected, typeof(PlanningPermissions).GetField(field)!.GetValue(null));

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void Series_scale_deterministic(int n)
    {
        var raw = Enumerable.Range(0, n).Select(i => (new DateTime(2026, 1, 1).AddDays(i % 365), 1m)).ToList();
        var a = DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.PvCumulative, "PV", raw,
            DashboardSeriesGranularity.Daily, DashboardSeriesMode.Cumulative, DashboardSeriesUnit.Cost, "CLP", null, null);
        Assert.Equal(a.Points.Sum(p => DashboardSeriesMode.Period == DashboardSeriesMode.Cumulative ? 0 : 0) + a.Points[^1].Value, a.Points[^1].Value);
        Assert.True(a.Points[^1].Value >= 1m);
    }

    [Theory]
    [InlineData(DashboardSeriesGranularity.Daily, DashboardSeriesMode.Cumulative)]
    [InlineData(DashboardSeriesGranularity.Weekly, DashboardSeriesMode.Cumulative)]
    [InlineData(DashboardSeriesGranularity.Monthly, DashboardSeriesMode.Cumulative)]
    [InlineData(DashboardSeriesGranularity.Daily, DashboardSeriesMode.Period)]
    [InlineData(DashboardSeriesGranularity.Weekly, DashboardSeriesMode.Period)]
    [InlineData(DashboardSeriesGranularity.Monthly, DashboardSeriesMode.Period)]
    public void Series_modes_matrix(DashboardSeriesGranularity g, DashboardSeriesMode m)
    {
        var raw = new[] { (new DateTime(2026, 1, 1), 3m), (new DateTime(2026, 1, 8), 2m) };
        var s = DashboardSeriesBuilder.BuildFromHistory(DashboardSeriesKind.EvCumulative, "EV", raw, g, m, DashboardSeriesUnit.Cost, "CLP", null, null);
        Assert.NotEmpty(s.Points);
    }

    [Fact]
    public void Export_helper_csv_xlsx_pdf()
    {
        var dash = new ProjectExecutiveDashboard
        {
            ScheduleId = _sid,
            ProjectName = "P",
            StatusDate = DateTime.Today,
            DataQuality = DashboardDataQuality.Complete,
            Kpis = new[]
            {
                new DashboardKpi { Code = "SPI", Title = "SPI", DisplayValue = "1", Status = DashboardKpiStatus.Good, Interpretation = "ok" }
            },
            Alerts = Array.Empty<DashboardAlert>(),
            Deviations = Array.Empty<DashboardDeviationItem>(),
            WbsNodes = Array.Empty<DashboardWbsNode>(),
            Series = Array.Empty<DashboardSeries>(),
            ByResponsible = Array.Empty<DashboardResponsibleSummary>(),
            OperationalRows = Array.Empty<DashboardOperationalRow>(),
            ExecutiveSummary = "Resumen"
        };
        var paths = QubiCostElectrical.Wpf.Features.ExecutiveDashboard.DashboardExportHelper.ExportAll(dash);
        Assert.Contains(".csv", paths);
        Assert.Contains(".xlsx", paths);
        Assert.Contains(".pdf", paths);
    }

    private sealed class FakeEvm : IEarnedValueService
    {
        private readonly ScheduleId _sid;
        private decimal _spi = 0.85m;
        public FakeEvm(ScheduleId sid) => _sid = sid;
        public void Bump() => _spi = 0.95m;
        private EarnedValueResult Result() => EarnedValueEngine.Calculate(new EarnedValueInput
        {
            ScheduleId = _sid, StatusDate = DateTime.Today,
            Bac = 1000m, Pv = 500m, Ev = 500m * _spi, Ac = 480m,
            HasBaseline = true, HasStatusDate = true, HasBudget = true, HasProgress = true
        });
        public Task<EarnedValueResult> CalculateAsync(ScheduleId scheduleId, string actor, decimal? projectBac = null, EacMethod? eacMethod = null, CancellationToken ct = default) => Task.FromResult(Result());
        public Task<EarnedValueComparison> CompareSnapshotsAsync(string snapshotIdA, string snapshotIdB, string actor, CancellationToken ct = default) => Task.FromResult(new EarnedValueComparison());
        public Task<EarnedValueComparison> CompareResultsAsync(EarnedValueResult left, EarnedValueResult right, string leftLabel, string rightLabel, string actor, CancellationToken ct = default) => Task.FromResult(new EarnedValueComparison());
        public Task<EarnedValueSnapshot> CreateSnapshotAsync(ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> VerifySnapshotIntegrityAsync(string snapshotId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<List<EarnedValueSnapshot>> ListSnapshotsAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(new List<EarnedValueSnapshot>());
        public Task<List<EarnedValueHistoryEntry>> GetHistoryAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default) =>
            Task.FromResult(new List<EarnedValueHistoryEntry>
            {
                new() { EntryId = "1", ScheduleId = _sid, StatusDate = DateTime.Today.AddDays(-7), RecordedUtc = DateTime.UtcNow, RecordedBy = "t", Pv = 300m, Ev = 250m, Ac = 280m, Bac = 1000m, Spi = 0.8m, Cpi = 0.9m, Eac = 1100m },
                new() { EntryId = "2", ScheduleId = _sid, StatusDate = DateTime.Today, RecordedUtc = DateTime.UtcNow, RecordedBy = "t", Pv = 500m, Ev = 500m * _spi, Ac = 480m, Bac = 1000m, Spi = _spi, Cpi = 0.95m, Eac = 1050m }
            });
        public Task<EarnedValueResult?> GetLatestAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult<EarnedValueResult?>(Result());
    }

    private sealed class FakeScheduleRepo : IScheduleRepository
    {
        public Dictionary<string, Schedule> Items { get; } = new();
        public Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default) => Task.FromResult(Items.TryGetValue(id.Value, out var s) ? s : null);
        public Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default) => Task.FromResult(Items.Values.ToList());
        public Task SaveAsync(Schedule schedule, CancellationToken ct = default) { Items[schedule.Id.Value] = schedule; return Task.CompletedTask; }
        public Task DeleteAsync(ScheduleId id, CancellationToken ct = default) { Items.Remove(id.Value); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(ScheduleId id, CancellationToken ct = default) => Task.FromResult(Items.ContainsKey(id.Value));
    }

    private sealed class FakeActivityRepo : IActivityRepository
    {
        public Dictionary<string, Activity> Items { get; } = new();
        public Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default) => Task.FromResult(Items.TryGetValue(id.Value, out var a) ? a : null);
        public Task<List<Activity>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(Items.Values.Where(a => a.ScheduleId == scheduleId).ToList());
        public Task<List<Activity>> GetByWbsNodeAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default) => Task.FromResult(Items.Values.Where(a => a.WbsNodeId == wbsNodeId).ToList());
        public Task<ActivitySearchResult> SearchAsync(ActivitySearchRequest request, CancellationToken ct = default) => Task.FromResult(new ActivitySearchResult { Activities = Items.Values.ToList(), TotalCount = Items.Count });
        public Task SaveAsync(Activity activity, CancellationToken ct = default) { Items[activity.Id.Value] = activity; return Task.CompletedTask; }
        public Task SaveBatchAsync(List<Activity> activities, CancellationToken ct = default) { foreach (var a in activities) Items[a.Id.Value] = a; return Task.CompletedTask; }
        public Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default) { Items.Remove(id.Value); return Task.CompletedTask; }
        public Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult("A1");
    }

    private sealed class FakeWbsRepo : IWbsPlanningRepository
    {
        public Task<WbsPlanningNode?> GetByIdAsync(WbsPlanningNodeId id, CancellationToken ct = default) => Task.FromResult<WbsPlanningNode?>(null);
        public Task<List<WbsPlanningNode>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(new List<WbsPlanningNode>());
        public Task SaveAsync(WbsPlanningNode node, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveBatchAsync(List<WbsPlanningNode> nodes, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(WbsPlanningNodeId id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CodeExistsAsync(ScheduleId scheduleId, string wbsCode, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> GetMaxSortOrderAsync(ScheduleId scheduleId, WbsPlanningNodeId? parentId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeProgressRepo : IActivityProgressRepository
    {
        public Dictionary<string, ActivityProgressUpdate> ByActivity { get; } = new();
        public Task<ActivityProgressUpdate?> GetByIdAsync(string progressUpdateId, CancellationToken ct = default) => Task.FromResult(ByActivity.Values.FirstOrDefault(p => p.ProgressUpdateId == progressUpdateId));
        public Task<ActivityProgressUpdate?> GetByActivityAsync(ScheduleId scheduleId, ActivityId activityId, CancellationToken ct = default) => Task.FromResult(ByActivity.TryGetValue(activityId.Value, out var p) ? p : null);
        public Task<List<ActivityProgressUpdate>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(ByActivity.Values.Where(p => p.ScheduleId == scheduleId).ToList());
        public Task SaveAsync(ActivityProgressUpdate update, CancellationToken ct = default) { ByActivity[update.ActivityId.Value] = update; return Task.CompletedTask; }
        public Task DeleteAsync(string progressUpdateId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeStatusDateRepo : IStatusDateRepository
    {
        public Dictionary<string, ScheduleStatusDate> Items { get; } = new();
        public Task<ScheduleStatusDate?> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(Items.TryGetValue(scheduleId.Value, out var s) ? s : null);
        public Task SaveAsync(ScheduleStatusDate statusDate, CancellationToken ct = default) { Items[statusDate.ScheduleId.Value] = statusDate; return Task.CompletedTask; }
    }

    private sealed class FakeBaselineRepo : IScheduleBaselineRepository
    {
        public List<ScheduleBaseline> Items { get; } = new();
        public Task<ScheduleBaseline?> GetByIdAsync(string baselineId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(b => b.BaselineId == baselineId));
        public Task<List<ScheduleBaseline>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(Items.Where(b => b.ScheduleId == scheduleId).ToList());
        public Task SaveAsync(ScheduleBaseline baseline, CancellationToken ct = default) { Items.Add(baseline); return Task.CompletedTask; }
        public Task DeleteAsync(string baselineId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> ExistsAsync(string baselineId, CancellationToken ct = default) => Task.FromResult(Items.Any(b => b.BaselineId == baselineId));
        public Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeAuditRepo : IPlanningAuditRepository
    {
        public List<PlanningAuditEvent> Events { get; } = new();
        public Task RecordAsync(PlanningAuditEvent auditEvent, CancellationToken ct = default) { Events.Add(auditEvent); return Task.CompletedTask; }
        public Task<List<PlanningAuditEvent>> GetByScheduleAsync(ScheduleId scheduleId, int limit = 100, CancellationToken ct = default) => Task.FromResult(Events.Where(e => e.ScheduleId == scheduleId.Value).Take(limit).ToList());
        public Task<List<PlanningAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 50, CancellationToken ct = default) => Task.FromResult(Events.Take(limit).ToList());
    }

    // DenyAllPlanningPermissionChecker is defined in Application
}
