using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IScheduleService
{
    Task<Schedule> CreateScheduleAsync(string name, string timeZoneId, CalendarId defaultCalendarId, string createdBy, CancellationToken ct = default);
    Task<Schedule?> GetScheduleAsync(ScheduleId id, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> UpdateScheduleAsync(Schedule schedule, string modifiedBy, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> DeleteScheduleAsync(ScheduleId id, CancellationToken ct = default);
    Task<Schedule?> GetCurrentProjectScheduleAsync(CancellationToken ct = default);

    Task<WbsPlanningNode> CreateWbsNodeAsync(ScheduleId scheduleId, string wbsCode, string name, WbsNodeType nodeType, WbsPlanningNodeId? parentId, string modifiedBy, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> MoveWbsNodeAsync(WbsPlanningNodeId nodeId, WbsPlanningNodeId? newParentId, int newSortOrder, string modifiedBy, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> DeleteWbsNodeAsync(WbsPlanningNodeId nodeId, string modifiedBy, CancellationToken ct = default);
    Task<List<WbsPlanningNode>> GetWbsTreeAsync(ScheduleId scheduleId, CancellationToken ct = default);

    Task<Activity> CreateActivityAsync(ScheduleId scheduleId, WbsPlanningNodeId wbsNodeId, string code, string name, ActivityType type, PlanningDuration duration, string author, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> UpdateActivityAsync(Activity activity, string lastEditor, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> DeleteActivityAsync(ActivityId id, string lastEditor, CancellationToken ct = default);
    Task<Activity?> GetActivityAsync(ActivityId id, CancellationToken ct = default);
    Task<List<Activity>> GetActivitiesByWbsAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default);
    Task<ActivitySearchResult> SearchActivitiesAsync(ActivitySearchRequest request, CancellationToken ct = default);

    Task<Calendar> CreateCalendarAsync(string code, string name, string timeZoneId, CalendarType calendarType, string author, CancellationToken ct = default);
    Task<List<PlanningValidationIssue>> UpdateCalendarAsync(Calendar calendar, CancellationToken ct = default);
    Task<Calendar?> GetCalendarAsync(CalendarId id, CancellationToken ct = default);
    Task<List<Calendar>> GetAllCalendarsAsync(CancellationToken ct = default);

    Task<List<PlanningValidationIssue>> ValidateScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default);
    Task<ScheduleSummary> GetScheduleSummaryAsync(ScheduleId scheduleId, CancellationToken ct = default);
}
