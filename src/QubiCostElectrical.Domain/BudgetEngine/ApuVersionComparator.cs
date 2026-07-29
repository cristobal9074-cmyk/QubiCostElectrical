using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum ComparisonDimension
{
    Components,
    Resources,
    EconomicConcepts,
    Scenarios,
    Metadata,
    DirectCost,
    FinalPrice
}

public enum DiffType
{
    Added,
    Removed,
    Modified,
    Unchanged
}

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
    public decimal? RelativeChange => OldNumericValue.GetValueOrDefault() != 0
        ? Math.Round((NewNumericValue.GetValueOrDefault() - OldNumericValue.GetValueOrDefault()) / OldNumericValue.GetValueOrDefault() * 100, 2)
        : null;
    public bool IsBreaking => Dimension == ComparisonDimension.Components
        && (Type == DiffType.Removed);
}

public sealed record ApuComparisonSummary
{
    public string ApuId { get; init; } = "";
    public string LeftVersionId { get; init; } = "";
    public string RightVersionId { get; init; } = "";
    public int LeftVersionNumber { get; init; }
    public int RightVersionNumber { get; init; }
    public DateTime ComparedAt { get; init; } = DateTime.UtcNow;

    public int TotalChanges { get; init; }
    public int BreakingChanges { get; init; }
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ModifiedCount { get; init; }

    public decimal OldDirectCost { get; init; }
    public decimal NewDirectCost { get; init; }
    public decimal DirectCostDelta => NewDirectCost - OldDirectCost;
    public decimal? DirectCostPercentChange => OldDirectCost != 0
        ? Math.Round(DirectCostDelta / OldDirectCost * 100, 2) : null;

    public decimal OldFinalPrice { get; init; }
    public decimal NewFinalPrice { get; init; }
    public decimal PriceDelta => NewFinalPrice - OldFinalPrice;
    public decimal? PricePercentChange => OldFinalPrice != 0
        ? Math.Round(PriceDelta / OldFinalPrice * 100, 2) : null;

    public bool IsIdentical => TotalChanges == 0;

    public IReadOnlyList<ApuVersionDiff> Changes { get; init; } = Array.Empty<ApuVersionDiff>();
    public IReadOnlyList<string> BreakingChangeDescriptions { get; init; } = Array.Empty<string>();
}

public sealed class ApuVersionComparator
{
    public ApuComparisonSummary Compare(ApuVersion left, ApuVersion right)
    {
        if (left.ApuId != right.ApuId)
        {
            throw new InvalidOperationException($"Cannot compare APUs with different IDs: {left.ApuId} vs {right.ApuId}");
        }

        var changes = new List<ApuVersionDiff>();

        changes.AddRange(CompareLists(
            ComparisonDimension.Components,
            left.Components.Select(c => c.ReferenceId).ToList(),
            right.Components.Select(c => c.ReferenceId).ToList(),
            "ComponentId"));

        changes.AddRange(CompareComponentDetails(left.Components, right.Components));
        changes.AddRange(CompareResourceRefs(left.ResourceRefs, right.ResourceRefs));
        changes.AddRange(CompareEconomicConcepts(left.EconomicConcepts, right.EconomicConcepts));
        changes.AddRange(CompareScenarios(left.Scenarios, right.Scenarios));

        if (left.CalculatedDirectCost != right.CalculatedDirectCost)
        {
            changes.Add(new ApuVersionDiff
            {
                Dimension = ComparisonDimension.DirectCost,
                Type = DiffType.Modified,
                Identifier = "DirectCost",
                Property = "CalculatedDirectCost",
                OldNumericValue = left.CalculatedDirectCost,
                NewNumericValue = right.CalculatedDirectCost
            });
        }

        if (left.CalculatedFinalPrice != right.CalculatedFinalPrice)
        {
            changes.Add(new ApuVersionDiff
            {
                Dimension = ComparisonDimension.FinalPrice,
                Type = DiffType.Modified,
                Identifier = "FinalPrice",
                Property = "CalculatedFinalPrice",
                OldNumericValue = left.CalculatedFinalPrice,
                NewNumericValue = right.CalculatedFinalPrice
            });
        }

        var breakingChanges = changes.Where(c => c.IsBreaking).ToList();

        return new ApuComparisonSummary
        {
            ApuId = left.ApuId,
            LeftVersionId = left.VersionId,
            RightVersionId = right.VersionId,
            LeftVersionNumber = left.VersionNumber,
            RightVersionNumber = right.VersionNumber,
            TotalChanges = changes.Count,
            BreakingChanges = breakingChanges.Count,
            AddedCount = changes.Count(c => c.Type == DiffType.Added),
            RemovedCount = changes.Count(c => c.Type == DiffType.Removed),
            ModifiedCount = changes.Count(c => c.Type == DiffType.Modified),
            OldDirectCost = left.CalculatedDirectCost,
            NewDirectCost = right.CalculatedDirectCost,
            OldFinalPrice = left.CalculatedFinalPrice,
            NewFinalPrice = right.CalculatedFinalPrice,
            Changes = changes.AsReadOnly(),
            BreakingChangeDescriptions = breakingChanges
                .Select(c => $"'{c.Identifier}' was removed from {c.Dimension}")
                .ToList().AsReadOnly()
        };
    }

