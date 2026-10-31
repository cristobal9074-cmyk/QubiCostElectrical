using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Review;

public partial class ReviewView : UserControl
{
    public ReviewView()
    {
        InitializeComponent();
    }

    private void OnUserControlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (DataContext is ReviewViewModel vm)
            {
                if (vm.HasSearchText)
                {
                    vm.ClearSearchCommand.Execute(null);
                    e.Handled = true;
                }
                else if (vm.HasActiveFilters && vm.ResetFilterCommand.CanExecute(null))
                {
                    vm.ResetFilterCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}

public sealed class ConfidenceToStrokeDashArrayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is decimal dec) pct = (double)dec;
        else if (value is double dbl) pct = dbl;
        else if (value is int i) pct = i;

        pct = Math.Clamp(pct, 0, 100);

        // Para un Ellipse de 100x100 con StrokeThickness=8:
        // Diámetro medio = 100 - 8 = 92.
        // Perímetro = pi * 92 = 289.026.
        // En unidades Dash (relativo a StrokeThickness = 8):
        // Total dash = 289.026 / 8 = 36.128.
        double totalDash = 36.128;
        double filled = totalDash * (pct / 100.0);
        double empty = totalDash - filled;

        return new DoubleCollection(new[] { filled, empty });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public sealed class ConfidenceToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xDC, 0x26, 0x26));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is decimal dec) pct = (double)dec;
        else if (value is double dbl) pct = dbl;
        else if (value is int i) pct = i;

        if (pct >= 80) return GreenBrush;
        if (pct >= 50) return BlueBrush;
        return RedBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class BlockerCountToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int c ? c : 0;
        return count > 0 ? RedBrush : GreenBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class BlockerCountToSoftBgConverter : IValueConverter
{
    private static readonly SolidColorBrush SoftRed = new(Color.FromRgb(0xFE, 0xF2, 0xF2));
    private static readonly SolidColorBrush SoftGreen = new(Color.FromRgb(0xEC, 0xFD, 0xF5));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int c ? c : 0;
        return count > 0 ? SoftRed : SoftGreen;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class BlockerCountToBorderBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush BorderRed = new(Color.FromRgb(0xFE, 0xCA, 0xCA));
    private static readonly SolidColorBrush BorderGreen = new(Color.FromRgb(0xA7, 0xF3, 0xD0));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int c ? c : 0;
        return count > 0 ? BorderRed : BorderGreen;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class BlockerCountToBadgeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int c ? c : 0;
        return count > 0
            ? $"PRE-FLIGHT GATE: BLOQUEADO ({count:N0} CRÍTICOS)"
            : "PRE-FLIGHT GATE: APROBADO PARA OFERTA";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class BlockerCountToBottomStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = value is int c ? c : 0;
        return count > 0
            ? $"⛔ Oferta Bloqueada ({count:N0} críticos)"
            : "✓ Aprobado para Exportar";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
