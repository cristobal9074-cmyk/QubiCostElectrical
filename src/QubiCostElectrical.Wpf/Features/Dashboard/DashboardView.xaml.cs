using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
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

    private void OnTopPartidaDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is DashboardPartidaVm partida &&
            DataContext is DashboardViewModel vm)
        {
            if (vm.GoToPartidaCommand.CanExecute(partida))
                vm.GoToPartidaCommand.Execute(partida);
        }
    }
}

/// <summary>
/// Convierte un porcentaje 0–100 a StrokeDashArray para el anillo de preparación
/// (Ellipse 120×120, StrokeThickness=10 → perímetro medio ≈ 34.56 unidades dash).
/// </summary>
public sealed class ReadinessToStrokeDashArrayConverter : IValueConverter
{
    private static readonly double TotalDash = Math.PI * (120.0 - 10.0) / 10.0;

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

/// <summary>Rank (1..N) → Canvas.Left de la barra del Pareto (plot 760×300, barras 44px, paso 68px).</summary>
public sealed class ParetoBarLeftConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int rank = value is int i ? i : 1;
        return 57.0 + (Math.Max(1, rank) - 1) * 68.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>[SharePct, ParetoAxisMaxPct] → alto en px de la barra (200px = eje completo).</summary>
public sealed class ParetoBarHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        double share = values.Length > 0 && values[0] is double s ? s : 0;
        double axisMax = values.Length > 1 && values[1] is double m && m > 0 ? m : 10;
        return Math.Max(1.5, Math.Min(200, share / axisMax * 200.0));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Partida → centro X de su punto en la curva acumulada del Pareto.</summary>
public sealed class ParetoPointXConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int rank = value is DashboardPartidaVm p ? p.Rank : 1;
        return 79.0 + (Math.Max(1, rank) - 1) * 68.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Partida → Y de su punto acumulado (0% → y250, 100% → y50; 2px por punto %).</summary>
public sealed class ParetoPointYConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double accum = value is DashboardPartidaVm p ? p.AccumPct : 0;
        return 250.0 - Math.Clamp(accum, 0, 100) * 2.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Top partidas → PointCollection de la polilínea de acumulado del Pareto.</summary>
public sealed class ParetoCumPointsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var points = new PointCollection();
        if (value is IEnumerable<DashboardPartidaVm> list)
        {
            foreach (var p in list.OrderBy(x => x.Rank))
                points.Add(new Point(79.0 + (Math.Max(1, p.Rank) - 1) * 68.0, 250.0 - Math.Clamp(p.AccumPct, 0, 100) * 2.0));
        }
        return points;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>ParetoAxisMaxPct + índice de tick (0..5) → etiqueta "12%".</summary>
public sealed class ParetoAxisLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double axisMax = value is double m && m > 0 ? m : 10;
        int tick = parameter is string s && int.TryParse(s, out int t) ? t : 0;
        return $"{axisMax * tick / 5.0:N0}%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>DashboardParetoSummary → suma de acumulado de las partidas listadas ("75,6%").</summary>
public sealed class ParetoAccumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DashboardParetoSummary s ? $"{s.PctA + s.PctB + s.PctC:N1}%" : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>DashboardParetoSummary → Canvas.Top del rótulo flotante sobre el último punto de acumulado.</summary>
public sealed class ParetoAccumYConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DashboardParetoSummary s) return 90.0;
        double total = Math.Clamp(s.PctA + s.PctB + s.PctC, 0, 100);
        return Math.Max(56.0, 250.0 - total * 2.0 - 14.0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Top partidas → Canvas.Left del rótulo flotante sobre el último punto de acumulado.</summary>
public sealed class ParetoAccumXConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double last = 79.0;
        if (value is IEnumerable<DashboardPartidaVm> list)
        {
            int maxRank = 1;
            foreach (var p in list)
                if (p.Rank > maxRank) maxRank = p.Rank;
            last = 79.0 + (maxRank - 1) * 68.0;
        }
        return Math.Min(last - 20.0, 730.0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>
/// Curva de sensibilidad → geometría WPF. Parámetro: "line" (polilínea abierta),
/// "area" (área hasta la base y235) o "markers" (círculos de cada punto).
/// Plot: x 60..730, y 20..235.
/// </summary>
public sealed class SensitivityChartConverter : IValueConverter
{
    private const double Left = 60.0;
    private const double Right = 730.0;
    private const double Top = 20.0;
    private const double Bottom = 235.0;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var pts = ToPoints(value);
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        if (pts.Count == 0)
        {
            ctx.Close();
            geometry.Freeze();
            return geometry;
        }

        string mode = parameter as string ?? "line";

        if (mode == "markers")
        {
            foreach (var pt in pts)
            {
                const double r = 3.5;
                ctx.BeginFigure(new Point(pt.X - r, pt.Y), true, true);
                ctx.ArcTo(new Point(pt.X + r, pt.Y), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
                ctx.ArcTo(new Point(pt.X - r, pt.Y), new Size(r, r), 0, false, SweepDirection.Counterclockwise, true, false);
            }
        }
        else if (mode == "area")
        {
            ctx.BeginFigure(new Point(pts[0].X, Bottom), false, true);
            foreach (var pt in pts)
                ctx.LineTo(pt, true, false);
            ctx.LineTo(new Point(pts[^1].X, Bottom), true, false);
        }
        else
        {
            ctx.BeginFigure(pts[0], false, false);
            foreach (var pt in pts.Skip(1))
                ctx.LineTo(pt, true, false);
        }

        ctx.Close();
        geometry.Freeze();
        return geometry;
    }

    private static List<Point> ToPoints(object? value)
    {
        var result = new List<Point>();
        if (value is not IEnumerable<DashboardSensitivityPoint> curve)
            return result;
        var list = curve.ToList();
        if (list.Count < 2)
            return result;

        decimal minX = list.Min(p => p.MaterialVariationPct);
        decimal maxX = list.Max(p => p.MaterialVariationPct);
        decimal minY = list.Min(p => p.TotalVentaUf);
        decimal maxY = list.Max(p => p.TotalVentaUf);

        foreach (var p in list)
        {
            double x = Left + (maxX == minX ? 0.5 : (double)((p.MaterialVariationPct - minX) / (maxX - minX))) * (Right - Left);
            double y = maxY == minY
                ? (Top + Bottom) / 2.0
                : Bottom - (double)((p.TotalVentaUf - minY) / (maxY - minY)) * (Bottom - Top);
            result.Add(new Point(x, y));
        }
        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Curva de sensibilidad + tick vertical (0..5 desde arriba) → etiqueta del eje Y en UF.</summary>
public sealed class SensitivityAxisLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<DashboardSensitivityPoint> curve)
            return "";
        var list = curve.ToList();
        if (list.Count == 0)
            return "";
        int tick = parameter is string s && int.TryParse(s, out int t) ? t : 0;
        decimal max = list.Max(p => p.TotalVentaUf);
        decimal min = list.Min(p => p.TotalVentaUf);
        decimal v = max - (max - min) * tick / 5.0m;
        return $"{v:N0}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Curva de sensibilidad + pct (parámetro "-10".."20") → Y del punto en el plot.</summary>
public sealed class SensitivityPointYConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<DashboardSensitivityPoint> curve || parameter is not string pStr || !decimal.TryParse(pStr, NumberStyles.Any, culture, out var target))
            return 0.0;
        var list = curve.ToList();
        if (list.Count < 2)
            return 0.0;
        decimal minX = list.Min(p => p.MaterialVariationPct);
        decimal maxX = list.Max(p => p.MaterialVariationPct);
        decimal minY = list.Min(p => p.TotalVentaUf);
        decimal maxY = list.Max(p => p.TotalVentaUf);
        var match = list.FirstOrDefault(p => p.MaterialVariationPct == target) ?? list[0];
        return maxY == minY
            ? (20.0 + 235.0) / 2.0
            : 235.0 - (double)((match.TotalVentaUf - minY) / (maxY - minY)) * (235.0 - 20.0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Curva de sensibilidad + pct (parámetro "-10".."20") → X del punto en el plot.</summary>
public sealed class SensitivityPointXConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<DashboardSensitivityPoint> curve || parameter is not string pStr || !decimal.TryParse(pStr, NumberStyles.Any, culture, out var target))
            return 60.0;
        var list = curve.ToList();
        if (list.Count < 2)
            return 60.0;
        decimal minX = list.Min(p => p.MaterialVariationPct);
        decimal maxX = list.Max(p => p.MaterialVariationPct);
        var match = list.FirstOrDefault(p => p.MaterialVariationPct == target) ?? list[0];
        return 60.0 + (maxX == minX ? 0.5 : (double)((match.MaterialVariationPct - minX) / (maxX - minX))) * (730.0 - 60.0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Variación de materiales → etiqueta del eje X ("Base", "+10%", "-5%").</summary>
public sealed class SensitivityXLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        decimal pct = value is DashboardSensitivityPoint p ? p.MaterialVariationPct : 0m;
        if (pct == 0m) return "Base";
        return $"{pct:+0;-0;0}%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>[punto de sensibilidad, curva] → Canvas.Left centrado de su etiqueta en el eje X.</summary>
public sealed class SensitivityItemXConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not DashboardSensitivityPoint pt ||
            values[1] is not IEnumerable<DashboardSensitivityPoint> curve)
            return 25.0;
        var list = curve.ToList();
        if (list.Count < 2)
            return 25.0;
        decimal minX = list.Min(p => p.MaterialVariationPct);
        decimal maxX = list.Max(p => p.MaterialVariationPct);
        double x = 60.0 + (maxX == minX ? 0.5 : (double)((pt.MaterialVariationPct - minX) / (maxX - minX))) * (730.0 - 60.0);
        return x - 35.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
