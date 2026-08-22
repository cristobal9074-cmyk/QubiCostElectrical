#nullable enable
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public interface ICubicacionAutocompleteHost
{
	Popup Popup { get; }

	ListBox Results { get; }
}
