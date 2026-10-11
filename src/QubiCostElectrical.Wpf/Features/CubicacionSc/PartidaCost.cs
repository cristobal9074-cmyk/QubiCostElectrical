#nullable enable
namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public readonly struct PartidaCost(decimal mat, decimal mo, decimal total)
{
    public decimal Mat { get; } = mat;

    public decimal Mo { get; } = mo;

    public decimal Total { get; } = total;
}
