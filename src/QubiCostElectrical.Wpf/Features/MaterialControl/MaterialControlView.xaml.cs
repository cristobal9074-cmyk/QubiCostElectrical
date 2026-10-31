using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.MaterialControl;

public partial class MaterialControlView : UserControl
{
    public MaterialControlView()
    {
        InitializeComponent();
    }
}

public sealed class PercentToProgressBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AmberBrush = new(Color.FromRgb(0xD9, 0x77, 0x06)); // Amber 600 - Sobreconsumo
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x10, 0xB9, 0x81));
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(0x25, 0x63, 0xEB));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(0xCB, 0xD5, 0xE1));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is double d) pct = d;
        else if (value is decimal dec) pct = (double)dec;
        else if (value is int i) pct = i;

        if (pct > 100.0) return AmberBrush;
        if (pct >= 100.0) return GreenBrush;
        if (pct > 0.0) return BlueBrush;
        return GrayBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StatusToBadgeBgConverter : IValueConverter
{
    private static readonly SolidColorBrush SoftAmber = new(Color.FromRgb(0xFE, 0xF3, 0xC7)); // Amber 100
    private static readonly SolidColorBrush SoftGreen = new(Color.FromRgb(0xEC, 0xFD, 0xF5));
    private static readonly SolidColorBrush SoftBlue = new(Color.FromRgb(0xEF, 0xF6, 0xFF));
    private static readonly SolidColorBrush SoftGray = new(Color.FromRgb(0xF1, 0xF5, 0xF9));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value?.ToString() ?? "";
        if (text.Contains("Sobreconsumo", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Sobrepedido", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("No presupuestado", StringComparison.OrdinalIgnoreCase))
            return SoftAmber;
        if (text.Contains("Completado", StringComparison.OrdinalIgnoreCase))
            return SoftGreen;
        if (text.Contains("En proceso", StringComparison.OrdinalIgnoreCase))
            return SoftBlue;
        return SoftGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StatusToBadgeBorderConverter : IValueConverter
{
    private static readonly SolidColorBrush BorderAmber = new(Color.FromRgb(0xFC, 0xD3, 0x4D)); // Amber 300
    private static readonly SolidColorBrush BorderGreen = new(Color.FromRgb(0xA7, 0xF3, 0xD0));
    private static readonly SolidColorBrush BorderBlue = new(Color.FromRgb(0xBF, 0xDB, 0xFE));
    private static readonly SolidColorBrush BorderGray = new(Color.FromRgb(0xE2, 0xE8, 0xF0));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value?.ToString() ?? "";
        if (text.Contains("Sobreconsumo", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Sobrepedido", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("No presupuestado", StringComparison.OrdinalIgnoreCase))
            return BorderAmber;
        if (text.Contains("Completado", StringComparison.OrdinalIgnoreCase))
            return BorderGreen;
        if (text.Contains("En proceso", StringComparison.OrdinalIgnoreCase))
            return BorderBlue;
        return BorderGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class StatusToBadgeFgConverter : IValueConverter
{
    private static readonly SolidColorBrush TextAmber = new(Color.FromRgb(0x78, 0x35, 0x0F)); // Amber 900 (> 7:1 WCAG AAA)
    private static readonly SolidColorBrush TextGreen = new(Color.FromRgb(0x06, 0x5F, 0x46));
    private static readonly SolidColorBrush TextBlue = new(Color.FromRgb(0x1D, 0x4E, 0xD8));
    private static readonly SolidColorBrush TextGray = new(Color.FromRgb(0x47, 0x55, 0x69));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value?.ToString() ?? "";
        if (text.Contains("Sobreconsumo", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Sobrepedido", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("No presupuestado", StringComparison.OrdinalIgnoreCase))
            return TextAmber;
        if (text.Contains("Completado", StringComparison.OrdinalIgnoreCase))
            return TextGreen;
        if (text.Contains("En proceso", StringComparison.OrdinalIgnoreCase))
            return TextBlue;
        return TextGray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class CategoryToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string cat = (value?.ToString() ?? "").ToLowerInvariant();
        if (cat.Contains("caja")) return "📦";
        if (cat.Contains("conduc") || cat.Contains("cable") || cat.Contains("alam")) return "⚡";
        if (cat.Contains("canal") || cat.Contains("tub") || cat.Contains("ban")) return "🛠️";
        if (cat.Contains("artefact") || cat.Contains("enchufe") || cat.Contains("interrup")) return "🔌";
        if (cat.Contains("soport") || cat.Contains("abraz") || cat.Contains("caddy")) return "🧰";
        if (cat.Contains("malla") || cat.Contains("tierra")) return "⚡";
        if (cat.Contains("tabler") || cat.Contains("equip")) return "📟";
        return "🏷️";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
