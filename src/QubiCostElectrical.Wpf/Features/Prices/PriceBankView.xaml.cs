using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Prices;

public partial class PriceBankView : UserControl
{
    public PriceBankView(PriceBankViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
        Unloaded += (_, _) => vm.PropertyChanged -= OnVmPropertyChanged;
        ApplyMonedaPolicy();
    }

    public void ApplyNavContext(PriceBankNavigationContext ctx)
    {
        if (DataContext is PriceBankViewModel vm)
            vm.SearchText = ctx.SearchTerm;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PriceBankViewModel.MuestraUf))
            ApplyMonedaPolicy();
    }

    /// <summary>
    /// Política de moneda del proyecto: en CLP se ocultan las columnas UF (PRECIO UF / MO UF).
    /// El precio fuente es siempre en pesos; la UF es solo una equivalencia derivada.
    /// </summary>
    private void ApplyMonedaPolicy()
    {
        var ufVisibility = DataContext is PriceBankViewModel { MuestraUf: true }
            ? Visibility.Visible
            : Visibility.Collapsed;

        PrecioUfColumn.Visibility = ufVisibility;
        MoUfColumn.Visibility = ufVisibility;
    }

    private void OnMoreActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }
    }
}
