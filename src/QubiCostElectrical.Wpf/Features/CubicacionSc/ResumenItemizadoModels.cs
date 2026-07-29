using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ItemizadoPartidaVm : ViewModelBase
{
    public bool EsSeccion { get; init; }
    public string Id { get; init; } = "";
    public string ItemCode { get; init; } = "";
    public string Descripcion { get; init; } = "";
    public string Unidad { get; init; } = "";
    public decimal Cantidad { get; init; }
    public decimal MaterialTotal { get; init; }
    public decimal ManoObraTotal { get; init; }
    public decimal CostoDirecto { get; init; }

    public string CantidadDisplay => Cantidad.ToString("#,##0.##");
    public string MaterialDisplay => MaterialTotal > 0 ? $"$ {MaterialTotal:N0}" : "";
    public string ManoObraDisplay => ManoObraTotal > 0 ? $"$ {ManoObraTotal:N0}" : "";
    public string CostoDirectoDisplay => CostoDirecto > 0 ? $"$ {CostoDirecto:N0}" : "";
    public bool TieneCubicacion => MaterialTotal > 0 || ManoObraTotal > 0;
}

public sealed class ItemizadoGroupVm : ViewModelBase
{
    public string Nombre { get; init; } = "";
}

public readonly struct PartidaCost(decimal mat, decimal mo, decimal total)
{
    public decimal Mat { get; } = mat;
    public decimal Mo { get; } = mo;
    public decimal Total { get; } = total;
}
