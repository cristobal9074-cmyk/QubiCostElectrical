using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IActivityRepository
{
    Task<Activity?> GetByIdAsync(ActivityId id, CancellationToken ct = default);
    Task<List<Activity>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<List<Activity>> GetByWbsNodeAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default);
    Task<ActivitySearchResult> SearchAsync(ActivitySearchRequest request, CancellationToken ct = default);
    Task SaveAsync(Activity activity, CancellationToken ct = default);
    Task SaveBatchAsync(List<Activity> activities, CancellationToken ct = default);
    Task DeleteAsync(ActivityId id, bool softDelete = true, CancellationToken ct = default);
    Task<string> GetNextCodeAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
