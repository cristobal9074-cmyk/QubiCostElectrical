using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

public partial class ItemizadoView : UserControl
{
    public ItemizadoView()
    {
        InitializeComponent();
        DataGridExcelNavigation.Attach(ItemGrid);
        DataContextChanged += OnDataContextChanged;
    }

    private ItemizadoViewModel? Vm => DataContext as ItemizadoViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ItemizadoViewModel oldVm)
            oldVm.RowAdded -= OnRowAdded;
        if (e.NewValue is ItemizadoViewModel newVm)
            newVm.RowAdded += OnRowAdded;
    }

    // Al agregar una partida o un título, entra directo en modo edición sobre la
    // columna Descripción de la fila nueva y selecciona todo su texto, para poder
    // escribir el nombre de inmediato sin F2 ni clic manual. No toca ningún otro
    // flujo de edición (F2, doble clic, navegación tipo Excel siguen igual).
    private void OnRowAdded(string rowId)
    {
        // Diferido: la fila recién insertada todavía no tiene contenedor visual
        // generado en el DataGrid justo después de RefreshAfterStructureChange().
        Dispatcher.BeginInvoke(new Action(() => BeginEditNewRow(rowId)), DispatcherPriority.ContextIdle);
    }

    private void BeginEditNewRow(string rowId)
    {
        var row = ItemGrid.Items.OfType<ItemizadoRowVm>().FirstOrDefault(r => r.Id == rowId);
        if (row == null)
            return;

        var column = ItemGrid.Columns.FirstOrDefault(c => Equals(c.Header, "DESCRIPCIÓN"));
        if (column == null)
            return;

        ItemGrid.ScrollIntoView(row, column);
        ItemGrid.UpdateLayout();
        ItemGrid.SelectedItem = row;
        ItemGrid.CurrentCell = new DataGridCellInfo(row, column);
        ItemGrid.Focus();

        if (!ItemGrid.BeginEdit())
            return;

        // El TextBox de edición se crea al aplicar la plantilla de edición de la
        // celda; se difiere una vez más para que ya exista en el árbol visual.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (FindEditingTextBox(ItemGrid) is { } textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }), DispatcherPriority.Input);
    }

    private static TextBox? FindEditingTextBox(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox { IsVisible: true, IsEnabled: true } textBox)
                return textBox;
            if (FindEditingTextBox(child) is { } found)
                return found;
        }
        return null;
    }

    private void OnGridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (Vm?.IsReadOnly == true)
        {
            e.Cancel = true;
            return;
        }

        if (e.Row.Item is ItemizadoRowVm row)
        {
            string? header = e.Column.Header?.ToString();
            if (string.Equals(header, "Ítem", StringComparison.OrdinalIgnoreCase))
                row.BeginItemEdit();
        }

        if (e.Row.Item is ItemizadoRowVm row2 && row2.EsTitulo)
        {
            string? header = e.Column.Header?.ToString();
            if (header is "Ud." or "Cantidad" or "Excl. cubic.")
                e.Cancel = true;
        }
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit)
            return;

        if (e.Row.Item is not ItemizadoRowVm row)
            return;

        string? header = e.Column.Header?.ToString();
        // Diferido: el DataGrid todavía tiene abierta la transacción AddNew/EditItem
        // de su CollectionView mientras CellEditEnding se está procesando. Llamar a
        // OnCellEdited aquí mismo dispara (vía MarkDirty) un CollectionView.Refresh()
        // síncrono que lanza InvalidOperationException ("no se permite Refresh durante
        // una transacción"). Se pospone al siguiente ciclo del dispatcher, una vez que
        // el DataGrid termina de confirmar la edición.
        // Prioridad Normal (no Background): con Background, ediciones seguidas o clics
        // en otros botones (Eliminar, Agregar partida) hacían que la cola de refrescos
        // se acumulara y quedara pendiente hasta que algo forzaba al dispatcher a
        // vaciarla (p.ej. cambiar de pestaña), dando la sensación de UI trabada o de
        // botones que "no funcionan".
        Dispatcher.BeginInvoke(new Action(() => Vm?.OnCellEdited(header, row)),
            System.Windows.Threading.DispatcherPriority.Normal);
    }

    private async void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Vm == null)
            return;

        if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && !IsGridEditing())
        {
            Vm.HandleDeleteKey();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt && !IsGridEditing())
        {
            if (e.Key == Key.Up)
            {
                Vm.MoveUpCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                Vm.MoveDownCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        if (!IsGridEditing() && Vm.SelectedRow != null)
        {
            bool ctrlPressed = Keyboard.Modifiers == ModifierKeys.Control;
            bool noModifiers = Keyboard.Modifiers == ModifierKeys.None;

            if (ctrlPressed || noModifiers)
            {
                if (e.Key is Key.T or Key.C)
                {
                    Vm.SelectedRow.EsTitulo = !Vm.SelectedRow.EsTitulo;
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.E)
                {
                    Vm.SelectedRow.ExcluidoCubicacion = !Vm.SelectedRow.ExcluidoCubicacion;
                    e.Handled = true;
                    return;
                }
            }
        }

        DataGridExcelNavigation.HandlePreviewKeyDown(ItemGrid, e);
        if (e.Handled)
            return;

        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            await Vm.HandlePasteKeyAsync(ItemGrid, showPreview: true);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None && !IsGridEditing())
        {
            if (Vm.AddPartidaCommand.CanExecute(null))
            {
                Vm.AddPartidaCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnRowContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is DataGridRow row && row.Item is ItemizadoRowVm vm && Vm != null)
            Vm.SelectedRow = vm;
    }

    // El ContextMenu de fila usa Click en vez de Command binding (ver comentario en el
    // XAML): al abrirse, sincronizamos manualmente el IsEnabled de cada ítem con el
    // CanExecute de su comando, ya que sin Command binding no hay actualización automática.
    // No usamos x:Name en los MenuItem porque un x:Name dentro de un Setter.Value dispara
    // en el compilador de XAML el mismo camino de generación de código que usan las
    // plantillas (ControlTemplate/DataTemplate), lo cual falla aquí; en su lugar
    // localizamos cada ítem por su texto de Header.
    private void OnRowContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not ContextMenu menu)
            return;

        SetItemEnabled(menu, "Agregar partida", Vm.AddPartidaCommand.CanExecute(null));
        SetItemEnabled(menu, "Agregar título", Vm.AddTitleCommand.CanExecute(null));
        SetItemEnabled(menu, "Cubicar", Vm.CubicarCommand.CanExecute(null));
        SetItemEnabled(menu, "Modelo comercial de la partida", Vm.ConfigureCommercialModelCommand.CanExecute(null));
        SetItemEnabled(menu, "Duplicar", Vm.DuplicateCommand.CanExecute(null));
        SetItemEnabled(menu, "Subir", Vm.MoveUpCommand.CanExecute(null));
        SetItemEnabled(menu, "Bajar", Vm.MoveDownCommand.CanExecute(null));
        SetItemEnabled(menu, "Eliminar", Vm.DeleteCommand.CanExecute(null));

        SetItemEnabled(menu, "Es título", !Vm.IsReadOnly);
        SetItemEnabled(menu, "No cubicar", !Vm.IsReadOnly);
        SetItemChecked(menu, "Es título", Vm.SelectedRow?.EsTitulo == true);
        SetItemChecked(menu, "No cubicar", Vm.SelectedRow?.ExcluidoCubicacion == true);
    }

    private static void SetItemEnabled(ContextMenu menu, string header, bool enabled)
    {
        foreach (var obj in menu.Items)
        {
            if (obj is MenuItem item && Equals(item.Header, header))
                item.IsEnabled = enabled;
        }
    }

    private static void SetItemChecked(ContextMenu menu, string header, bool isChecked)
    {
        foreach (var obj in menu.Items)
        {
            if (obj is MenuItem item && Equals(item.Header, header))
                item.IsChecked = isChecked;
        }
    }

    private void OnRowMenuAddPartidaClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.AddPartidaCommand.CanExecute(null) == true)
            Vm.AddPartidaCommand.Execute(null);
    }

    private void OnRowMenuAddTitleClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.AddTitleCommand.CanExecute(null) == true)
            Vm.AddTitleCommand.Execute(null);
    }

    private void OnRowMenuCubicarClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.CubicarCommand.CanExecute(null) == true)
            Vm.CubicarCommand.Execute(null);
    }

    private void OnRowMenuCommercialModelClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.ConfigureCommercialModelCommand.CanExecute(null) == true)
            Vm.ConfigureCommercialModelCommand.Execute(null);
    }

    private void OnRowMenuDuplicateClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.DuplicateCommand.CanExecute(null) == true)
            Vm.DuplicateCommand.Execute(null);
    }

    private void OnRowMenuMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.MoveUpCommand.CanExecute(null) == true)
            Vm.MoveUpCommand.Execute(null);
    }

    private void OnRowMenuMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.MoveDownCommand.CanExecute(null) == true)
            Vm.MoveDownCommand.Execute(null);
    }

    private void OnRowMenuDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.DeleteCommand.CanExecute(null) == true)
            Vm.DeleteCommand.Execute(null);
    }

    private void OnRowMenuToggleEsTituloClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedRow != null && !Vm.IsReadOnly)
        {
            Vm.SelectedRow.EsTitulo = !Vm.SelectedRow.EsTitulo;
        }
    }

    private void OnRowMenuToggleNoCubicarClick(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedRow != null && !Vm.IsReadOnly)
        {
            Vm.SelectedRow.ExcluidoCubicacion = !Vm.SelectedRow.ExcluidoCubicacion;
        }
    }

    private bool IsGridEditing()
    {
        foreach (var item in ItemGrid.Items)
        {
            if (ItemGrid.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row && row.IsEditing)
                return true;
        }
        return false;
    }
}
