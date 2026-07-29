namespace QubiCostElectrical;

public interface IPlanningPermissionChecker
{
    bool Has(string permission);
}

public sealed class AllowAllPlanningPermissionChecker : IPlanningPermissionChecker
{
    public bool Has(string permission) => true;
}

public sealed class HashSetPlanningPermissionChecker : IPlanningPermissionChecker
{
    private readonly System.Collections.Generic.HashSet<string> _granted;

    public HashSetPlanningPermissionChecker(System.Collections.Generic.IEnumerable<string> granted)
    {
        _granted = new System.Collections.Generic.HashSet<string>(granted ?? System.Array.Empty<string>(), System.StringComparer.Ordinal);
    }

    public bool Has(string permission) => _granted.Contains(permission);
}

public sealed class DenyAllPlanningPermissionChecker : IPlanningPermissionChecker
{
    public bool Has(string permission) => false;
}
