#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record PunchItemId(string Value)
{
    public static PunchItemId New()
    {
        return new PunchItemId(Guid.NewGuid().ToString("N"));
    }

    public static PunchItemId From(string value)
    {
        return new PunchItemId(value);
    }
}
