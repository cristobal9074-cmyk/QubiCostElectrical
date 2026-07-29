using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public sealed class NavDropdownItem
{
    public required string Label { get; init; }
    public required ShellRoute Route { get; init; }
    public string? Badge { get; init; }
}
