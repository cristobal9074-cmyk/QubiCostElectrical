using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public sealed class ApuVersionComparator
{
    public ApuComparisonSummary Compare(ApuVersion left, ApuVersion right)
    {
        if (left.ApuId != right.ApuId)
        {
            throw new InvalidOperationException("Cannot compare APUs with different IDs: " + left.ApuId + " vs " + right.ApuId);
        }
        List<ApuVersionDiff> list = new List<ApuVersionDiff>();
        list.AddRange(CompareLists(ComparisonDimension.Components, left.Components.Select((ApuVersionComponent c) => c.ReferenceId).ToList(), right.Components.Select((ApuVersionComponent c) => c.ReferenceId).ToList(), "ComponentId"));
        list.AddRange(CompareComponentDetails(left.Components, right.Components));
        list.AddRange(CompareResourceRefs(left.ResourceRefs, right.ResourceRefs));
        list.AddRange(CompareEconomicConcepts(left.EconomicConcepts, right.EconomicConcepts));
        list.AddRange(CompareScenarios(left.Scenarios, right.Scenarios));
        if (left.CalculatedDirectCost != right.CalculatedDirectCost)
        {
            list.Add(new ApuVersionDiff
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
            list.Add(new ApuVersionDiff
            {
                Dimension = ComparisonDimension.FinalPrice,
                Type = DiffType.Modified,
                Identifier = "FinalPrice",
                Property = "CalculatedFinalPrice",
                OldNumericValue = left.CalculatedFinalPrice,
                NewNumericValue = right.CalculatedFinalPrice
            });
        }
        List<ApuVersionDiff> list2 = list.Where((ApuVersionDiff c) => c.IsBreaking).ToList();
        return new ApuComparisonSummary
        {
            ApuId = left.ApuId,
            LeftVersionId = left.VersionId,
            RightVersionId = right.VersionId,
            LeftVersionNumber = left.VersionNumber,
            RightVersionNumber = right.VersionNumber,
            TotalChanges = list.Count,
            BreakingChanges = list2.Count,
            AddedCount = list.Count((ApuVersionDiff c) => c.Type == DiffType.Added),
            RemovedCount = list.Count((ApuVersionDiff c) => c.Type == DiffType.Removed),
            ModifiedCount = list.Count((ApuVersionDiff c) => c.Type == DiffType.Modified),
            OldDirectCost = left.CalculatedDirectCost,
            NewDirectCost = right.CalculatedDirectCost,
            OldFinalPrice = left.CalculatedFinalPrice,
            NewFinalPrice = right.CalculatedFinalPrice,
            Changes = list.AsReadOnly(),
            BreakingChangeDescriptions = list2.Select((ApuVersionDiff c) => $"'{c.Identifier}' was removed from {c.Dimension}").ToList().AsReadOnly()
        };
    }

    private static List<ApuVersionDiff> CompareLists(ComparisonDimension dimension, IReadOnlyList<string> leftItems, IReadOnlyList<string> rightItems, string identifierLabel)
    {
        List<ApuVersionDiff> list = new List<ApuVersionDiff>();
        HashSet<string> hashSet = new HashSet<string>(leftItems);
        HashSet<string> hashSet2 = new HashSet<string>(rightItems);
        foreach (string item in hashSet.Except(hashSet2))
        {
            list.Add(new ApuVersionDiff
            {
                Dimension = dimension,
                Type = DiffType.Removed,
                Identifier = item,
                Property = identifierLabel
            });
        }
        foreach (string item2 in hashSet2.Except(hashSet))
        {
            list.Add(new ApuVersionDiff
            {
                Dimension = dimension,
                Type = DiffType.Added,
                Identifier = item2,
                Property = identifierLabel
            });
        }
        return list;
    }

    private static List<ApuVersionDiff> CompareComponentDetails(IReadOnlyList<ApuVersionComponent> left, IReadOnlyList<ApuVersionComponent> right)
    {
        List<ApuVersionDiff> list = new List<ApuVersionDiff>();
        Dictionary<string, ApuVersionComponent> dictionary = right.ToDictionary((ApuVersionComponent r) => r.ReferenceId, (ApuVersionComponent r) => r);
        foreach (ApuVersionComponent item in left)
        {
            if (dictionary.TryGetValue(item.ReferenceId, out var value))
            {
                if (item.Yield != value.Yield)
                {
                    list.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Components,
                        Type = DiffType.Modified,
                        Identifier = item.ReferenceId,
                        Property = "Yield",
                        OldNumericValue = item.Yield,
                        NewNumericValue = value.Yield
                    });
                }
                if (item.WastePercent != value.WastePercent)
                {
                    list.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Components,
                        Type = DiffType.Modified,
                        Identifier = item.ReferenceId,
                        Property = "WastePercent",
                        OldNumericValue = item.WastePercent,
                        NewNumericValue = value.WastePercent
                    });
                }
                if (item.AdjustmentFactor != value.AdjustmentFactor)
                {
                    list.Add(new ApuVersionDiff
                    {
                        Dimension = ComparisonDimension.Components,
                        Type = DiffType.Modified,
                        Identifier = item.ReferenceId,
                        Property = "AdjustmentFactor",
                        OldNumericValue = item.AdjustmentFactor,
                        NewNumericValue = value.AdjustmentFactor
                    });
                }
            }
        }
        return list;
    }

    private static List<ApuVersionDiff> CompareResourceRefs(IReadOnlyList<ApuVersionResourceRef> left, IReadOnlyList<ApuVersionResourceRef> right)
    {
        List<ApuVersionDiff> list = new List<ApuVersionDiff>();
        Dictionary<string, ApuVersionResourceRef> dictionary = right.ToDictionary((ApuVersionResourceRef r) => r.ResourceId, (ApuVersionResourceRef r) => r);
        foreach (ApuVersionResourceRef item in left)
        {
            if (dictionary.TryGetValue(item.ResourceId, out var value) && item.Price != value.Price)
            {
                list.Add(new ApuVersionDiff
                {
                    Dimension = ComparisonDimension.Resources,
                    Type = DiffType.Modified,
                    Identifier = item.ResourceId,
                    Property = "Price",
                    OldValue = $"{item.Price} {item.Currency}",
                    NewValue = $"{value.Price} {value.Currency}"
                });
            }
        }
        return list;
    }

    private static List<ApuVersionDiff> CompareEconomicConcepts(IReadOnlyList<ApuVersionEconomicConcept> left, IReadOnlyList<ApuVersionEconomicConcept> right)
    {
        List<ApuVersionDiff> list = new List<ApuVersionDiff>();
        Dictionary<string, ApuVersionEconomicConcept> dictionary = right.ToDictionary((ApuVersionEconomicConcept c) => c.Code, (ApuVersionEconomicConcept c) => c);
        foreach (ApuVersionEconomicConcept item in left)
        {
            if (dictionary.TryGetValue(item.Code, out var value) && item.Value != value.Value)
            {
                list.Add(new ApuVersionDiff
                {
                    Dimension = ComparisonDimension.EconomicConcepts,
                    Type = DiffType.Modified,
                    Identifier = item.Code,
                    Property = "Value",
                    OldNumericValue = item.Value,
                    NewNumericValue = value.Value
                });
            }
        }
        return list;
    }

    private static List<ApuVersionDiff> CompareScenarios(IReadOnlyList<ApuVersionScenario> left, IReadOnlyList<ApuVersionScenario> right)
    {
        List<ApuVersionDiff> list = new List<ApuVersionDiff>();
        Dictionary<string, ApuVersionScenario> dictionary = right.ToDictionary((ApuVersionScenario s) => s.ScenarioId, (ApuVersionScenario s) => s);
        foreach (ApuVersionScenario item in left)
        {
            if (dictionary.TryGetValue(item.ScenarioId, out var value) && item.OverrideCount != value.OverrideCount)
            {
                list.Add(new ApuVersionDiff
                {
                    Dimension = ComparisonDimension.Scenarios,
                    Type = DiffType.Modified,
                    Identifier = item.ScenarioId,
                    Property = "OverrideCount",
                    OldNumericValue = item.OverrideCount,
                    NewNumericValue = value.OverrideCount
                });
            }
        }
        return list;
    }
}
