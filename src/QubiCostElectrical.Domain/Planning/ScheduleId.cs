using System;

namespace QubiCostElectrical;

public sealed record ScheduleId(string Value)
{
    public static ScheduleId New() => new(Guid.NewGuid().ToString("N"));
    public static ScheduleId Parse(string value) => new(value);
    public override string ToString() => Value;
}
