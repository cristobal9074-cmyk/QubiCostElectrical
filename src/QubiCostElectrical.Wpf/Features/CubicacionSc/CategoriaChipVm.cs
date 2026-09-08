#nullable enable
using System.Collections.Concurrent;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CategoriaChipVm : ViewModelBase
{
	private static readonly ConcurrentDictionary<string, SolidColorBrush> BrushCache = new ConcurrentDictionary<string, SolidColorBrush>();

	private bool isActive;

	public string Nombre { get; init; } = "";

	public int Count { get; init; }

	public string ColorHex { get; init; } = "#64748b";

	public SolidColorBrush DotBrush => BrushCache.GetOrAdd(ColorHex, (string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));

	public bool IsActive
	{
		get
		{
			return isActive;
		}
		set
		{
			SetProperty(ref isActive, value, "IsActive");
		}
	}
}
