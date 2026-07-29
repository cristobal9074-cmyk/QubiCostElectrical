using System;

namespace QubiCostElectrical;

public sealed class PlanningAuditEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Actor { get; set; } = string.Empty;
    public DateTime UtcTimestamp { get; set; } = DateTime.UtcNow;
    public string ProjectId { get; set; } = string.Empty;
    public string ScheduleId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? PreviousState { get; set; }
    public string? NewState { get; set; }
    public string? RelevantValues { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}
