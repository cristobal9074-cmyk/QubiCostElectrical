using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ProjectDashboardService : IProjectDashboardService
{
    private readonly IScheduleRepository _scheduleRepo;
    private readonly IActivityRepository _activityRepo;
    private readonly IWbsPlanningRepository _wbsRepo;
    private readonly IStatusDateRepository _statusDateRepo;
    private readonly IActivityProgressRepository _progressRepo;
    private readonly IScheduleBaselineRepository _baselineRepo;
    private readonly IEarnedValueService _evm;
    private readonly IProgressTrackingService? _progressTracking;
    private readonly IDashboardSnapshotRepository _snapRepo;
    private readonly IDashboardAlertRepository _alertRepo;
    private readonly IDashboardPreferenceRepository _prefRepo;
    private readonly IPlanningAuditRepository _auditRepo;
    private readonly IPlanningPermissionChecker _permissions;

    public ProjectDashboardService(
        IScheduleRepository scheduleRepo,
        IActivityRepository activityRepo,
        IWbsPlanningRepository wbsRepo,
        IStatusDateRepository statusDateRepo,
        IActivityProgressRepository progressRepo,
        IScheduleBaselineRepository baselineRepo,
        IEarnedValueService evm,
        IDashboardSnapshotRepository snapRepo,
        IDashboardAlertRepository alertRepo,
        IDashboardPreferenceRepository prefRepo,
        IPlanningAuditRepository auditRepo,
        IPlanningPermissionChecker permissions,
        IProgressTrackingService? progressTracking = null)
    {
        _scheduleRepo = scheduleRepo ?? throw new ArgumentNullException(nameof(scheduleRepo));
        _activityRepo = activityRepo ?? throw new ArgumentNullException(nameof(activityRepo));
        _wbsRepo = wbsRepo ?? throw new ArgumentNullException(nameof(wbsRepo));
        _statusDateRepo = statusDateRepo ?? throw new ArgumentNullException(nameof(statusDateRepo));
        _progressRepo = progressRepo ?? throw new ArgumentNullException(nameof(progressRepo));
        _baselineRepo = baselineRepo ?? throw new ArgumentNullException(nameof(baselineRepo));
        _evm = evm ?? throw new ArgumentNullException(nameof(evm));
        _snapRepo = snapRepo ?? throw new ArgumentNullException(nameof(snapRepo));
        _alertRepo = alertRepo ?? throw new ArgumentNullException(nameof(alertRepo));
        _prefRepo = prefRepo ?? throw new ArgumentNullException(nameof(prefRepo));
        _auditRepo = auditRepo ?? throw new ArgumentNullException(nameof(auditRepo));
        _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        _progressTracking = progressTracking;
    }

    public async Task<ProjectExecutiveDashboard> GetExecutiveAsync(
        ScheduleId scheduleId, DashboardFilter? filter, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.DashboardExecutiveView, actor, scheduleId, "Dashboard", "Open");
        var dash = await BuildAsync(scheduleId, filter, ct);
        await _alertRepo.ReplaceOpenAlertsAsync(scheduleId, dash.Alerts.Where(a => a.Lifecycle == DashboardAlertLifecycle.Open).ToList(), ct);
        await AuditAsync(scheduleId, "Dashboard", scheduleId.Value, "OpenExecutive", actor, null, dash.DataQuality.ToString(), null, "Ok", ct);
        return dash;
    }

    public async Task<IReadOnlyList<DashboardOperationalRow>> GetOperationalAsync(
        ScheduleId scheduleId, DashboardFilter? filter, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.DashboardView, actor, scheduleId, "Dashboard", "OpenOperational");
        var dash = await BuildAsync(scheduleId, filter, ct);
        return ApplyOperationalFilter(dash.OperationalRows, filter ?? new DashboardFilter());
    }

    public async Task<DashboardSnapshot> CreateSnapshotAsync(ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default)
    {
        Ensure(PlanningPermissions.DashboardSnapshotCreate, actor, scheduleId, "DashboardSnapshot", "Create");
        var dash = await BuildAsync(scheduleId, null, ct);
        var version = await _snapRepo.GetNextVersionAsync(scheduleId, ct);
        var engines = "calc=2;evm=1;dashboard=1";
        var sha = DashboardSnapshotHasher.Compute(dash, engines, dash.SourceHashes);
        var snap = new DashboardSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString("N"),
            ScheduleId = scheduleId,
            Version = version,
            StatusDate = dash.StatusDate ?? DateTime.Today,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            Comment = comment ?? string.Empty,
            SHA256 = sha,
            EngineVersions = engines,
            SourceHashes = dash.SourceHashes,
            Payload = dash
        };
        await _snapRepo.SaveAsync(snap, ct);
        await AuditAsync(scheduleId, "DashboardSnapshot", snap.SnapshotId, "Create", actor, null, sha, null, "Ok", ct);
        return snap;
    }

    public async Task<bool> VerifySnapshotAsync(string snapshotId, CancellationToken ct = default)
    {
        var snap = await _snapRepo.GetByIdAsync(snapshotId, ct);
        if (snap is null) return false;
        var recomputed = DashboardSnapshotHasher.Compute(snap.Payload, snap.EngineVersions, snap.SourceHashes);
        return string.Equals(recomputed, snap.SHA256, StringComparison.OrdinalIgnoreCase);
    }

    public Task<List<DashboardSnapshot>> ListSnapshotsAsync(ScheduleId scheduleId, CancellationToken ct = default) =>
        _snapRepo.GetByScheduleAsync(scheduleId, ct);

    public async Task<DashboardComparison> CompareSnapshotsAsync(string leftId, string rightId, string actor, CancellationToken ct = default)
    {
        var left = await _snapRepo.GetByIdAsync(leftId, ct) ?? throw new InvalidOperationException("Snapshot A no encontrado.");
        var right = await _snapRepo.GetByIdAsync(rightId, ct) ?? throw new InvalidOperationException("Snapshot B no encontrado.");
        Ensure(PlanningPermissions.DashboardSnapshotCompare, actor, left.ScheduleId, "DashboardSnapshot", "Compare");
        var findings = new List<string>();
        var leftSpi = left.Payload.Kpis.FirstOrDefault(k => k.Code == "SPI")?.Value;
        var rightSpi = right.Payload.Kpis.FirstOrDefault(k => k.Code == "SPI")?.Value;
        if (leftSpi.HasValue && rightSpi.HasValue)
            findings.Add($"ΔSPI={(rightSpi.Value - leftSpi.Value).ToString("0.####", CultureInfo.InvariantCulture)}");
        var leftCpi = left.Payload.Kpis.FirstOrDefault(k => k.Code == "CPI")?.Value;
        var rightCpi = right.Payload.Kpis.FirstOrDefault(k => k.Code == "CPI")?.Value;
        if (leftCpi.HasValue && rightCpi.HasValue)
            findings.Add($"ΔCPI={(rightCpi.Value - leftCpi.Value).ToString("0.####", CultureInfo.InvariantCulture)}");
        await AuditAsync(left.ScheduleId, "DashboardSnapshot", leftId, "Compare", actor, leftId, rightId, null, "Ok", ct);
        return new DashboardComparison
        {
            LeftLabel = $"v{left.Version}",
            RightLabel = $"v{right.Version}",
            LeftKpis = left.Payload.Kpis,
            RightKpis = right.Payload.Kpis,
            Findings = findings
        };
    }

    public Task<List<DashboardAlert>> GetAlertsAsync(ScheduleId scheduleId, CancellationToken ct = default) =>
        _alertRepo.GetByScheduleAsync(scheduleId, ct);

    public async Task AcknowledgeAlertAsync(string alertId, string actor, string? note, CancellationToken ct = default) =>
        await SetLifecycleAsync(alertId, actor, DashboardAlertLifecycle.Acknowledged, note, ct);

    public async Task ResolveAlertAsync(string alertId, string actor, string? note, CancellationToken ct = default) =>
        await SetLifecycleAsync(alertId, actor, DashboardAlertLifecycle.Resolved, note, ct);

    public async Task DismissAlertAsync(string alertId, string actor, string reason, CancellationToken ct = default) =>
        await SetLifecycleAsync(alertId, actor, DashboardAlertLifecycle.Dismissed, reason, ct);

    public async Task SavePreferencesAsync(DashboardUserPreferences prefs, string actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prefs);
        if (prefs.ScheduleId is null) throw new InvalidOperationException("ScheduleId requerido.");
        Ensure(PlanningPermissions.DashboardConfigureThresholds, actor, prefs.ScheduleId, "DashboardPrefs", "Save");
        prefs = new DashboardUserPreferences
        {
            UserKey = prefs.UserKey,
            ScheduleId = prefs.ScheduleId,
            Filter = prefs.Filter,
            Thresholds = prefs.Thresholds,
            UpdatedUtc = DateTime.UtcNow
        };
        await _prefRepo.SaveAsync(prefs, ct);
        await AuditAsync(prefs.ScheduleId, "DashboardPrefs", prefs.UserKey, "SaveThresholds", actor, null, null, null, "Ok", ct);
    }

    public Task<DashboardUserPreferences?> GetPreferencesAsync(string userKey, ScheduleId scheduleId, CancellationToken ct = default) =>
        _prefRepo.GetAsync(userKey, scheduleId, ct);
}
