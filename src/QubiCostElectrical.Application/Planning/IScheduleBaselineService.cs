using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IScheduleBaselineService
{
    Task<ScheduleBaseline> CreateBaselineAsync(ScheduleId scheduleId, string name, string description, string createdBy, CancellationToken ct = default);
    Task RestoreBaselineAsync(string baselineId, string restoredBy, CancellationToken ct = default);
    Task<ScheduleComparisonResult> CompareBaselinesAsync(string baselineIdA, string baselineIdB, CancellationToken ct = default);
    Task<ScheduleComparisonResult> CompareWithCurrentAsync(ScheduleId scheduleId, string baselineId, CancellationToken ct = default);
    Task<bool> VerifyIntegrityAsync(string baselineId, CancellationToken ct = default);
    Task<List<ScheduleBaseline>> ListBaselinesAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task DeleteDraftBaselineAsync(string baselineId, CancellationToken ct = default);
    Task MarkOfficialAsync(string baselineId, CancellationToken ct = default);
}
