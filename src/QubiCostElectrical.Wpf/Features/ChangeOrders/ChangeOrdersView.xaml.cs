using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public partial class ChangeOrdersView : UserControl
{
    public ChangeOrdersView() => InitializeComponent();

    private ChangeOrdersViewModel? Vm => DataContext as ChangeOrdersViewModel;

    private void OnAddClick(object sender, RoutedEventArgs e)           => Vm?.AddNew();
    private void OnEditClick(object sender, RoutedEventArgs e)          => Vm?.EditSelected();
    private void OnDeleteClick(object sender, RoutedEventArgs e)        => Vm?.DeleteSelected();
    private void OnEnviarRevisionClick(object sender, RoutedEventArgs e) => Vm?.EnviarARevision();
    private void OnAprobarClick(object sender, RoutedEventArgs e)       => Vm?.AprobarSelected();
    private void OnRechazarClick(object sender, RoutedEventArgs e)      => Vm?.RechazarSelected();
    private void OnReabrirClick(object sender, RoutedEventArgs e)       => Vm?.ReabrirSelected();

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            Vm?.EditSelected();
    }

    private void OnFilterEstadoChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (Vm == null) return;
        if (EstadoFilterBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            Vm.FilterEstado = item.Tag?.ToString() ?? "";
    }

    private void OnClearFiltersClick(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        Vm.SearchText   = "";
        Vm.FilterEstado = "";
        EstadoFilterBox.SelectedIndex = 0;
    }
}
