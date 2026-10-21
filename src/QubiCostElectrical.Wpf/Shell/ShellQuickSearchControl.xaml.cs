using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Shell;

public partial class ShellQuickSearchControl : UserControl
{
    public ShellQuickSearchControl()
    {
        InitializeComponent();
    }

    public TextBox SearchBox => PopupSearchBox;

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

    private void OnSearchBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // No longer automatically open Quick Search on focus.
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        if (e.Key == Key.Down)
        {
            if (vm.IsSearchPaletteOpen)
            {
                vm.SelectQuickSearchIndex(1);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Up)
        {
            if (vm.IsSearchPaletteOpen)
            {
                vm.SelectQuickSearchIndex(-1);
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = vm.ExecuteQuickSearchAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            var values = (Features.CommandPalette.CommandPaletteCategory[])Enum.GetValues(typeof(Features.CommandPalette.CommandPaletteCategory));
            var currentIndex = Array.IndexOf(values, vm.SelectedPaletteCategory);
            var nextIndex = (currentIndex + 1) % values.Length;
            vm.SelectedPaletteCategory = values[nextIndex];
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            vm.CloseQuickSearch();
            e.Handled = true;
        }
    }

    private void OnQuickSearchItemClick(object sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        if (sender is not FrameworkElement element || element.DataContext is not ShellViewModel.QuickSearchItemVm item)
            return;

        vm.SelectedQuickSearchItem = item;
        _ = vm.ExecuteQuickSearchAsync();
    }

    private void OnQuickSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null) return;

        if (sender is not ListBox listBox)
            return;

        vm.SelectedQuickSearchItem = listBox.SelectedItem as ShellViewModel.QuickSearchItemVm;
        if (listBox.SelectedItem != null)
        {
            listBox.ScrollIntoView(listBox.SelectedItem);
        }
    }
}