    private static List<ApuVersionDiff> CompareLists(
        ComparisonDimension dimension,
        IReadOnlyList<string> leftItems,
        IReadOnlyList<string> rightItems,
        string identifierLabel)
    {
        var diffs = new List<ApuVersionDiff>();
        var leftSet = new HashSet<string>(leftItems);
        var rightSet = new HashSet<string>(rightItems);

        foreach (var item in leftSet.Except(rightSet))
        {
            diffs.Add(new ApuVersionDiff
            {
                Dimension = dimension,
                Type = DiffType.Removed,
                Identifier = item,
                Property = identifierLabel
            });
        }

        foreach (var item in rightSet.Except(leftSet))
        {
            diffs.Add(new ApuVersionDiff
            {
                Dimension = dimension,
                Type = DiffType.Added,
                Identifier = item,
                Property = identifierLabel
            });
        }

        return diffs;
    }

    private static List<ApuVersionDiff> CompareComponentDetails(
        IReadOnlyList<ApuVersionComponent> left,
        IReadOnlyList<ApuVersionComponent> right)
    {
        var diffs = new List<ApuVersionDiff>();
        var rightByRef = right.ToDictionary(r => r.ReferenceId, r => r);

        foreach (var l in left)
        {
            if (rightByRef.TryGetValue(l.ReferenceId, out var r))
            {
                if (l.Yield != r.Yield)
                {
                    diffs.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Components,
                        Type = DiffType.Modified,
                        Identifier = l.ReferenceId,
                        Property = "Yield",
                        OldNumericValue = l.Yield,
                        NewNumericValue = r.Yield
                    });
                }
                if (l.WastePercent != r.WastePercent)
                {
                    diffs.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Components,
                        Type = DiffType.Modified,
                        Identifier = l.ReferenceId,
                        Property = "WastePercent",
                        OldNumericValue = l.WastePercent,
                        NewNumericValue = r.WastePercent
                    });
                }
                if (l.AdjustmentFactor != r.AdjustmentFactor)
                {
                    diffs.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Components,
                        Type = DiffType.Modified,
                        Identifier = l.ReferenceId,
                        Property = "AdjustmentFactor",
                        OldNumericValue = l.AdjustmentFactor,
                        NewNumericValue = r.AdjustmentFactor
                    });
                }
            }
        }

        return diffs;
    }

    private static List<ApuVersionDiff> CompareResourceRefs(
        IReadOnlyList<ApuVersionResourceRef> left,
        IReadOnlyList<ApuVersionResourceRef> right)
    {
        var diffs = new List<ApuVersionDiff>();
        var rightById = right.ToDictionary(r => r.ResourceId, r => r);

        foreach (var l in left)
        {
            if (rightById.TryGetValue(l.ResourceId, out var r))
            {
                if (l.Price != r.Price)
                {
                    diffs.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Resources,
                        Type = DiffType.Modified,
                        Identifier = l.ResourceId,
                        Property = "Price",
                        OldValue = $"{l.Price} {l.Currency}",
                        NewValue = $"{r.Price} {r.Currency}"
                    });
                }
            }
        }

        return diffs;
    }

    private static List<ApuVersionDiff> CompareEconomicConcepts(
        IReadOnlyList<ApuVersionEconomicConcept> left,
        IReadOnlyList<ApuVersionEconomicConcept> right)
    {
        var diffs = new List<ApuVersionDiff>();
        var rightByCode = right.ToDictionary(c => c.Code, c => c);

        foreach (var l in left)
        {
            if (rightByCode.TryGetValue(l.Code, out var r))
            {
                if (l.Value != r.Value)
                {
                    diffs.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.EconomicConcepts,
                        Type = DiffType.Modified,
                        Identifier = l.Code,
                        Property = "Value",
                        OldNumericValue = l.Value,
                        NewNumericValue = r.Value
                    });
                }
            }
        }

        return diffs;
    }

    private static List<ApuVersionDiff> CompareScenarios(
        IReadOnlyList<ApuVersionScenario> left,
        IReadOnlyList<ApuVersionScenario> right)
    {
        var diffs = new List<ApuVersionDiff>();
        var rightById = right.ToDictionary(s => s.ScenarioId, s => s);

        foreach (var l in left)
        {
            if (rightById.TryGetValue(l.ScenarioId, out var r))
            {
                if (l.OverrideCount != r.OverrideCount)
                {
                    diffs.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Scenarios,
                        Type = DiffType.Modified,
                        Identifier = l.ScenarioId,
                        Property = "OverrideCount",
                        OldNumericValue = l.OverrideCount,
                        NewNumericValue = r.OverrideCount
                    });
                }
            }
        }

        return diffs;
    }
}
