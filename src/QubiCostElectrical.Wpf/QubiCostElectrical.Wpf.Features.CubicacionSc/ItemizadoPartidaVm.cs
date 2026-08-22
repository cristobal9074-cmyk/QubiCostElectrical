#nullable enable
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

	public string MaterialDisplay
	{
		get
		{
			if (!(MaterialTotal > 0m))
			{
				return "";
			}
			return $"$ {MaterialTotal:N0}";
		}
	}

	public string ManoObraDisplay
	{
		get
		{
			if (!(ManoObraTotal > 0m))
			{
				return "";
			}
			return $"$ {ManoObraTotal:N0}";
		}
	}

	public string CostoDirectoDisplay
	{
		get
		{
			if (!(CostoDirecto > 0m))
			{
				return "";
			}
			return $"$ {CostoDirecto:N0}";
		}
	}

	public bool TieneCubicacion
	{
		get
		{
			if (!(MaterialTotal > 0m))
			{
				return ManoObraTotal > 0m;
			}
			return true;
		}
	}
}
