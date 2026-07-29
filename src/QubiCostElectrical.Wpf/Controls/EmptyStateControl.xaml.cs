using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Controls;

public partial class EmptyStateControl : UserControl
{
    public static readonly DependencyProperty IconTextProperty =
        DependencyProperty.Register(nameof(IconText), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(""));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(""));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(EmptyStateControl), new PropertyMetadata(""));

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(EmptyStateControl), new PropertyMetadata(null));

    public string IconText
    {
        get => (string)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public EmptyStateControl()
    {
        InitializeComponent();
    }
}
