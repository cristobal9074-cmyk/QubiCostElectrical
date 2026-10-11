using System;

namespace QubiCostElectrical;

public sealed record ApuVersionDiff
{
    public ComparisonDimension Dimension { get; init; }

    public DiffType Type { get; init; }

    public string Identifier { get; init; } = "";

    public string OldValue { get; init; } = "";

    public string NewValue { get; init; } = "";

    public string Property { get; init; } = "";

    public decimal? OldNumericValue { get; init; }

    public decimal? NewNumericValue { get; init; }

    public decimal AbsoluteChange => NewNumericValue.GetValueOrDefault() - OldNumericValue.GetValueOrDefault();

    public decimal? RelativeChange
    {
        get
        {
            if (!(OldNumericValue.GetValueOrDefault() != 0m))
            {
                return null;
            }
            return Math.Round((NewNumericValue.GetValueOrDefault() - OldNumericValue.GetValueOrDefault()) / OldNumericValue.GetValueOrDefault() * 100m, 2);
        }
    }

    public bool IsBreaking
    {
        get
        {
            if (Dimension == ComparisonDimension.Components)
            {
                return Type == DiffType.Removed;
            }
            return false;
        }
    }
}
