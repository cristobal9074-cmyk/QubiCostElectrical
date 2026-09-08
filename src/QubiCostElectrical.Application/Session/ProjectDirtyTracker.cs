using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Tracking interno de scopes dirty / versiones. Usado solo por <see cref="BudgetProjectSession"/>.
/// </summary>
internal sealed class ProjectDirtyTracker
{
    private readonly Dictionary<string, int> scopeVersions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> dirtyScopes = new(StringComparer.OrdinalIgnoreCase);

    public bool IsDirty { get; private set; }

    public IReadOnlyCollection<string> GetDirtyScopes() => dirtyScopes.ToArray();

    public int GetScopeVersion(string normalizedScope) =>
        scopeVersions.TryGetValue(normalizedScope, out int version) ? version : 0;

    public bool HasDirtyScope(string normalizedScope) => dirtyScopes.Contains(normalizedScope);

    public void ClearDirtyScopes(IEnumerable<string>? normalizedScopes = null)
    {
        if (normalizedScopes == null)
        {
            dirtyScopes.Clear();
            return;
        }

        foreach (var scope in normalizedScopes)
            dirtyScopes.Remove(scope);
    }

    public string Touch(string normalizedScope, bool markDirty = true)
    {
        scopeVersions[normalizedScope] = scopeVersions.TryGetValue(normalizedScope, out int current)
            ? current + 1
            : 1;

        if (markDirty
            && !string.Equals(normalizedScope, "saved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(normalizedScope, "reset", StringComparison.OrdinalIgnoreCase))
        {
            dirtyScopes.Add(normalizedScope);
            IsDirty = true;
        }

        return normalizedScope;
    }

    public void MarkDirtyFlag() => IsDirty = true;

    public void MarkSaved()
    {
        IsDirty = false;
        dirtyScopes.Clear();
    }

    public void ResetVersionsAndDirty()
    {
        scopeVersions.Clear();
        dirtyScopes.Clear();
        IsDirty = false;
    }
}
