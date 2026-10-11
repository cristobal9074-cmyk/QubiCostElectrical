#nullable enable
namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public abstract class FlatRowVm
{
    public abstract bool IsGroupHeader { get; }
    public bool IsMaterial => !IsGroupHeader;
}
