#nullable enable
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public sealed class CubicacionResourceGridAdapter
{
	private readonly CubicacionResourceGrid _grid;

	public DataGrid Grid => _grid.Grid;

	public DataGridColumn Index => _grid.Index;

	public DataGridColumn Codigo => _grid.Codigo;

	public DataGridColumn Descripcion => _grid.Descripcion;

	public DataGridColumn Unidad => _grid.Unidad;

	public DataGridTextColumn Cantidad => _grid.Cantidad;

	public DataGridColumn MaterialPrice => _grid.MaterialPrice;

	public DataGridColumn LaborPrice => _grid.LaborPrice;

	public DataGridColumn TotalPrice => _grid.TotalPrice;

	public DataGridColumn Subtotal => _grid.Subtotal;

	public DataGridColumn GgUt => _grid.GgUt;

	public DataGridColumn UfUnit => _grid.UfUnit;

	public DataGridColumn SubtotalUf => _grid.SubtotalUf;

	public DataGridColumn Observaciones => _grid.Observaciones;

	public CubicacionResourceGridAdapter(CubicacionResourceGrid grid)
	{
		_grid = grid;
	}
}
