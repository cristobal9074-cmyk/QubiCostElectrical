using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Infrastructure;

/// <summary>
/// Convierte bool a FontWeight (True=SemiBold, False=Normal).
/// </summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Convierte null/non-null a Visibility. Null→Visible, Non-null→Collapsed.
/// Con ConverterParameter=Inverse: Null→Collapsed, Non-null→Visible.
/// Con ConverterParameter=NonEmpty: visible solo si el valor tiene contenido real
/// (string no vacío/blanco, o número distinto de cero); útil para mensajes de error/estado.
/// Con ConverterParameter=Empty: visible solo si el valor está vacío/ausente (inverso de NonEmpty).
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? mode = parameter as string;

        if (string.Equals(mode, "NonEmpty", StringComparison.OrdinalIgnoreCase))
            return IsContentEmpty(value) ? Visibility.Collapsed : Visibility.Visible;

        if (string.Equals(mode, "Empty", StringComparison.OrdinalIgnoreCase))
            return IsContentEmpty(value) ? Visibility.Visible : Visibility.Collapsed;

        bool isNull = value == null;
        if (string.Equals(mode, "Inverse", StringComparison.OrdinalIgnoreCase))
            isNull = !isNull;
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsContentEmpty(object? value) => value switch
    {
        null => true,
        string s => string.IsNullOrWhiteSpace(s),
        int i => i == 0,
        long l => l == 0,
        _ => false
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public sealed class RowIndexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DataGridRow row)
        {
            var grid = ItemsControl.ItemsControlFromItemContainer(row) as DataGrid;
            if (grid != null)
            {
                int index = grid.ItemContainerGenerator.IndexFromContainer(row);
                if (index >= 0) return (index + 1).ToString();
            }
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public sealed class StringNotEmptyToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Cadena vacía/blanca/null → Collapsed. Cualquier otra cadena → Visible.
/// </summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

/// <summary>
/// Cadena vacía/blanca → texto de marcador de posición (ConverterParameter). Con contenido, se muestra tal cual.
/// Usado en la columna Observaciones de recursos para sugerir "Nota interna (no sale en la oferta)".
/// </summary>
public sealed class EmptyToPlaceholderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? s : (parameter as string ?? "");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public sealed class MaterialAutocompleteMatchReasonConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return "";

        string query = values[0] as string ?? "";
        if (values[1] is not QubiCostElectrical.MaterialRow material)
            return "";

        string queryKey = Normalize(query);
        if (string.IsNullOrWhiteSpace(queryKey))
            return "Sugerido";

        string code = Normalize(material.Codigo);
        string desc = Normalize(material.Descripcion);

        if (code.Equals(queryKey, StringComparison.OrdinalIgnoreCase))
            return "Código exacto";
        if (code.StartsWith(queryKey, StringComparison.OrdinalIgnoreCase))
            return "Código";
        if (desc.StartsWith(queryKey, StringComparison.OrdinalIgnoreCase))
            return "Descripción";
        if (code.Contains(queryKey, StringComparison.OrdinalIgnoreCase))
            return "Código parcial";
        if (desc.Contains(queryKey, StringComparison.OrdinalIgnoreCase))
            return "Descripción parcial";

        return "Catálogo";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString()
            .Replace("-", "")
            .Replace(" ", "")
            .Replace(".", "")
            .Replace("_", "")
            .Replace("/", "")
            .ToUpperInvariant();
    }
}
