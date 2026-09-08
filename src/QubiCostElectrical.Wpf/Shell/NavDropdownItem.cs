using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public sealed class NavDropdownItem
{
    public string Label { get; init; } = "";
    public ShellRoute? Route { get; init; }
    public string? Badge { get; init; }
    /// <summary>Cuando es true, el ítem se renderiza como separador visual (línea horizontal).</summary>
    public bool IsSeparator { get; init; }

    public static NavDropdownItem Separator() => new() { IsSeparator = true };
}
