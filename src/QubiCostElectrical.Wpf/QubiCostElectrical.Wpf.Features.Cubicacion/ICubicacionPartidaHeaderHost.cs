#nullable enable
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public interface ICubicacionPartidaHeaderHost
{
	TextBox Unidad { get; }

	TextBox Cantidad { get; }

	TextBox Observaciones { get; }

	Border CodigoCell { get; }

	Border DescripcionCell { get; }

	Border UnidadCell { get; }

	Border CantidadCell { get; }

	Border MaterialCell { get; }

	Border ManoObraCell { get; }

	Border TotalCell { get; }

	Border SubtotalCell { get; }

	Border GgUtCell { get; }

	Border UfUnitCell { get; }

	Border SubtotalUfCell { get; }
}
