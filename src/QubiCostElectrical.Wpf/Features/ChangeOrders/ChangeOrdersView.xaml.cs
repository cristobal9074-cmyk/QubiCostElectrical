using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

public partial class ChangeOrdersView : UserControl
{
    public ChangeOrdersView()
    {
        InitializeComponent();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private ChangeOrdersViewModel? Vm => DataContext as ChangeOrdersViewModel;

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm == null) return;

        // Ctrl+N: Nueva orden de cambio
        if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            Vm.AddNew();
            e.Handled = true;
            return;
        }

        // Ctrl+D: Duplicar orden seleccionada
        if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Vm.CanDuplicate)
            {
                Vm.DuplicateSelected();
                e.Handled = true;
            }
            return;
        }

        // F5: Refrescar
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            Vm.Refresh();
            e.Handled = true;
            return;
        }

        // Ctrl+F: Enfocar buscador
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox?.Focus();
            SearchBox?.SelectAll();
            e.Handled = true;
            return;
        }

        // Esc: Limpiar búsqueda si hay texto
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (!string.IsNullOrEmpty(Vm.SearchText))
            {
                Vm.SearchText = "";
                e.Handled = true;
                return;
            }
        }

        // Enter: Editar orden seleccionada (cuando no está escribiendo en el buscador)
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && e.OriginalSource is not TextBox)
        {
            if (Vm.CanEdit)
            {
                Vm.EditSelected();
                e.Handled = true;
                return;
            }
        }

        // Delete: Eliminar orden seleccionada (cuando no está escribiendo en el buscador)
        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && e.OriginalSource is not TextBox)
        {
            if (Vm.CanDelete)
            {
                Vm.DeleteSelected();
                e.Handled = true;
                return;
            }
        }
    }

    private void OnAddClick(object sender, RoutedEventArgs e) => Vm?.AddNew();
    private void OnEditClick(object sender, RoutedEventArgs e) => Vm?.EditSelected();
    private void OnDuplicateClick(object sender, RoutedEventArgs e) => Vm?.DuplicateSelected();
    private void OnDeleteClick(object sender, RoutedEventArgs e) => Vm?.DeleteSelected();
    private void OnEnviarRevisionClick(object sender, RoutedEventArgs e) => Vm?.EnviarARevision();
    private void OnAprobarClick(object sender, RoutedEventArgs e) => Vm?.AprobarSelected();
    private void OnRechazarClick(object sender, RoutedEventArgs e) => Vm?.RechazarSelected();
    private void OnReabrirClick(object sender, RoutedEventArgs e) => Vm?.ReabrirSelected();
    private void OnRefreshClick(object sender, RoutedEventArgs e) => Vm?.Refresh();

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            Vm?.EditSelected();
    }

    private void OnFilterAllClick(object sender, RoutedEventArgs e) => Vm?.SetFilterAll();
    private void OnFilterBorradorClick(object sender, RoutedEventArgs e) => Vm?.SetFilterBorrador();
    private void OnFilterEnRevisionClick(object sender, RoutedEventArgs e) => Vm?.SetFilterEnRevision();
    private void OnFilterAprobadoClick(object sender, RoutedEventArgs e) => Vm?.SetFilterAprobado();
    private void OnFilterRechazadoClick(object sender, RoutedEventArgs e) => Vm?.SetFilterRechazado();

    private void OnClearFiltersClick(object sender, RoutedEventArgs e) => Vm?.ClearFilters();
}
