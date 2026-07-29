using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public enum MergeConflictResolution
{
    UseLeft,
    UseRight,
    UseNewer,
    UseOlder,
    Manual
}

public sealed record MergeConflict
{
    public string Identifier { get; init; } = "";
    public string Property { get; init; } = "";
    public string LeftValue { get; init; } = "";
    public string RightValue { get; init; } = "";
    public string BaseValue { get; init; } = "";
    public MergeConflictResolution Resolution { get; init; } = MergeConflictResolution.Manual;
    public bool IsResolved => Resolution != MergeConflictResolution.Manual;
}

public sealed record MergeResult
{
    public ApuVersion MergedVersion { get; init; } = null!;
    public bool Success { get; init; }
    public bool HasConflicts => Conflicts.Count > 0;
    public bool AllConflictsResolved => Conflicts.Count > 0 && Conflicts.All(c => c.IsResolved);
    public IReadOnlyList<MergeConflict> Conflicts { get; init; } = Array.Empty<MergeConflict>();
    public IReadOnlyList<string> AppliedChanges { get; init; } = Array.Empty<string>();
}

public sealed class ApuVersionMerger
{
    public MergeResult Merge(
        ApuVersion left,
        ApuVersion right,
        ApuVersion baseVersion,
        string author,
        string changeReason,
        Dictionary<string, MergeConflictResolution>? autoResolutions = null)
    {
        autoResolutions ??= new Dictionary<string, MergeConflictResolution>();

        var conflicts = new List<MergeConflict>();
        var appliedChanges = new List<string>();

        var mergedComponents = MergeComponentLists(
            left.Components, right.Components, baseVersion.Components,
            conflicts, appliedChanges, autoResolutions);

        var mergedResources = MergeResourceRefs(
            left.ResourceRefs, right.ResourceRefs, baseVersion.ResourceRefs,
            conflicts, appliedChanges, autoResolutions);

        var mergedConcepts = MergeEconomicConcepts(
            left.EconomicConcepts, right.EconomicConcepts, baseVersion.EconomicConcepts,
            conflicts, appliedChanges, autoResolutions);

        var mergedScenarios = MergeScenarioLists(
            left.Scenarios, right.Scenarios, baseVersion.Scenarios,
            conflicts, appliedChanges, autoResolutions);

        var directCost = ResolveNumericConflict(
            "CalculatedDirectCost",
            left.CalculatedDirectCost, right.CalculatedDirectCost, baseVersion.CalculatedDirectCost,
            conflicts, appliedChanges, autoResolutions);

        var finalPrice = ResolveNumericConflict(
            "CalculatedFinalPrice",
            left.CalculatedFinalPrice, right.CalculatedFinalPrice, baseVersion.CalculatedFinalPrice,
            conflicts, appliedChanges, autoResolutions);

        var mergedVersion = left with
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = Math.Max(left.VersionNumber, right.VersionNumber) + 1,
            PreviousVersionId = $"{left.VersionId},{right.VersionId}",
            Status = ApuVersionStatus.Draft,
            Author = author,
            ChangeReason = changeReason,
            CreatedAt = DateTime.UtcNow,
            Components = mergedComponents.AsReadOnly(),
            ResourceRefs = mergedResources.AsReadOnly(),
            EconomicConcepts = mergedConcepts.AsReadOnly(),
            Scenarios = mergedScenarios.AsReadOnly(),
            CalculatedDirectCost = directCost,
            CalculatedFinalPrice = finalPrice,
            IntegrityHash = ""
        };

