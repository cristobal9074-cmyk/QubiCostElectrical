#nullable enable
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class CubicacionPartidaHeaderAdapter : ICubicacionPartidaHeaderHost
{
	private readonly CubicacionPartidaHeader _header;

	public TextBox Unidad => _header.Unidad;

	public TextBox Cantidad => _header.Cantidad;

	public TextBox Observaciones => _header.Observaciones;

	public Border CodigoCell => _header.CodigoCell;

	public Border DescripcionCell => _header.DescripcionCell;

	public Border UnidadCell => _header.UnidadCell;

	public Border CantidadCell => _header.CantidadCell;

	public Border MaterialCell => _header.MaterialCell;

	public Border ManoObraCell => _header.ManoObraCell;

	public Border TotalCell => _header.TotalCell;

	public Border SubtotalCell => _header.SubtotalCell;

	public Border GgUtCell => _header.GgUtCell;

	public Border UfUnitCell => _header.UfUnitCell;

	public Border SubtotalUfCell => _header.SubtotalUfCell;

	public CubicacionPartidaHeaderAdapter(CubicacionPartidaHeader header)
	{
		_header = header;
	}
}
