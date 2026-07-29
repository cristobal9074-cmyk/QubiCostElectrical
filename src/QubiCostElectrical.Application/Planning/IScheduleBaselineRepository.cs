using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IScheduleBaselineRepository
{
    Task<ScheduleBaseline?> GetByIdAsync(string baselineId, CancellationToken ct = default);
    Task<List<ScheduleBaseline>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SaveAsync(ScheduleBaseline baseline, CancellationToken ct = default);
    Task DeleteAsync(string baselineId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string baselineId, CancellationToken ct = default);
    Task<int> GetNextVersionAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