        return new MergeResult
        {
            MergedVersion = mergedVersion,
            Success = conflicts.Count == 0 || conflicts.All(c => c.IsResolved),
            Conflicts = conflicts.AsReadOnly(),
            AppliedChanges = appliedChanges.AsReadOnly()
        };
    }

    private static List<ApuVersionComponent> MergeComponentLists(
        IReadOnlyList<ApuVersionComponent> left,
        IReadOnlyList<ApuVersionComponent> right,
        IReadOnlyList<ApuVersionComponent> baseVersion,
        List<MergeConflict> conflicts,
        List<string> appliedChanges,
        Dictionary<string, MergeConflictResolution> resolutions)
    {
        var baseDict = baseVersion.ToDictionary(c => c.ReferenceId, c => c);
        var leftDict = left.ToDictionary(c => c.ReferenceId, c => c);
        var rightDict = right.ToDictionary(c => c.ReferenceId, c => c);
        var allKeys = new HashSet<string>(leftDict.Keys.Concat(rightDict.Keys));
        var result = new List<ApuVersionComponent>();

        foreach (var key in allKeys.OrderBy(k => k))
        {
            var hasLeft = leftDict.TryGetValue(key, out var l);
            var hasRight = rightDict.TryGetValue(key, out var r);
            var hasBase = baseDict.TryGetValue(key, out var b);

            if (hasLeft && hasRight)
            {
                if (l == r)
                {
                    result.Add(l!);
                    continue;
                }
                if (hasBase && b == l) { result.Add(r!); appliedChanges.Add($"Component '{key}': using right version (base == left)"); continue; }
                if (hasBase && b == r) { result.Add(l!); appliedChanges.Add($"Component '{key}': using left version (base == right)"); continue; }

                var resolutionKey = $"Component:{key}";
                if (resolutions.TryGetValue(resolutionKey, out var resolution))
                {
                    if (resolution == MergeConflictResolution.UseLeft) { result.Add(l!); appliedChanges.Add($"Component '{key}': auto-resolved using left"); }
                    else if (resolution == MergeConflictResolution.UseRight) { result.Add(r!); appliedChanges.Add($"Component '{key}': auto-resolved using right"); }
                    else { result.Add(l!); conflicts.Add(CreateConflict(key, "Yield", $"{l!.Yield}", $"{r!.Yield}", $"{b?.Yield}")); }
                }
                else
                {
                    result.Add(b ?? l!);
                    conflicts.Add(CreateConflict(key, "Yield", $"{l!.Yield}", $"{r!.Yield}", $"{b?.Yield}"));
                }
            }
            else if (hasLeft && !hasRight)
            {
                if (hasBase && b != null) { conflicts.Add(CreateConflict(key, "Existence", "Present", "Removed", "Present")); }
                result.Add(l!);
            }
            else if (!hasLeft && hasRight)
            {
                if (hasBase) { conflicts.Add(CreateConflict(key, "Existence", "Removed", "Present", "Present")); result.Add(r!); }
                else { result.Add(r!); appliedChanges.Add($"Component '{key}': added from right branch"); }
            }
        }

        return result;
    }

    private static List<ApuVersionResourceRef> MergeResourceRefs(
        IReadOnlyList<ApuVersionResourceRef> left,
        IReadOnlyList<ApuVersionResourceRef> right,
        IReadOnlyList<ApuVersionResourceRef> baseVersion,
        List<MergeConflict> conflicts,
        List<string> appliedChanges,
        Dictionary<string, MergeConflictResolution> resolutions)
    {
        var baseDict = baseVersion.ToDictionary(r => r.ResourceId, r => r);
        var leftDict = left.ToDictionary(r => r.ResourceId, r => r);
        var rightDict = right.ToDictionary(r => r.ResourceId, r => r);
        var allKeys = new HashSet<string>(leftDict.Keys.Concat(rightDict.Keys));
        var result = new List<ApuVersionResourceRef>();

        foreach (var key in allKeys.OrderBy(k => k))
        {
            if (leftDict.TryGetValue(key, out var l) && rightDict.TryGetValue(key, out var r))
            {
                if (l == r) { result.Add(l!); continue; }
                if (baseDict.TryGetValue(key, out var b))
                {
                    if (b == l) { result.Add(r!); appliedChanges.Add($"Resource '{key}': using right version"); continue; }
                    if (b == r) { result.Add(l!); appliedChanges.Add($"Resource '{key}': using left version"); continue; }
                    var resolutionKey = $"Resource:{key}";
                    if (resolutions.TryGetValue(resolutionKey, out var resolution))
                    {
                        if (resolution == MergeConflictResolution.UseLeft) { result.Add(l!); appliedChanges.Add($"Resource '{key}': auto-resolved using left"); }
                        else { result.Add(r!); appliedChanges.Add($"Resource '{key}': auto-resolved using right"); }
                    }
                    else
                    {
                        result.Add(l!);
                        conflicts.Add(CreateConflict(key, "Price", $"{l!.Price}", $"{r!.Price}", $"{b!.Price}"));
                    }
                }
                else
                {
                    result.Add(r!);
                    appliedChanges.Add($"Resource '{key}': new, using right branch value");
                }
            }
            else if (leftDict.TryGetValue(key, out var leftVal))
            {
                result.Add(leftVal!);
            }
            else
            {
                var rightVal = rightDict[key];
                result.Add(rightVal);
                appliedChanges.Add($"Resource '{key}': added from right branch");
            }
        }

        return result;
    }

    private static List<ApuVersionEconomicConcept> MergeEconomicConcepts(
        IReadOnlyList<ApuVersionEconomicConcept> left,
        IReadOnlyList<ApuVersionEconomicConcept> right,
        IReadOnlyList<ApuVersionEconomicConcept> baseVersion,
        List<MergeConflict> conflicts,
        List<string> appliedChanges,
        Dictionary<string, MergeConflictResolution> resolutions)
    {
        var baseDict = baseVersion.ToDictionary(c => c.Code, c => c);
        var leftDict = left.ToDictionary(c => c.Code, c => c);
        var rightDict = right.ToDictionary(c => c.Code, c => c);
        var allKeys = new HashSet<string>(leftDict.Keys.Concat(rightDict.Keys));
        var result = new List<ApuVersionEconomicConcept>();

        foreach (var key in allKeys.OrderBy(k => k))
        {
            if (leftDict.TryGetValue(key, out var l) && rightDict.TryGetValue(key, out var r))
            {
                if (l == r) { result.Add(l); continue; }
                if (baseDict.TryGetValue(key, out var b))
                {
                    if (b == l) { result.Add(r); appliedChanges.Add($"Economic concept '{key}': using right version"); continue; }
                    if (b == r) { result.Add(l); appliedChanges.Add($"Economic concept '{key}': using left version"); continue; }
                    var resolutionKey = $"Economic:{key}";
                    if (resolutions.TryGetValue(resolutionKey, out var resolution))
                    {
                        if (resolution == MergeConflictResolution.UseLeft) { result.Add(l); appliedChanges.Add($"Economic concept '{key}': auto-resolved using left"); }
                        else { result.Add(r); appliedChanges.Add($"Economic concept '{key}': auto-resolved using right"); }
                    }
                    else
                    {
                        result.Add(l);
                        conflicts.Add(CreateConflict(key, "Value", $"{l.Value}", $"{r.Value}", $"{b.Value}"));
                    }
                }
                else
                {
                    result.Add(r);
                    appliedChanges.Add($"Economic concept '{key}': new, using right branch value");
                }
            }
            else if (leftDict.ContainsKey(key))
            {
                result.Add(leftDict[key]);
            }
            else
            {
                result.Add(rightDict[key]);
                appliedChanges.Add($"Economic concept '{key}': added from right branch");
            }
        }

        return result;
    }

    private static List<ApuVersionScenario> MergeScenarioLists(
        IReadOnlyList<ApuVersionScenario> left,
        IReadOnlyList<ApuVersionScenario> right,
        IReadOnlyList<ApuVersionScenario> baseVersion,
        List<MergeConflict> conflicts,
        List<string> appliedChanges,
        Dictionary<string, MergeConflictResolution> resolutions)
    {
        var baseDict = baseVersion.ToDictionary(s => s.ScenarioId, s => s);
        var leftDict = left.ToDictionary(s => s.ScenarioId, s => s);
        var rightDict = right.ToDictionary(s => s.ScenarioId, s => s);
        var allKeys = new HashSet<string>(leftDict.Keys.Concat(rightDict.Keys));
        var result = new List<ApuVersionScenario>();

        foreach (var key in allKeys.OrderBy(k => k))
        {
            if (leftDict.ContainsKey(key) && !rightDict.ContainsKey(key))
            {
                result.Add(leftDict[key]);
            }
            else if (!leftDict.ContainsKey(key) && rightDict.ContainsKey(key))
            {
                result.Add(rightDict[key]);
                appliedChanges.Add($"Scenario '{key}': added from right branch");
            }
            else
            {
                result.Add(leftDict[key]);
            }
        }

        return result;
    }

    private static decimal ResolveNumericConflict(
        string property,
        decimal left,
        decimal right,
        decimal baseVal,
        List<MergeConflict> conflicts,
        List<string> appliedChanges,
        Dictionary<string, MergeConflictResolution> resolutions)
    {
        if (left == right) return left;
        if (left == baseVal) { appliedChanges.Add($"'{property}': using right ({right}) — left unchanged"); return right; }
        if (right == baseVal) { appliedChanges.Add($"'{property}': using left ({left}) — right unchanged"); return left; }
        var resolutionKey = $"Numeric:{property}";
        if (resolutions.TryGetValue(resolutionKey, out var resolution))
        {
            if (resolution == MergeConflictResolution.UseLeft) { appliedChanges.Add($"'{property}': auto-resolved using left ({left})"); return left; }
            if (resolution == MergeConflictResolution.UseRight) { appliedChanges.Add($"'{property}': auto-resolved using right ({right})"); return right; }
            if (resolution == MergeConflictResolution.UseNewer) { var v = Math.Max(left, right); appliedChanges.Add($"'{property}': auto-resolved using newer ({v})"); return v; }
            if (resolution == MergeConflictResolution.UseOlder) { var v = Math.Min(left, right); appliedChanges.Add($"'{property}': auto-resolved using older ({v})"); return v; }
        }
        conflicts.Add(CreateConflict("_numeric_", property, $"{left:F6}", $"{right:F6}", $"{baseVal:F6}"));
        return baseVal;
    }

    private static MergeConflict CreateConflict(string id, string prop, string leftVal, string rightVal, string baseVal)
    {
        return new MergeConflict
        {
            Identifier = id,
            Property = prop,
            LeftValue = leftVal,
            RightValue = rightVal,
            BaseValue = baseVal,
            Resolution = MergeConflictResolution.Manual
        };
    }
}
