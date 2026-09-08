using System.Globalization;
using System.Windows.Data;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>OR lógico de varios bool (p. ej. IsReadOnly = TienePisos || IsGridReadOnly).</summary>
public sealed class BoolOrConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null) return false;
        foreach (var value in values)
        {
            if (value is bool b && b)
                return true;
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
