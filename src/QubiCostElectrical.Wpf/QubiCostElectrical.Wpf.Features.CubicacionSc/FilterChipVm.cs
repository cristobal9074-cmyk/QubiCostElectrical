#nullable enable
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class FilterChipVm : ViewModelBase
{
	private bool isActive;

	public string Nombre { get; init; } = "";

	public int Count { get; init; }

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

	public string Display => $"{Nombre} ({Count})";
}
