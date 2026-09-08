using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public partial class NavDropdownGroup : UserControl
{
    public static readonly DependencyProperty GroupTitleProperty =
        DependencyProperty.Register(nameof(GroupTitle), typeof(string), typeof(NavDropdownGroup));

    public static readonly DependencyProperty SectionLabelProperty =
        DependencyProperty.Register(nameof(SectionLabel), typeof(string), typeof(NavDropdownGroup));

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable<NavDropdownItem>), typeof(NavDropdownGroup),
            new PropertyMetadata(null, OnItemsChanged));

    public string GroupTitle
    {
        get => (string)GetValue(GroupTitleProperty);
        set => SetValue(GroupTitleProperty, value);
    }

    public string SectionLabel
    {
        get => (string)GetValue(SectionLabelProperty);
        set => SetValue(SectionLabelProperty, value);
    }

    public IEnumerable<NavDropdownItem>? Items
    {
        get => (IEnumerable<NavDropdownItem>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public NavDropdownGroup()
    {
        InitializeComponent();
    }

    private static void OnItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavDropdownGroup group)
            group.ItemsHost.ItemsSource = e.NewValue as IEnumerable<NavDropdownItem>;
    }

    private void OnItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ShellRoute route)
            return;

        if (Window.GetWindow(this)?.DataContext is ShellViewModel vm)
            vm.Navigate(route);

        Trigger.IsChecked = false;
        Popup.IsOpen = false;
    }

    private void OnSeparatorClick(object sender, RoutedEventArgs e)
    {
        // Separadores no navegan; absorber el evento para evitar cierre accidental del popup.
        e.Handled = true;
    }

    private void OnTriggerClick(object sender, RoutedEventArgs e) =>
        Popup.IsOpen = Trigger.IsChecked == true;

    public void ClosePopup()
    {
        Trigger.IsChecked = false;
        Popup.IsOpen = false;
    }

    public void SetActiveState(bool isActive) =>
        NavigationVisuals.SetIsActive(Trigger, isActive);
}
