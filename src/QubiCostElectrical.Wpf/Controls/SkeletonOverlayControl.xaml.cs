using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Controls;

public partial class SkeletonOverlayControl : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(SkeletonOverlayControl),
            new PropertyMetadata("Cargando…"));

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public SkeletonOverlayControl()
    {
        InitializeComponent();
    }
}
