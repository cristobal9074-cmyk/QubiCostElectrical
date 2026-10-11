using System;

namespace QubiCostElectrical.Wpf.Features.Costo;

/// <summary>Brushes temáticos de la pantalla Costo (extraído de CostoViewModel por el gate de MaxLineCount).</summary>
public static class CostoUiBrushes
{
    public static System.Windows.Media.Brush Parse(string resourceKey, double opacity = 1)
    {
        System.Windows.Media.Color c;
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is System.Windows.Media.SolidColorBrush themed)
        {
            c = themed.Color;
        }
        else
        {
            c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")!;
        }

        if (opacity < 1)
            c.A = (byte)Math.Clamp((int)(opacity * 255), 0, 255);
        var brush = new System.Windows.Media.SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }
}
