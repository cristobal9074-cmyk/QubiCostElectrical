#nullable enable
using System.Windows;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public interface ICubicacionTotalsHost
{
	UIElement TotalsBar { get; }

	UIElement StatusBar { get; }
}
