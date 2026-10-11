#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record ContractVersionId(string Value)
{
    public static ContractVersionId New()
    {
        return new ContractVersionId(Guid.NewGuid().ToString("N"));
    }

    public static ContractVersionId From(string value)
    {
        return new ContractVersionId(value ?? throw new ArgumentNullException("value"));
    }

    public override string ToString()
    {
        return Value;
    }
}
