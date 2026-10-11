#nullable enable
using System;

namespace QubiCostElectrical;

public sealed record CloseoutId(string Value)
{
    public static CloseoutId New()
    {
        return new CloseoutId(Guid.NewGuid().ToString("N"));
    }

    public static CloseoutId From(string value)
    {
        return new CloseoutId(value);
    }
}
