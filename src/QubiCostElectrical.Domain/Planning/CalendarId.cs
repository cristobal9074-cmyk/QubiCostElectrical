using System;

namespace QubiCostElectrical;

public sealed record CalendarId(string Value)
{
    public static CalendarId New() => new(Guid.NewGuid().ToString("N"));
    public static CalendarId Parse(string value) => new(value);
    public override string ToString() => Value;
}
