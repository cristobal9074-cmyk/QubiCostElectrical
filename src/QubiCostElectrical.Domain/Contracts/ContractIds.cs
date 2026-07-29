using System;

namespace QubiCostElectrical;

public sealed record ContractId(string Value)
{
    public static ContractId New() => new(Guid.NewGuid().ToString("N"));
    public static ContractId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}

public sealed record ContractVersionId(string Value)
{
    public static ContractVersionId New() => new(Guid.NewGuid().ToString("N"));
    public static ContractVersionId From(string value) => new(value ?? throw new ArgumentNullException(nameof(value)));
    public override string ToString() => Value;
}
