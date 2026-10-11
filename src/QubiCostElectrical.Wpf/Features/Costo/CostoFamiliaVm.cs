using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Dashboard;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf.Features.Costo;

public sealed class FerreteriaFamiliaVm
{
    public string Familia { get; init; } = "";
    public string MaterialesLabel { get; init; } = "";
    public string MaterialesUfLabel { get; init; } = "";
    public string FerreteriaLabel { get; init; } = "";
    public string FerreteriaUfLabel { get; init; } = "";
    public bool IsTotal { get; init; }
}

public sealed class CostoFamiliaVm
{
    public string Familia { get; init; } = "";
    public int MaterialesCount { get; init; }
    public decimal TotalClp { get; init; }
    public decimal TotalUf { get; init; }
    public string TotalLabel { get; init; } = "";
    public string TotalMonedaLabel { get; init; } = "";
    public string CountLabel { get; init; } = "";
    public string ShareLabel { get; init; } = "";
    public double Share { get; init; }
    public double BarWidth { get; init; }
}

public sealed class FamiliaShareWidthConverter : System.Windows.Data.IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        double share = values.Length > 0 && values[0] is double s ? s : 0d;
        double width = values.Length > 1 && values[1] is double w ? w : 0d;
        if (share <= 0d || width <= 1d || double.IsNaN(width) || double.IsInfinity(width))
            return 0d;
        return Math.Max(6d, share * width);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
