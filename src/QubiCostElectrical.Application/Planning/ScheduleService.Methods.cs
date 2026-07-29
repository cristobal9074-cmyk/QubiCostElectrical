using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ScheduleService
{
    public async Task<Activity> CreateActivityAsync(ScheduleId scheduleId, WbsPlanningNodeId wbsNodeId, string code, string name, ActivityType type, PlanningDuration duration, string author, CancellationToken ct = default)
    {
        if (scheduleId == null) throw new ArgumentNullException(nameof(scheduleId));
        if (wbsNodeId == null) throw new ArgumentNullException(nameof(wbsNodeId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentNullException(nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (duration == null) throw new ArgumentNullException(nameof(duration));
        if (string.IsNullOrWhiteSpace(author)) throw new ArgumentNullException(nameof(author));

        if (type == ActivityType.Milestone && duration.Value != 0)
        {
            duration = PlanningDuration.Zero(type);
        }

        var activity = new Activity(ActivityId.New(), scheduleId, wbsNodeId, code, name, type, duration, author);
        await activityRepo.SaveAsync(activity, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = author,
            ScheduleId = scheduleId.ToString(),
            EntityType = "Activity",
            EntityId = activity.Id.ToString(),
            Action = "Create",
            RelevantValues = $"{{\"code\":\"{code}\",\"type\":\"{type}\"}}",
            Result = "Success"
        }, ct);
        return activity;
    }

    public async Task<List<PlanningValidationIssue>> UpdateActivityAsync(Activity activity, string lastEditor, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        if (activity == null) throw new ArgumentNullException(nameof(activity));
        if (string.IsNullOrWhiteSpace(lastEditor)) throw new ArgumentNullException(nameof(lastEditor));

        var existing = await activityRepo.GetByIdAsync(activity.Id, ct);
        if (existing == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ActivityNotFound,
                "La actividad no existe.",
                PlanningValidationSeverity.Error,
                "Activity", "Id", activity.Id.ToString()));
            return issues;
        }

        if (existing.IsDeleted)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ActivityNotFound,
                "No se puede modificar una actividad eliminada.",
                PlanningValidationSeverity.Error,
                "Activity", "Id", activity.Id.ToString()));
            return issues;
        }

        if (activity.Status != existing.Status && !ActivityStatusTransitions.IsValid(existing.Status, activity.Status))
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.InvalidActivityStatusTransition,
                $"Transición de estado inválida de {existing.Status} a {activity.Status}.",
                PlanningValidationSeverity.Error,
                "Activity", "Status", activity.Id.ToString()));
            return issues;
        }

        if (activity.Type == ActivityType.Milestone && activity.PlannedDuration != null && activity.PlannedDuration.Value > 0)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.MilestoneDurationNotZero,
                "Un hito debe tener duración cero.",
                PlanningValidationSeverity.Warning,
                "Activity", "PlannedDuration", activity.Id.ToString()));
        }

        activity.LastEditor = lastEditor;
        activity.ModifiedAt = DateTime.UtcNow;
        await activityRepo.SaveAsync(activity, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = lastEditor,
            ScheduleId = activity.ScheduleId.ToString(),
            EntityType = "Activity",
            EntityId = activity.Id.ToString(),
            Action = "Update",
            PreviousState = existing.RowVersion,
            Result = issues.Count == 0 ? "Success" : "SuccessWithWarnings"
        }, ct);
        return issues;
    }

    public async Task<List<PlanningValidationIssue>> DeleteActivityAsync(ActivityId id, string lastEditor, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        if (id == null) throw new ArgumentNullException(nameof(id));

        var existing = await activityRepo.GetByIdAsync(id, ct);
        if (existing == null || existing.IsDeleted)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ActivityNotFound,
                "La actividad no existe o ya fue eliminada.",
                PlanningValidationSeverity.Error,
                "Activity", "Id", id.ToString()));
            return issues;
        }

        await activityRepo.DeleteAsync(id, true, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = lastEditor,
            ScheduleId = existing.ScheduleId.ToString(),
            EntityType = "Activity",
            EntityId = id.ToString(),
            Action = "Delete",
            PreviousState = existing.Code,
            Result = "Success"
        }, ct);
        return issues;
    }

    public async Task<Activity?> GetActivityAsync(ActivityId id, CancellationToken ct = default)
    {
        return await activityRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<Activity>> GetActivitiesByWbsAsync(WbsPlanningNodeId wbsNodeId, CancellationToken ct = default)
    {
        return await activityRepo.GetByWbsNodeAsync(wbsNodeId, ct);
    }

    public async Task<ActivitySearchResult> SearchActivitiesAsync(ActivitySearchRequest request, CancellationToken ct = default)
    {
        return await activityRepo.SearchAsync(request, ct);
    }

    public async Task<Calendar> CreateCalendarAsync(string code, string name, string timeZoneId, CalendarType calendarType, string author, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentNullException(nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(author)) throw new ArgumentNullException(nameof(author));

        var calendar = new Calendar(CalendarId.New(), code, name, timeZoneId, author, calendarType);
        await calendarRepo.SaveAsync(calendar, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = author,
            EntityType = "Calendar",
            EntityId = calendar.Id.ToString(),
            Action = "Create",
            RelevantValues = $"{{\"code\":\"{code}\",\"calendarType\":\"{calendarType}\"}}",
            Result = "Success"
        }, ct);
        return calendar;
    }

    public async Task<List<PlanningValidationIssue>> UpdateCalendarAsync(Calendar calendar, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        if (calendar == null) throw new ArgumentNullException(nameof(calendar));

        var existing = await calendarRepo.GetByIdAsync(calendar.Id, ct);
        if (existing == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.CalendarNotFound,
                "El calendario no existe.",
                PlanningValidationSeverity.Error,
                "Calendar", "Id", calendar.Id.ToString()));
            return issues;
        }

        for (int i = 0; i < calendar.WeeklyPattern.Count; i++)
        {
            var day = calendar.WeeklyPattern[i];
            if (day.IsWorkingDay && day.Shifts.Count == 0)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationCode.InvalidWorkingInterval,
                    $"El día {day.DayOfWeek} está marcado como laboral pero no tiene turnos definidos.",
                    PlanningValidationSeverity.Warning,
                    "Calendar", "WeeklyPattern", calendar.Id.ToString()));
            }

            for (int j = 0; j < day.Shifts.Count; j++)
            {
                for (int k = j + 1; k < day.Shifts.Count; k++)
                {
                    if (day.Shifts[j].StartTime < day.Shifts[k].EndTime && day.Shifts[k].StartTime < day.Shifts[j].EndTime)
                    {
                        issues.Add(new PlanningValidationIssue(
                            PlanningValidationCode.CalendarOverlap,
                            $"Turnos solapados en {day.DayOfWeek}: {day.Shifts[j].StartTime}-{day.Shifts[j].EndTime} y {day.Shifts[k].StartTime}-{day.Shifts[k].EndTime}.",
                            PlanningValidationSeverity.Warning,
                            "Calendar", "Shifts", calendar.Id.ToString()));
                    }
                }
            }
        }

        calendar.ModifiedAt = DateTime.UtcNow;
        await calendarRepo.SaveAsync(calendar, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = calendar.Author,
            EntityType = "Calendar",
            EntityId = calendar.Id.ToString(),
            Action = "Update",
            PreviousState = existing.RowVersion,
            Result = issues.Count == 0 ? "Success" : "SuccessWithWarnings"
        }, ct);
        return issues;
    }

    public async Task<Calendar?> GetCalendarAsync(CalendarId id, CancellationToken ct = default)
    {
        return await calendarRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<Calendar>> GetAllCalendarsAsync(CancellationToken ct = default)
    {
        return await calendarRepo.GetAllAsync(ct);
    }

    public async Task<List<PlanningValidationIssue>> ValidateScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        var schedule = await scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ScheduleNotFound,
                "El cronograma no existe.",
                PlanningValidationSeverity.Error,
                "Schedule", "Id", scheduleId.ToString()));
            return issues;
        }

        if (string.IsNullOrWhiteSpace(schedule.Name))
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ScheduleNotFound,
                "El nombre del cronograma no puede estar vacío.",
                PlanningValidationSeverity.Error,
                "Schedule", "Name", scheduleId.ToString()));
        }

        if (string.IsNullOrWhiteSpace(schedule.TimeZoneId))
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.InvalidTimeZone,
                "La zona horaria del cronograma no está definida.",
                PlanningValidationSeverity.Error,
                "Schedule", "TimeZoneId", scheduleId.ToString()));
        }

        var calendarExists = await calendarRepo.ExistsAsync(schedule.DefaultCalendarId, ct);
        if (!calendarExists)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.CalendarNotFound,
                "El calendario por defecto asociado al cronograma no existe.",
                PlanningValidationSeverity.Error,
                "Schedule", "DefaultCalendarId", scheduleId.ToString()));
        }

        if (schedule.ProjectStartDate.HasValue && schedule.TargetEndDate.HasValue && schedule.ProjectStartDate > schedule.TargetEndDate)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.InvalidConstraint,
                "La fecha de inicio del proyecto no puede ser posterior a la fecha de término estimada.",
                PlanningValidationSeverity.Error,
                "Schedule", "ProjectStartDate", scheduleId.ToString()));
        }

        return issues;
    }

    public async Task<ScheduleSummary> GetScheduleSummaryAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var schedule = await scheduleRepo.GetByIdAsync(scheduleId, ct);
        if (schedule == null)
            throw new InvalidOperationException($"Schedule with id {scheduleId} not found.");

        var wbsNodes = await wbsRepo.GetByScheduleAsync(scheduleId, ct);
        var activities = await activityRepo.GetByScheduleAsync(scheduleId, ct);
        var calendars = await calendarRepo.GetAllAsync(ct);

        return new ScheduleSummary
        {
            Id = schedule.Id,
            Name = schedule.Name,
            Status = schedule.Status,
            TotalWbsNodes = wbsNodes.Count(n => !n.IsDeleted),
            TotalActivities = activities.Count(a => !a.IsDeleted),
            TotalMilestones = activities.Count(a => !a.IsDeleted && a.Type == ActivityType.Milestone),
            TotalCalendars = calendars.Count,
            TotalConstraints = 0,
            ProjectStartDate = schedule.ProjectStartDate,
            TargetEndDate = schedule.TargetEndDate
        };
    }
}
