namespace QubiCostElectrical;

public sealed record AuthAdminAuditEntry(
    Guid Id,
    DateTime OccurredAt,
    string ActorEmail,
    string Action,
    string TargetEmail,
    string Details);
