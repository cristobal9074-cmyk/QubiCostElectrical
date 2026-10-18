using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Export;

public sealed class ExportActionVm
{
    public ExportActionVm(string title, string description, string buttonText, ICommand command, bool primary = false, string iconText = "")
    {
        Title = title;
        Description = description;
        ButtonText = buttonText;
        Command = command;
        Primary = primary;
        IconText = string.IsNullOrWhiteSpace(iconText) ? DeriveIcon(buttonText) : iconText;
        IconBackground = IconText switch
        {
            "PDF" => ThemeOrFallback("Brush.BlueSoft", "#EAF1FF"),
            "XLS" => ThemeOrFallback("Brush.GreenSoft", "#EAF8EF"),
            "DOC" => ThemeOrFallback("Brush.AmberSoft", "#FFF5DD"),
            _ => ThemeOrFallback("Brush.Canvas", "#F3F5F8")
        };
        IconForeground = IconText switch
        {
            "PDF" => ThemeOrFallback("Brush.Blue", "#2563EB"),
            "XLS" => ThemeOrFallback("Brush.Green", "#15803D"),
            "DOC" => ThemeOrFallback("Brush.Amber", "#B45309"),
            _ => ThemeOrFallback("Brush.Ink3", "#66758A")
        };
    }

    public string Title { get; }
    public string Description { get; }
    public string ButtonText { get; }
    public ICommand Command { get; }
    public bool Primary { get; }
    public string IconText { get; }
    public Brush IconBackground { get; }
    public Brush IconForeground { get; }

    private static Brush ThemeOrFallback(string resourceKey, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(resourceKey) is Brush themeBrush)
            return themeBrush;

        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        brush.Freeze();
        return brush;
    }

    private static string DeriveIcon(string buttonText) => buttonText.ToUpperInvariant() switch
    {
        var s when s.Contains("PDF") => "PDF",
        var s when s.Contains("EXCEL") || s.Contains("XLS") => "XLS",
        var s when s.Contains("DOCX") || s.Contains("DOC") => "DOC",
        var s when s.Contains("ZIP") => "ZIP",
        _ => "EXP"
    };
}
