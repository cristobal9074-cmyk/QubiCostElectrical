using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed partial class ScheduleService : IScheduleService
{
    private readonly IScheduleRepository scheduleRepo;
    private readonly IWbsPlanningRepository wbsRepo;
    private readonly IActivityRepository activityRepo;
    private readonly ICalendarRepository calendarRepo;
    private readonly IPlanningAuditRepository auditRepo;
    private readonly ICalendarCalculationService calendarCalc;

    public ScheduleService(
        IScheduleRepository scheduleRepo,
        IWbsPlanningRepository wbsRepo,
        IActivityRepository activityRepo,
        ICalendarRepository calendarRepo,
        IPlanningAuditRepository auditRepo,
        ICalendarCalculationService calendarCalc)
    {
        this.scheduleRepo = scheduleRepo;
        this.wbsRepo = wbsRepo;
        this.activityRepo = activityRepo;
        this.calendarRepo = calendarRepo;
        this.auditRepo = auditRepo;
        this.calendarCalc = calendarCalc;
    }

    public async Task<Schedule> CreateScheduleAsync(string name, string timeZoneId, CalendarId defaultCalendarId, string createdBy, CancellationToken ct = default)
    {
        var schedule = new Schedule(ScheduleId.New(), name, timeZoneId, defaultCalendarId, createdBy);
        await scheduleRepo.SaveAsync(schedule, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = createdBy,
            ProjectId = "",
            ScheduleId = schedule.Id.ToString(),
            EntityType = "Schedule",
            EntityId = schedule.Id.ToString(),
            Action = "Create",
            Result = "Success"
        }, ct);
        return schedule;
    }

    public async Task<Schedule?> GetScheduleAsync(ScheduleId id, CancellationToken ct = default)
    {
        return await scheduleRepo.GetByIdAsync(id, ct);
    }

    public async Task<List<PlanningValidationIssue>> UpdateScheduleAsync(Schedule schedule, string modifiedBy, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        if (schedule == null)
            throw new ArgumentNullException(nameof(schedule));
        if (string.IsNullOrWhiteSpace(modifiedBy))
            throw new ArgumentNullException(nameof(modifiedBy));

        var existing = await scheduleRepo.GetByIdAsync(schedule.Id, ct);
        if (existing == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ScheduleNotFound,
                "El cronograma no existe.",
                PlanningValidationSeverity.Error,
                "Schedule", "Id", schedule.Id.ToString()));
            return issues;
        }

        if (existing.Status == ScheduleStatus.Archived || existing.Status == ScheduleStatus.Obsolete)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ReadOnlySchedule,
                "No se puede modificar un cronograma archivado u obsoleto.",
                PlanningValidationSeverity.Error,
                "Schedule", "Status", schedule.Id.ToString()));
            return issues;
        }

        schedule.ModifiedBy = modifiedBy;
        schedule.ModifiedAt = DateTime.UtcNow;
        await scheduleRepo.SaveAsync(schedule, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = modifiedBy,
            ScheduleId = schedule.Id.ToString(),
            EntityType = "Schedule",
            EntityId = schedule.Id.ToString(),
            Action = "Update",
            PreviousState = existing.RowVersion,
            Result = "Success"
        }, ct);
        return issues;
    }

    public async Task<List<PlanningValidationIssue>> DeleteScheduleAsync(ScheduleId id, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        var existing = await scheduleRepo.GetByIdAsync(id, ct);
        if (existing == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ScheduleNotFound,
                "El cronograma no existe.",
                PlanningValidationSeverity.Error,
                "Schedule", "Id", id.ToString()));
            return issues;
        }

        if (existing.Status == ScheduleStatus.Approved)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ScheduleAlreadyApproved,
                "No se puede eliminar un cronograma aprobado.",
                PlanningValidationSeverity.Error,
                "Schedule", "Status", id.ToString()));
            return issues;
        }

        var activities = await activityRepo.GetByScheduleAsync(id, ct);
        if (activities.Any(a => !a.IsDeleted))
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.InvalidActivityDuration,
                "No se puede eliminar el cronograma porque tiene actividades activas.",
                PlanningValidationSeverity.Error,
                "Schedule", "Id", id.ToString()));
            return issues;
        }

        await scheduleRepo.DeleteAsync(id, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            ScheduleId = id.ToString(),
            EntityType = "Schedule",
            EntityId = id.ToString(),
            Action = "Delete",
            Result = "Success"
        }, ct);
        return issues;
    }

    public async Task<Schedule?> GetCurrentProjectScheduleAsync(CancellationToken ct = default)
    {
        var schedules = await scheduleRepo.GetByProjectAsync("", ct);
        return schedules.FirstOrDefault(s => s.Status == ScheduleStatus.Draft || s.Status == ScheduleStatus.InPlanning || s.Status == ScheduleStatus.InReview);
    }

    public async Task<WbsPlanningNode> CreateWbsNodeAsync(ScheduleId scheduleId, string wbsCode, string name, WbsNodeType nodeType, WbsPlanningNodeId? parentId, string modifiedBy, CancellationToken ct = default)
    {
        if (scheduleId == null) throw new ArgumentNullException(nameof(scheduleId));
        if (string.IsNullOrWhiteSpace(wbsCode)) throw new ArgumentNullException(nameof(wbsCode));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(modifiedBy)) throw new ArgumentNullException(nameof(modifiedBy));

        var node = new WbsPlanningNode(WbsPlanningNodeId.New(), scheduleId, wbsCode, name, nodeType, modifiedBy);
        node.ParentId = parentId;

        if (parentId != null)
        {
            var parent = await wbsRepo.GetByIdAsync(parentId, ct);
            if (parent != null)
            {
                node.Level = parent.Level + 1;
            }
        }

        node.SortOrder = await wbsRepo.GetMaxSortOrderAsync(scheduleId, parentId, ct) + 1;
        await wbsRepo.SaveAsync(node, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = modifiedBy,
            ScheduleId = scheduleId.ToString(),
            EntityType = "WbsPlanningNode",
            EntityId = node.Id.ToString(),
            Action = "Create",
            RelevantValues = $"{{\"wbsCode\":\"{wbsCode}\",\"nodeType\":\"{nodeType}\"}}",
            Result = "Success"
        }, ct);
        return node;
    }

    public async Task<List<PlanningValidationIssue>> MoveWbsNodeAsync(WbsPlanningNodeId nodeId, WbsPlanningNodeId? newParentId, int newSortOrder, string modifiedBy, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        var node = await wbsRepo.GetByIdAsync(nodeId, ct);
        if (node == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.WbsNodeNotFound,
                "El nodo WBS no existe.",
                PlanningValidationSeverity.Error,
                "WbsPlanningNode", "Id", nodeId.ToString()));
            return issues;
        }

        if (newParentId != null)
        {
            if (newParentId.Equals(nodeId))
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationCode.WbsCycleDetected,
                    "Un nodo no puede ser su propio padre.",
                    PlanningValidationSeverity.Error,
                    "WbsPlanningNode", "ParentId", nodeId.ToString()));
                return issues;
            }

            var parent = await wbsRepo.GetByIdAsync(newParentId, ct);
            if (parent == null)
            {
                issues.Add(new PlanningValidationIssue(
                    PlanningValidationCode.InvalidWbsParent,
                    "El nodo padre especificado no existe.",
                    PlanningValidationSeverity.Error,
                    "WbsPlanningNode", "ParentId", newParentId.ToString()));
                return issues;
            }
        }

        node.ParentId = newParentId;
        node.SortOrder = newSortOrder;
        node.ModifiedBy = modifiedBy;
        node.ModifiedAt = DateTime.UtcNow;

        if (newParentId != null)
        {
            var parent = await wbsRepo.GetByIdAsync(newParentId, ct);
            if (parent != null)
                node.Level = parent.Level + 1;
            else
                node.Level = 1;
        }
        else
        {
            node.Level = 1;
        }

        await wbsRepo.SaveAsync(node, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = modifiedBy,
            ScheduleId = node.ScheduleId.ToString(),
            EntityType = "WbsPlanningNode",
            EntityId = node.Id.ToString(),
            Action = "Move",
            RelevantValues = $"{{\"newParentId\":\"{newParentId}\",\"newSortOrder\":{newSortOrder}}}",
            Result = "Success"
        }, ct);
        return issues;
    }

    public async Task<List<PlanningValidationIssue>> DeleteWbsNodeAsync(WbsPlanningNodeId nodeId, string modifiedBy, CancellationToken ct = default)
    {
        var issues = new List<PlanningValidationIssue>();
        var node = await wbsRepo.GetByIdAsync(nodeId, ct);
        if (node == null)
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.WbsNodeNotFound,
                "El nodo WBS no existe.",
                PlanningValidationSeverity.Error,
                "WbsPlanningNode", "Id", nodeId.ToString()));
            return issues;
        }

        var children = await wbsRepo.GetByScheduleAsync(node.ScheduleId, ct);
        if (children.Any(c => c.ParentId != null && c.ParentId.Equals(nodeId) && !c.IsDeleted))
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.InvalidWbsParent,
                "No se puede eliminar un nodo que tiene hijos. Elimine los hijos primero.",
                PlanningValidationSeverity.Error,
                "WbsPlanningNode", "Id", nodeId.ToString()));
            return issues;
        }

        var activities = await activityRepo.GetByWbsNodeAsync(nodeId, ct);
        if (activities.Any(a => !a.IsDeleted))
        {
            issues.Add(new PlanningValidationIssue(
                PlanningValidationCode.ActivityNotFound,
                "No se puede eliminar un nodo que tiene actividades. Elimine las actividades primero.",
                PlanningValidationSeverity.Error,
                "WbsPlanningNode", "Id", nodeId.ToString()));
            return issues;
        }

        node.IsDeleted = true;
        node.ModifiedBy = modifiedBy;
        node.ModifiedAt = DateTime.UtcNow;
        await wbsRepo.SaveAsync(node, ct);
        await auditRepo.RecordAsync(new PlanningAuditEvent
        {
            Actor = modifiedBy,
            ScheduleId = node.ScheduleId.ToString(),
            EntityType = "WbsPlanningNode",
            EntityId = node.Id.ToString(),
            Action = "Delete",
            Result = "Success"
        }, ct);
        return issues;
    }

    public async Task<List<WbsPlanningNode>> GetWbsTreeAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var nodes = await wbsRepo.GetByScheduleAsync(scheduleId, ct);
        return nodes.Where(n => !n.IsDeleted).OrderBy(n => n.SortOrder).ThenBy(n => n.WbsCode).ToList();
    }
}
