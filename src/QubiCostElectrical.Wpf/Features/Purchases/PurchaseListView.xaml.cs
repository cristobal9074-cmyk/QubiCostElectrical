using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Purchases;

public partial class PurchaseListView : UserControl
{
    private PurchaseListViewModel? vm;

    public PurchaseListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachVm();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        vm = DataContext as PurchaseListViewModel;
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
        if (e.PropertyName == nameof(PurchaseListViewModel.MuestraUf))
            ApplyMonedaPolicy();
    }

    /// <summary>
    /// Política de moneda del proyecto: en CLP se ocultan las columnas UF (PRECIO UF / NETO UF /
    /// COMPRA UF) para que no quede ninguna cifra en UF en pantalla.
    /// </summary>
    private void ApplyMonedaPolicy()
    {
        var ufVisibility = vm?.MuestraUf == true ? Visibility.Visible : Visibility.Collapsed;
        PrecioUfColumn.Visibility = ufVisibility;
        NetoUfColumn.Visibility = ufVisibility;
        CompraUfColumn.Visibility = ufVisibility;
    }
}
