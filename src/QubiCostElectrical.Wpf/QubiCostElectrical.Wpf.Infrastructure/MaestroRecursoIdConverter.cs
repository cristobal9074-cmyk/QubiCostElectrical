// TODO(null-safety): archivo proveniente de decompilación; nulabilidad pendiente de revisión manual.
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8619, CS8625, CS8622, CS4014
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
		object obj;
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
