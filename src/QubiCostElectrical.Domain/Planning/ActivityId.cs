using System;

namespace QubiCostElectrical;

public sealed record ActivityId(string Value)
{
    public static ActivityId New() => new(Guid.NewGuid().ToString("N"));
    public static ActivityId Parse(string value) => new(value);
    public override string ToString() => Value;
}
