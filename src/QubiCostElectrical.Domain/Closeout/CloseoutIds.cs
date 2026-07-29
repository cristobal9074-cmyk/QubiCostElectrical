namespace QubiCostElectrical;

public sealed record CloseoutId(string Value)
{
    public static CloseoutId New() => new(Guid.NewGuid().ToString("N"));
    public static CloseoutId From(string value) => new(value);
}

public sealed record ChecklistId(string Value)
{
    public static ChecklistId New() => new(Guid.NewGuid().ToString("N"));
    public static ChecklistId From(string value) => new(value);
}

public sealed record ChecklistItemId(string Value)
{
    public static ChecklistItemId New() => new(Guid.NewGuid().ToString("N"));
    public static ChecklistItemId From(string value) => new(value);
}

public sealed record ChecklistTemplateId(string Value)
{
    public static ChecklistTemplateId New() => new(Guid.NewGuid().ToString("N"));
    public static ChecklistTemplateId From(string value) => new(value);
}

public sealed record ChecklistCategoryId(string Value)
{
    public static ChecklistCategoryId New() => new(Guid.NewGuid().ToString("N"));
    public static ChecklistCategoryId From(string value) => new(value);
}

public sealed record PunchItemId(string Value)
{
    public static PunchItemId New() => new(Guid.NewGuid().ToString("N"));
    public static PunchItemId From(string value) => new(value);
}

public sealed record NonConformanceId(string Value)
{
    public static NonConformanceId New() => new(Guid.NewGuid().ToString("N"));
    public static NonConformanceId From(string value) => new(value);
}

public sealed record CorrectiveActionId(string Value)
{
    public static CorrectiveActionId New() => new(Guid.NewGuid().ToString("N"));
    public static CorrectiveActionId From(string value) => new(value);
}

public sealed record CloseoutDocumentId(string Value)
{
    public static CloseoutDocumentId New() => new(Guid.NewGuid().ToString("N"));
    public static CloseoutDocumentId From(string value) => new(value);
}

public sealed record DocumentRequirementId(string Value)
{
    public static DocumentRequirementId New() => new(Guid.NewGuid().ToString("N"));
    public static DocumentRequirementId From(string value) => new(value);
}

public sealed record EvidenceId(string Value)
{
    public static EvidenceId New() => new(Guid.NewGuid().ToString("N"));
    public static EvidenceId From(string value) => new(value);
}

public sealed record CloseoutMeetingId(string Value)
{
    public static CloseoutMeetingId New() => new(Guid.NewGuid().ToString("N"));
    public static CloseoutMeetingId From(string value) => new(value);
}

public sealed record CloseoutExceptionId(string Value)
{
    public static CloseoutExceptionId New() => new(Guid.NewGuid().ToString("N"));
    public static CloseoutExceptionId From(string value) => new(value);
}

public sealed record CloseoutSnapshotId(string Value)
{
    public static CloseoutSnapshotId New() => new(Guid.NewGuid().ToString("N"));
    public static CloseoutSnapshotId From(string value) => new(value);
}

public sealed record CloseoutAlertId(string Value)
{
    public static CloseoutAlertId New() => new(Guid.NewGuid().ToString("N"));
    public static CloseoutAlertId From(string value) => new(value);
}
