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
