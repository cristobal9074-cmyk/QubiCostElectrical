#nullable enable
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class ZonaQtyWrapper : ViewModelBase
{
	private string text = "0";

	public string ZonaId { get; init; } = "";

	public string ZonaNombre { get; init; } = "";

	public string Text
	{
		get
		{
			return text;
		}
		set
		{
			if (SetProperty(ref text, value, "Text"))
			{
				OnPropertyChanged("Value");
			}
		}
	}

	public decimal Value
	{
		get
		{
			if (!decimal.TryParse(Text, out var result))
			{
				return 0m;
			}
			return result;
		}
	}

	internal void NotifyValueChanged()
	{
		OnPropertyChanged("Text");
		OnPropertyChanged("Value");
	}
}
