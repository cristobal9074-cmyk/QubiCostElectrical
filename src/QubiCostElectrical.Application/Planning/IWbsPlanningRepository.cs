using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IWbsPlanningRepository
{
    Task<WbsPlanningNode?> GetByIdAsync(WbsPlanningNodeId id, CancellationToken ct = default);
    Task<List<WbsPlanningNode>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task SaveAsync(WbsPlanningNode node, CancellationToken ct = default);
    Task SaveBatchAsync(List<WbsPlanningNode> nodes, CancellationToken ct = default);
    Task DeleteAsync(WbsPlanningNodeId id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(ScheduleId scheduleId, string wbsCode, CancellationToken ct = default);
    Task<int> GetMaxSortOrderAsync(ScheduleId scheduleId, WbsPlanningNodeId? parentId, CancellationToken ct = default);
}
