using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Quotes;

public partial class QuotesView : UserControl
{
    private QuotesViewModel? vm;

    public QuotesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachVm();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        vm = DataContext as QuotesViewModel;
        if (vm == null)
            return;

        vm.PropertyChanged += OnVmPropertyChanged;
        ApplyMonedaPolicy();
    }

    private void DetachVm()
    {
        if (vm == null)
            return;

        vm.PropertyChanged -= OnVmPropertyChanged;
        vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QuotesViewModel.MuestraUf))
            ApplyMonedaPolicy();
    }

    /// <summary>
    /// Política de moneda del proyecto: en CLP se ocultan las columnas UF (PRECIO UF / MEJOR UF /
    /// DELTA UF) y queda solo la comparación en pesos.
    /// </summary>
    private void ApplyMonedaPolicy()
    {
        var ufVisibility = vm?.MuestraUf == true ? Visibility.Visible : Visibility.Collapsed;

        PrecioUfColumn.Visibility = ufVisibility;
        SelectedUfColumn.Visibility = ufVisibility;
        BestUfColumn.Visibility = ufVisibility;
        DeltaUfColumn.Visibility = ufVisibility;
    }

    private void OnUserControlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (vm?.HasActiveFilters == true)
            {
                vm.ClearFiltersCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control && e.Key == System.Windows.Input.Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }
    }
}
