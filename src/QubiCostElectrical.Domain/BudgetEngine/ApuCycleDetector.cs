namespace QubiCostElectrical;

public sealed class ApuCycleDetector
{
    public bool HasCycle(
        string apuId,
        IReadOnlyList<ApuComponent> components,
        IReadOnlyList<ApuDefinition> allDefinitions,
        HashSet<string> visited,
        HashSet<string> inProgress)
    {
        if (inProgress.Contains(apuId))
            return true;

        if (visited.Contains(apuId))
            return false;

        inProgress.Add(apuId);

        foreach (var component in components)
        {
            if (component.ComponentType != ApuComponentType.AuxiliaryApu)
                continue;

            var child = allDefinitions.FirstOrDefault(d => d.Id == component.ReferenceId);
            if (child == null)
                continue;

            if (HasCycle(child.Id, child.Components, allDefinitions, visited, inProgress))
                return true;
        }

        inProgress.Remove(apuId);
        visited.Add(apuId);
        return false;
    }

    public IReadOnlyList<string> FindCyclePath(
        string apuId,
        IReadOnlyList<ApuComponent> components,
        IReadOnlyList<ApuDefinition> allDefinitions)
    {
        var path = new List<string>();
        var inProgress = new HashSet<string>();
        if (FindCyclePathInternal(apuId, components, allDefinitions, new HashSet<string>(), inProgress, path))
            return path.AsReadOnly();
        return Array.Empty<string>();
    }

    private static bool FindCyclePathInternal(
        string apuId,
        IReadOnlyList<ApuComponent> components,
        IReadOnlyList<ApuDefinition> allDefinitions,
        HashSet<string> visited,
        HashSet<string> inProgress,
        List<string> path)
    {
        if (inProgress.Contains(apuId))
        {
            path.Add(apuId);
            return true;
        }

        if (visited.Contains(apuId))
            return false;

        inProgress.Add(apuId);
        path.Add(apuId);

        foreach (var component in components)
        {
            if (component.ComponentType != ApuComponentType.AuxiliaryApu)
                continue;

            var child = allDefinitions.FirstOrDefault(d => d.Id == component.ReferenceId);
            if (child == null)
                continue;

            if (FindCyclePathInternal(child.Id, child.Components, allDefinitions, visited, inProgress, path))
                return true;
        }

        inProgress.Remove(apuId);
        path.RemoveAt(path.Count - 1);
        visited.Add(apuId);
        return false;
    }
}

public sealed class ApuCycleValidationResult
{
    public bool HasCycles { get; init; }
    public IReadOnlyList<string> AffectedApuIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string>> CyclePaths { get; init; } = Array.Empty<IReadOnlyList<string>>();
}
