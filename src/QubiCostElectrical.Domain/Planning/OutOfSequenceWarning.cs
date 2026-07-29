using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class OutOfSequenceWarning
{
    public OutOfSequenceKind Kind { get; init; }
    public ActivityId ActivityId { get; init; } = null!;
    public ActivityId? RelatedActivityId { get; init; }
    public DateTime DetectedAtStatusDate { get; init; }
    public string Impact { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
}

public sealed class ProgressValidationIssue
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsError { get; init; }
}
