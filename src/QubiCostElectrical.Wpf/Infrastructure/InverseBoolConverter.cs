using System.Globalization;
using System.Windows.Data;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>Invierte un bool (p. ej. IsEnabled cuando IsBusy).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not bool b || !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not bool b || !b;
}
