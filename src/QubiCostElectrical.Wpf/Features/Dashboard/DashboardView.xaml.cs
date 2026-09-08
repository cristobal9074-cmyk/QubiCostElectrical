using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Dashboard;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.Load();
        };
    }
}

/// <summary>
/// Convierte un porcentaje 0–100 a StrokeDashArray para el anillo de preparación
/// (Ellipse 76×76, StrokeThickness=9 → perímetro medio ≈ 23.39 unidades dash).
/// </summary>
public sealed class ReadinessToStrokeDashArrayConverter : IValueConverter
{
    private static readonly double TotalDash = Math.PI * (76.0 - 9.0) / 9.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is double dbl) pct = dbl;
        else if (value is decimal dec) pct = (double)dec;
        else if (value is int i) pct = i;
        else if (value is float f) pct = f;

        pct = Math.Clamp(pct, 0, 100);
        double filled = TotalDash * (pct / 100.0);
        double empty = Math.Max(0.0001, TotalDash - filled);
        return new DoubleCollection(new[] { filled, empty });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
