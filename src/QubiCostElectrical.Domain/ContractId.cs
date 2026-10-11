#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record ContractId(string Value)
{
    public static ContractId New()
    {
        return new ContractId(Guid.NewGuid().ToString("N"));
    }

    public static ContractId From(string value)
    {
        return new ContractId(value ?? throw new ArgumentNullException("value"));
    }

    public override string ToString()
    {
        return Value;
    }
}
