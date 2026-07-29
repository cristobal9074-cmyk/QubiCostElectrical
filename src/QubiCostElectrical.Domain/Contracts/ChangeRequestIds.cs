using System;

namespace QubiCostElectrical;

public sealed record ChangeRequestId(string Value)
{
    public static ChangeRequestId New() => new(Guid.NewGuid().ToString("N"));
    public static ChangeRequestId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ChangeOrderId(string Value)
{
    public static ChangeOrderId New() => new(Guid.NewGuid().ToString("N"));
    public static ChangeOrderId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ChangeOrderVersionId(string Value)
{
    public static ChangeOrderVersionId New() => new(Guid.NewGuid().ToString("N"));
    public static ChangeOrderVersionId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ChangeOrderSnapshotId(string Value)
{
    public static ChangeOrderSnapshotId New() => new(Guid.NewGuid().ToString("N"));
    public static ChangeOrderSnapshotId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ExtraWorkId(string Value)
{
    public static ExtraWorkId New() => new(Guid.NewGuid().ToString("N"));
    public static ExtraWorkId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ClaimId(string Value)
{
    public static ClaimId New() => new(Guid.NewGuid().ToString("N"));
    public static ClaimId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ClaimEventId(string Value)
{
    public static ClaimEventId New() => new(Guid.NewGuid().ToString("N"));
    public static ClaimEventId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ClaimEvidenceId(string Value)
{
    public static ClaimEvidenceId New() => new(Guid.NewGuid().ToString("N"));
    public static ClaimEvidenceId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ClaimEvaluationId(string Value)
{
    public static ClaimEvaluationId New() => new(Guid.NewGuid().ToString("N"));
    public static ClaimEvaluationId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ContractChangeAuditEventId(string Value)
{
    public static ContractChangeAuditEventId New() => new(Guid.NewGuid().ToString("N"));
    public static ContractChangeAuditEventId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}
