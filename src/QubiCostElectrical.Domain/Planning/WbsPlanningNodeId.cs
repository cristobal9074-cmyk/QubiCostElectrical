using System;

namespace QubiCostElectrical;

public sealed record WbsPlanningNodeId(string Value)
{
    public static WbsPlanningNodeId New() => new(Guid.NewGuid().ToString("N"));
    public static WbsPlanningNodeId Parse(string value) => new(value);
    public override string ToString() => Value;
}
