using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface ICpmCalculationService
{
    Task<CpmResult> CalculateScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
