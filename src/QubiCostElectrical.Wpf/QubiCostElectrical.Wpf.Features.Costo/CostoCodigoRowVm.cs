#nullable enable
namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class CostoCodigoRowVm
{
	public string Codigo { get; init; } = "";

	public string Descripcion { get; set; } = "";

	public string Unidad { get; set; } = "";

	public decimal TotalClp { get; set; }

	public string TotalLabel { get; set; } = "";
}
