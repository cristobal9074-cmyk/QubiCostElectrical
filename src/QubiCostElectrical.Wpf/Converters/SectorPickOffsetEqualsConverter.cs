using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace QubiCostElectrical.Wpf.Converters;

/// <summary>
/// Comprueba si el sector absoluto fijo de ESTA columna (pasado como ConverterParameter al
/// armar la columna) está entre los offsets "pickeados" de la fila (FormulaPickTargetOffsets)
/// — usado para resaltar CADA celda apuntada al armar una referencia entre casillas con las
/// flechas, incluso si hay varias en la MISMA fila (suma horizontal).
/// </summary>
public sealed class SectorPickOffsetEqualsConverter : IValueConverter
{
    public static readonly SectorPickOffsetEqualsConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is IReadOnlyCollection<int> offsets && parameter is int columnOffset && offsets.Contains(columnOffset);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
