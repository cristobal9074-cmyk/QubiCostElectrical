using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Currency;

public partial class CurrencyView : UserControl
{
    public CurrencyView() => InitializeComponent();

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CurrencyViewModel vm)
            vm.TryApplyChanges();
    }

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CurrencyViewModel vm)
            vm.TryDiscardChanges();
    }
}
