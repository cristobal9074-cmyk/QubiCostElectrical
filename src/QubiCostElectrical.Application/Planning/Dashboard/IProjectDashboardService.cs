using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProjectDashboardService
{
    Task<ProjectExecutiveDashboard> GetExecutiveAsync(ScheduleId scheduleId, DashboardFilter? filter, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<DashboardOperationalRow>> GetOperationalAsync(ScheduleId scheduleId, DashboardFilter? filter, string actor, CancellationToken ct = default);
    Task<DashboardSnapshot> CreateSnapshotAsync(ScheduleId scheduleId, string comment, string actor, CancellationToken ct = default);
    Task<bool> VerifySnapshotAsync(string snapshotId, CancellationToken ct = default);
    Task<List<DashboardSnapshot>> ListSnapshotsAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<DashboardComparison> CompareSnapshotsAsync(string leftId, string rightId, string actor, CancellationToken ct = default);
    Task AcknowledgeAlertAsync(string alertId, string actor, string? note, CancellationToken ct = default);
    Task ResolveAlertAsync(string alertId, string actor, string? note, CancellationToken ct = default);
    Task DismissAlertAsync(string alertId, string actor, string reason, CancellationToken ct = default);
    Task<List<DashboardAlert>> GetAlertsAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SavePreferencesAsync(DashboardUserPreferences prefs, string actor, CancellationToken ct = default);
    Task<DashboardUserPreferences?> GetPreferencesAsync(string userKey, ScheduleId scheduleId, CancellationToken ct = default);
}

public interface IDashboardSnapshotRepository
{
    Task SaveAsync(DashboardSnapshot snapshot, CancellationToken ct = default);
    Task<DashboardSnapshot?> GetByIdAsync(string snapshotId, CancellationToken ct = default);
    Task<List<DashboardSnapshot>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default);
}

public interface IDashboardAlertRepository
{
    Task ReplaceOpenAlertsAsync(ScheduleId scheduleId, IReadOnlyList<DashboardAlert> alerts, CancellationToken ct = default);
    Task SaveAsync(DashboardAlert alert, CancellationToken ct = default);
    Task<DashboardAlert?> GetByIdAsync(string alertId, CancellationToken ct = default);
    Task<List<DashboardAlert>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
}

public interface IDashboardPreferenceRepository
{
    Task SaveAsync(DashboardUserPreferences prefs, CancellationToken ct = default);
    Task<DashboardUserPreferences?> GetAsync(string userKey, ScheduleId scheduleId, CancellationToken ct = default);
}
