using System.Windows;

namespace QubiCostElectrical.Wpf.DesignSystem;

/// <summary>
/// Provee propiedades adjuntas Prefix y Suffix para TextBox y controles de entrada,
/// permitiendo renderizar adornos y unidades integradas ($, UF, %, m, kg, etc.)
/// con estilo cohesivo con el Design System.
/// </summary>
public static class FieldAdornment
{
    public static readonly DependencyProperty PrefixProperty =
        DependencyProperty.RegisterAttached(
            "Prefix",
            typeof(string),
            typeof(FieldAdornment),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static void SetPrefix(UIElement element, string? value) => element.SetValue(PrefixProperty, value);
    public static string? GetPrefix(UIElement element) => (string?)element.GetValue(PrefixProperty);

    public static readonly DependencyProperty SuffixProperty =
        DependencyProperty.RegisterAttached(
            "Suffix",
            typeof(string),
            typeof(FieldAdornment),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.Inherits));

    public static void SetSuffix(UIElement element, string? value) => element.SetValue(SuffixProperty, value);
    public static string? GetSuffix(UIElement element) => (string?)element.GetValue(SuffixProperty);
}
