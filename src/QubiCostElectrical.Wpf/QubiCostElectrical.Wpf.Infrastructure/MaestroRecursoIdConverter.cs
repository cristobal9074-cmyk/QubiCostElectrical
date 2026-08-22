#nullable enable
using System;
using System.Globalization;
using System.Windows.Data;

namespace QubiCostElectrical.Wpf.Infrastructure;

public sealed class MaestroRecursoIdConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value ?? "";
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		object? obj;
		if (!(value is string value2) || !string.IsNullOrWhiteSpace(value2))
		{
			obj = value;
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = Binding.DoNothing;
		}
		return obj;
	}
}
