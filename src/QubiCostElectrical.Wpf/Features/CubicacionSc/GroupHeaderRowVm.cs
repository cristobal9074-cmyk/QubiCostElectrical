#nullable enable
namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class GroupHeaderRowVm : FlatRowVm
{
    public override bool IsGroupHeader => true;

    public string Nombre { get; init; } = "";

    public decimal TotalMl { get; init; }

    public int MaterialCount { get; init; }

    public bool IsExpanded { get; set; } = true;

    public string MaterialCountDisplay => MaterialCount.ToString();

    public string Subtitle => $"{MaterialCount} material(es)";

    public string CodigoDisplay => $"📁 {Nombre.ToUpperInvariant()} ({MaterialCount} materiales)";

    public string Unidad => "";

    public string TotalDisplay => TotalMl > 0 ? TotalMl.ToString("#,##0.##") : "";
}
