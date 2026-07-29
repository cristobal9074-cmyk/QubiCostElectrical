using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public sealed class CategoriaChipVm : ViewModelBase
{
    private static readonly Dictionary<string, SolidColorBrush> BrushCache = new();

    public string Nombre { get; init; } = "";
    public int Count { get; init; }
    public string ColorHex { get; init; } = "#64748b";

    public SolidColorBrush DotBrush
    {
        get
        {
            if (!BrushCache.TryGetValue(ColorHex, out var brush))
            {
                var color = (Color)ColorConverter.ConvertFromString(ColorHex);
                brush = new SolidColorBrush(color);
                BrushCache[ColorHex] = brush;
            }
            return brush;
        }
    }

    private bool isActive;
    public bool IsActive
    {
        get => isActive;
        set => SetProperty(ref isActive, value);
    }
}

public sealed class ZonaQtyWrapper : ViewModelBase
{
    private string text = "0";

    public string ZonaId { get; init; } = "";
    public string ZonaNombre { get; init; } = "";

    public string Text
    {
        get => text;
        set
        {
            if (SetProperty(ref text, value))
                OnPropertyChanged(nameof(Value));
        }
    }

    public decimal Value => decimal.TryParse(Text, out var v) ? v : 0m;

    internal void NotifyValueChanged()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Value));
    }
}

public abstract class FlatRowVm
{
    public abstract bool IsGroupHeader { get; }
}

public sealed class GroupHeaderRowVm : FlatRowVm
{
    public override bool IsGroupHeader => true;
    public string Nombre { get; init; } = "";
    public decimal TotalMl { get; init; }
    public int MaterialCount { get; init; }
    public bool IsExpanded { get; set; } = true;

    public string MaterialCountDisplay => MaterialCount.ToString();
    public string Subtitle => $"{MaterialCount} material(es)";
}
