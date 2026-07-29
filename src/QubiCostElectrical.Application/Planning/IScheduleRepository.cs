using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IScheduleRepository
{
    Task<Schedule?> GetByIdAsync(ScheduleId id, CancellationToken ct = default);
    Task<List<Schedule>> GetByProjectAsync(string projectId, CancellationToken ct = default);
    Task SaveAsync(Schedule schedule, CancellationToken ct = default);
    Task DeleteAsync(ScheduleId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(ScheduleId id, CancellationToken ct = default);
}
