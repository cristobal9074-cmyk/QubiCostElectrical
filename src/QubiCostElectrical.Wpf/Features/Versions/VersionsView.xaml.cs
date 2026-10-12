using System.ComponentModel;
using System.Windows.Controls;

namespace QubiCostElectrical.Wpf.Features.Versions;

public partial class VersionsView : UserControl
{
    private VersionsViewModel? vm;

    public VersionsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => DetachVm();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        DetachVm();
        vm = DataContext as VersionsViewModel;
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
        if (e.PropertyName == nameof(VersionsViewModel.MuestraUf))
            ApplyMonedaPolicy();
    }

    /// <summary>
    /// Política de moneda del proyecto: en CLP no queda ninguna cifra en UF en pantalla. Las
    /// columnas se reetiquetan a pesos y la mini tabla de recursos cambia sus encabezados.
    /// </summary>
    private void ApplyMonedaPolicy()
    {
        bool muestraUf = vm?.MuestraUf != false;

        VentaColumn.Header = muestraUf ? "Venta UF" : "Venta $";
        VentaDeltaColumn.Header = muestraUf ? "Δ UF" : "Δ $";
        PuColumn.Header = muestraUf ? "P.U. UF" : "P.U. $";
        TotalColumn.Header = muestraUf ? "Total UF" : "Total $";
        DeltaUfColumn.Header = muestraUf ? "Δ UF" : "Δ $";

        ResourcePrecioUnitHeader.Text = muestraUf ? "UF unit." : "$ unit.";
        ResourceDeltaHeader.Text = muestraUf ? "Δ UF" : "Δ $";
    }
}
