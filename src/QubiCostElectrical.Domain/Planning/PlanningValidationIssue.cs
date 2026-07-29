namespace QubiCostElectrical;

public sealed record PlanningValidationIssue
{
    public PlanningValidationCode Code { get; init; }
    public string Message { get; init; }
    public PlanningValidationSeverity Severity { get; init; }
    public string EntityType { get; init; }
    public string EntityField { get; init; }
    public string EntityId { get; init; }

    public PlanningValidationIssue(PlanningValidationCode code, string message, PlanningValidationSeverity severity,
        string entityType = "", string entityField = "", string entityId = "")
    {
        Code = code;
        Message = message;
        Severity = severity;
        EntityType = entityType;
        EntityField = entityField;
        EntityId = entityId;
    }
}
