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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnUserControlKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.F && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            if (MainTabControl?.SelectedIndex == 1)
            {
                SearchBoxComparison?.Focus();
                SearchBoxComparison?.SelectAll();
            }
            else
            {
                SearchBoxVersions?.Focus();
                SearchBoxVersions?.SelectAll();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            if (DataContext is VersionsViewModel vm && vm.HasSearchText)
            {
                vm.ClearSearchCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ComparisonDataGrid != null)
        {
            ComparisonDataGrid.LayoutUpdated += OnComparisonDataGridLayoutUpdated;
        }
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ComparisonDataGrid != null)
        {
            ComparisonDataGrid.LayoutUpdated -= OnComparisonDataGridLayoutUpdated;
        }
        DetachVm();
    }

    private void OnComparisonDataGridLayoutUpdated(object? sender, System.EventArgs e)
    {
        SyncHeaderColumns();
    }

    private void SyncHeaderColumns()
    {
        if (ComparisonDataGrid == null || ComparisonHeaderGrid == null)
            return;

        int count = Math.Min(ComparisonDataGrid.Columns.Count, 13);
        for (int i = 0; i < count; i++)
        {
            var col = ComparisonDataGrid.Columns[i];
            if (col.ActualWidth > 0 && i < ComparisonHeaderGrid.ColumnDefinitions.Count)
            {
                var colDef = ComparisonHeaderGrid.ColumnDefinitions[i];
                if (!colDef.Width.IsAbsolute || Math.Abs(colDef.Width.Value - col.ActualWidth) > 0.5)
                {
                    colDef.Width = new System.Windows.GridLength(col.ActualWidth, System.Windows.GridUnitType.Pixel);
                }
            }
        }
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

        if (VentaColumn != null) VentaColumn.Header = muestraUf ? "Venta UF" : "Venta $";
        if (VentaDeltaColumn != null) VentaDeltaColumn.Header = muestraUf ? "Δ UF" : "Δ $";
        if (TotalColumn != null) TotalColumn.Header = muestraUf ? "A · Total UF" : "A · Total $";
        if (DeltaUfColumn != null) DeltaUfColumn.Header = muestraUf ? "Δ Monto UF" : "Δ Monto $";
        if (TotalBColumn != null) TotalBColumn.Header = muestraUf ? "B · Total UF" : "B · Total $";

        if (ResourcePrecioUnitHeader != null) ResourcePrecioUnitHeader.Text = muestraUf ? "UF unit." : "$ unit.";
        if (ResourceDeltaHeader != null) ResourceDeltaHeader.Text = muestraUf ? "Δ UF" : "Δ $";
    }
}
