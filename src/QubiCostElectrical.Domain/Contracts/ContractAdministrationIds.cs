using System;

namespace QubiCostElectrical;

public sealed record AdjustmentId(string Value)
{
    public static AdjustmentId New() => new(Guid.NewGuid().ToString("N"));
    public static AdjustmentId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record AdjustmentFormulaId(string Value)
{
    public static AdjustmentFormulaId New() => new(Guid.NewGuid().ToString("N"));
    public static AdjustmentFormulaId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record AdjustmentIndexId(string Value)
{
    public static AdjustmentIndexId New() => new(Guid.NewGuid().ToString("N"));
    public static AdjustmentIndexId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record GuaranteeId(string Value)
{
    public static GuaranteeId New() => new(Guid.NewGuid().ToString("N"));
    public static GuaranteeId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record PenaltyId(string Value)
{
    public static PenaltyId New() => new(Guid.NewGuid().ToString("N"));
    public static PenaltyId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record InvoiceId(string Value)
{
    public static InvoiceId New() => new(Guid.NewGuid().ToString("N"));
    public static InvoiceId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record InvoiceLineId(string Value)
{
    public static InvoiceLineId New() => new(Guid.NewGuid().ToString("N"));
    public static InvoiceLineId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ContractAdministrationAuditEventId(string Value)
{
    public static ContractAdministrationAuditEventId New() => new(Guid.NewGuid().ToString("N"));
    public static ContractAdministrationAuditEventId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}
