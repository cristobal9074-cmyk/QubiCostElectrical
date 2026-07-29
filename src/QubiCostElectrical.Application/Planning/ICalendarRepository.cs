using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface ICalendarRepository
{
    Task<Calendar?> GetByIdAsync(CalendarId id, CancellationToken ct = default);
    Task<List<Calendar>> GetAllAsync(CancellationToken ct = default);
    Task<List<Calendar>> GetByProjectAsync(string projectId, CancellationToken ct = default);
    Task SaveAsync(Calendar calendar, CancellationToken ct = default);
    Task DeleteAsync(CalendarId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(CalendarId id, CancellationToken ct = default);
}
