using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IActivityDependencyRepository
{
    Task<ActivityDependency?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<ActivityDependency>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<List<ActivityDependency>> GetByPredecessorAsync(ActivityId activityId, CancellationToken ct = default);
    Task<List<ActivityDependency>> GetBySuccessorAsync(ActivityId activityId, CancellationToken ct = default);
    Task SaveAsync(ActivityDependency dependency, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
}
