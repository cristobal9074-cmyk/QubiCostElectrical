using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Solicitudes;

public partial class SolicitudesView : UserControl
{
    private TextBox? _activeEditor;
    private SolicitudItemRowVm? _activeRow;
    private bool _isApplyingSuggestion;
    private SolicitudesViewModel? _subscribedVm;

    private SolicitudesViewModel? Vm => DataContext as SolicitudesViewModel;

    public SolicitudesView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PreviewKeyDown += OnUserControlPreviewKeyDown;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.Materiales.CollectionChanged -= OnMaterialesCollectionChanged;
            _subscribedVm = null;
        }

        if (DataContext is SolicitudesViewModel vm)
        {
            _subscribedVm = vm;
            vm.Materiales.CollectionChanged += OnMaterialesCollectionChanged;
        }
    }

    private int _lastDeletedIndex = -1;
    private DataGridColumn? _lastDeletedCol;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVm == null && DataContext is SolicitudesViewModel vm)
        {
            _subscribedVm = vm;
            vm.Materiales.CollectionChanged -= OnMaterialesCollectionChanged;
            vm.Materiales.CollectionChanged += OnMaterialesCollectionChanged;
        }

        if (MaterialesGrid.Items.Count > 0)
        {
            FocusCellDirectly(0, CodigoColumn);
        }
    }

    private void OnMaterialesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            int oldIdx = _lastDeletedIndex >= 0 ? _lastDeletedIndex : e.OldStartingIndex;
            var targetCol = _lastDeletedCol ?? CodigoColumn;
            _lastDeletedIndex = -1;
            _lastDeletedCol = null;

            if (MaterialesGrid.Items.Count == 0)
            {
                AddAndFocusNewMaterial(targetCol);
            }
            else
            {
                int nextIdx = Math.Clamp(oldIdx, 0, MaterialesGrid.Items.Count - 1);
                FocusCellDirectly(nextIdx, targetCol);
            }
        }
    }

    private void DeleteActiveMaterial()
    {
        if (Vm == null) return;

        var targetRow = MaterialesGrid.CurrentCell.Item as SolicitudItemRowVm
                     ?? MaterialesGrid.SelectedItem as SolicitudItemRowVm;

        if (targetRow == null && MaterialesGrid.SelectedCells.Count > 0)
        {
            targetRow = MaterialesGrid.SelectedCells[0].Item as SolicitudItemRowVm;
        }

        if (targetRow != null && Vm.DeleteMaterialCommand.CanExecute(targetRow))
        {
            int oldIdx = MaterialesGrid.Items.IndexOf(targetRow);
            var col = MaterialesGrid.CurrentCell.Column;
            if (col == null || col == QuitarColumn || col.Visibility != Visibility.Visible)
                col = CodigoColumn;

            _lastDeletedCol = col;
            _lastDeletedIndex = oldIdx;

            Vm.DeleteMaterialCommand.Execute(targetRow);
        }
    }

    private void OnGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox)
            return;

        if (((e.Key == Key.Delete || e.Key == Key.Back) && Keyboard.Modifiers == ModifierKeys.None) ||
            ((e.Key == Key.OemMinus || e.Key == Key.Subtract) && Keyboard.Modifiers == ModifierKeys.Control))
        {
            DeleteActiveMaterial();
            e.Handled = true;
            return;
        }
    }

    private void OnGridPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (DataGridExcelNavigation.TryBeginEditFromChar(MaterialesGrid, e.Text))
            e.Handled = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.Materiales.CollectionChanged -= OnMaterialesCollectionChanged;
            _subscribedVm = null;
        }

        if (DataContext is SolicitudesViewModel vm)
        {
            vm.AutocompleteVisible = false;
        }
    }

    private void OnUserControlPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Delete / Supr o Ctrl+- : eliminar la fila activa del material sin perder el teclado ni el indicador
        if ((e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None) ||
            ((e.Key == Key.OemMinus || e.Key == Key.Subtract) && Keyboard.Modifiers == ModifierKeys.Control))
        {
            if (e.OriginalSource is not TextBox && MaterialesGrid.IsKeyboardFocusWithin)
            {
                DeleteActiveMaterial();
                e.Handled = true;
                return;
            }
        }

        // Atajos globales de teclado para agilizar el trabajo sin ratón:
        // Ctrl+S / Ctrl+G: Guardar
        if ((e.Key == Key.S || e.Key == Key.G) && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (Vm?.SaveCommand.CanExecute(null) == true)
            {
                MaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                MaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
                Vm.SaveCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Ctrl+P: Generar PDF / Imprimir
        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (Vm?.ExportPdfCommand.CanExecute(null) == true)
            {
                Vm.ExportPdfCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Ctrl+B o F3: Abrir buscador modal de lista de compra
        if (((e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control) || e.Key == Key.F3) && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            if (Vm?.OpenMaterialPickerCommand.CanExecute(null) == true)
            {
                Vm.OpenMaterialPickerCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Ctrl+N o tecla Insert: Agregar fila y enfocar celda Código
        if ((e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) || e.Key == Key.Insert)
        {
            if (Vm?.AddMaterialCommand.CanExecute(null) == true)
            {
                AddAndFocusNewMaterial();
                e.Handled = true;
                return;
            }
        }
    }

    public void AddAndFocusNewMaterial(DataGridColumn? column = null)
    {
        if (Vm == null || !Vm.AddMaterialCommand.CanExecute(null)) return;
        Vm.AddMaterialCommand.Execute(null);

        int newIdx = MaterialesGrid.Items.Count - 1;
        if (newIdx >= 0)
        {
            FocusCellDirectly(newIdx, column ?? CodigoColumn);
        }
    }

    public void FocusCellDirectly(int rowIndex, DataGridColumn? column)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (Vm == null) return;
            if (MaterialesGrid.Items.Count == 0)
            {
                AddAndFocusNewMaterial();
                return;
            }

            int targetIdx = Math.Clamp(rowIndex, 0, MaterialesGrid.Items.Count - 1);
            var targetCol = (column != null && column.Visibility == Visibility.Visible && column != QuitarColumn)
                ? column
                : (CodigoColumn ?? MaterialesGrid.Columns[0]);

            DataGridExcelNavigation.FocusCell(MaterialesGrid, targetIdx, targetCol);
        }), DispatcherPriority.Input);
    }

    private void OnPrintClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is SolicitudesViewModel vm)
        {
            vm.ExportPdfCommand.Execute(null);
        }
    }

    private void OnGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
        {
            _activeEditor = tb;
            _activeRow = e.Row.Item as SolicitudItemRowVm;

            if (ReferenceEquals(e.Column, CodigoColumn) || ReferenceEquals(e.Column, DescripcionColumn))
            {
                tb.TextChanged -= OnEditorTextChanged;
                tb.TextChanged += OnEditorTextChanged;
                tb.PreviewKeyDown -= OnCodigoOrDescripcionPreviewKeyDown;
                tb.PreviewKeyDown += OnCodigoOrDescripcionPreviewKeyDown;

                AutocompletePopup.PlacementTarget = tb;

                if (e.EditingEventArgs is KeyEventArgs or TextCompositionEventArgs)
                    tb.CaretIndex = tb.Text.Length;
                else
                    tb.SelectAll();
            }
            else if (ReferenceEquals(e.Column, CantidadColumn))
            {
                tb.PreviewKeyDown -= OnCantidadPreviewKeyDown;
                tb.PreviewKeyDown += OnCantidadPreviewKeyDown;
                tb.SelectAll();
            }
            else if (ReferenceEquals(e.Column, ObservacionesColumn))
            {
                tb.PreviewKeyDown -= OnObservacionesPreviewKeyDown;
                tb.PreviewKeyDown += OnObservacionesPreviewKeyDown;
            }
        }
    }

    private void OnGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
        {
            tb.TextChanged -= OnEditorTextChanged;
            tb.PreviewKeyDown -= OnCodigoOrDescripcionPreviewKeyDown;
            tb.PreviewKeyDown -= OnCantidadPreviewKeyDown;
            tb.PreviewKeyDown -= OnObservacionesPreviewKeyDown;

            if (ReferenceEquals(_activeEditor, tb))
                _activeEditor = null;
            _activeRow = null;
            AutocompletePopup.PlacementTarget = null;
            if (Vm != null)
                Vm.AutocompleteVisible = false;
        }

        if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is SolicitudItemRowVm row)
        {
            if (ReferenceEquals(e.Column, CodigoColumn) && e.EditingElement is TextBox codigoBox)
            {
                row.Codigo = codigoBox.Text;
            }
            else if (ReferenceEquals(e.Column, DescripcionColumn) && e.EditingElement is TextBox descBox)
            {
                row.Descripcion = descBox.Text;
            }
            else if (ReferenceEquals(e.Column, CantidadColumn) && e.EditingElement is TextBox cantBox)
            {
                if (decimal.TryParse(cantBox.Text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.CurrentCulture, out var cVal))
                {
                    row.Cantidad = cVal;
                }
            }
            Vm?.SaveToSession();
        }
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingSuggestion) return;
        if (sender is TextBox tb && MaterialesGrid.CurrentItem is SolicitudItemRowVm)
        {
            Vm?.SearchMaterialsAutocomplete(tb.Text);
        }
    }

    private void OnGridCurrentCellChanged(object? sender, EventArgs e)
    {
        if (Vm != null && Vm.AutocompleteVisible && MaterialesGrid.CurrentCell.Column != CodigoColumn && MaterialesGrid.CurrentCell.Column != DescripcionColumn)
        {
            Vm.AutocompleteVisible = false;
        }
    }

    private void OnGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
    }

    private void CommitAndMoveDown()
    {
        if (Vm != null)
            Vm.AutocompleteVisible = false;

        var currentCol = MaterialesGrid.CurrentCell.Column ?? CodigoColumn;
        var currentRow = _activeRow ?? MaterialesGrid.CurrentItem as SolicitudItemRowVm;

        MaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Vm?.SaveToSession();

        int currIdx = currentRow != null ? MaterialesGrid.Items.IndexOf(currentRow) : MaterialesGrid.SelectedIndex;
        if (currIdx >= 0 && currIdx < MaterialesGrid.Items.Count - 1)
        {
            FocusCellDirectly(currIdx + 1, currentCol);
        }
        else
        {
            // En la última fila: agregar fila nueva y comenzar en Código
            AddAndFocusNewMaterial(CodigoColumn);
        }
    }

    private void CommitAndMoveUp()
    {
        if (Vm != null)
            Vm.AutocompleteVisible = false;

        var currentCol = MaterialesGrid.CurrentCell.Column ?? CodigoColumn;
        var currentRow = _activeRow ?? MaterialesGrid.CurrentItem as SolicitudItemRowVm;

        MaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Vm?.SaveToSession();

        int currIdx = currentRow != null ? MaterialesGrid.Items.IndexOf(currentRow) : MaterialesGrid.SelectedIndex;
        if (currIdx > 0)
        {
            FocusCellDirectly(currIdx - 1, currentCol);
        }
        else if (currIdx == 0)
        {
            FocusCellDirectly(0, currentCol);
        }
    }

    private void CommitAndMoveRight()
    {
        if (Vm != null)
            Vm.AutocompleteVisible = false;

        var currentCol = MaterialesGrid.CurrentCell.Column ?? CodigoColumn;
        var currentRow = _activeRow ?? MaterialesGrid.CurrentItem as SolicitudItemRowVm;

        MaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Vm?.SaveToSession();

        int currIdx = currentRow != null ? MaterialesGrid.Items.IndexOf(currentRow) : MaterialesGrid.SelectedIndex;
        if (currIdx < 0) currIdx = 0;

        if (ReferenceEquals(currentCol, CodigoColumn))
        {
            FocusCellDirectly(currIdx, DescripcionColumn);
        }
        else if (ReferenceEquals(currentCol, DescripcionColumn))
        {
            FocusCellDirectly(currIdx, CantidadColumn);
        }
        else if (ReferenceEquals(currentCol, CantidadColumn))
        {
            FocusCellDirectly(currIdx, ObservacionesColumn);
        }
        else if (ReferenceEquals(currentCol, ObservacionesColumn))
        {
            if (currIdx < MaterialesGrid.Items.Count - 1)
                FocusCellDirectly(currIdx + 1, CodigoColumn);
            else
                AddAndFocusNewMaterial(CodigoColumn);
        }
    }

    private void CommitAndMoveLeft()
    {
        if (Vm != null)
            Vm.AutocompleteVisible = false;

        var currentCol = MaterialesGrid.CurrentCell.Column ?? CodigoColumn;
        var currentRow = _activeRow ?? MaterialesGrid.CurrentItem as SolicitudItemRowVm;

        MaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        Vm?.SaveToSession();

        int currIdx = currentRow != null ? MaterialesGrid.Items.IndexOf(currentRow) : MaterialesGrid.SelectedIndex;
        if (currIdx < 0) currIdx = 0;

        if (ReferenceEquals(currentCol, ObservacionesColumn))
        {
            FocusCellDirectly(currIdx, CantidadColumn);
        }
        else if (ReferenceEquals(currentCol, CantidadColumn))
        {
            FocusCellDirectly(currIdx, DescripcionColumn);
        }
        else if (ReferenceEquals(currentCol, DescripcionColumn))
        {
            FocusCellDirectly(currIdx, CodigoColumn);
        }
        else if (ReferenceEquals(currentCol, CodigoColumn))
        {
            if (currIdx > 0)
                FocusCellDirectly(currIdx - 1, ObservacionesColumn);
        }
    }

    private void OnCodigoOrDescripcionPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox;

        if (Vm != null && Vm.AutocompleteVisible)
        {
            switch (e.Key)
            {
                case Key.Down:
                    MoveAutocompleteSelection(+1);
                    e.Handled = true;
                    return;

                case Key.Up:
                    MoveAutocompleteSelection(-1);
                    e.Handled = true;
                    return;

                case Key.Enter:
                case Key.Tab:
                    if (AutocompleteList.SelectedItem is SolicitudAutocompleteSuggestionVm target)
                    {
                        ApplySuggestionAndAdvance(target, advanceToQuantity: true);
                        e.Handled = true;
                        return;
                    }
                    Vm.AutocompleteVisible = false;
                    break;

                case Key.Escape:
                    Vm.AutocompleteVisible = false;
                    e.Handled = true;
                    return;
            }
        }

        // Shift+Enter confirma y sube
        if (e.Key is Key.Enter or Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            CommitAndMoveUp();
            e.Handled = true;
            return;
        }

        // Enter confirma y baja a la celda inferior (Excel tradicional)
        if (e.Key is Key.Enter or Key.Return)
        {
            CommitAndMoveDown();
            e.Handled = true;
            return;
        }

        // Shift+Tab retrocede a la columna izquierda
        if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            CommitAndMoveLeft();
            e.Handled = true;
            return;
        }

        // Tab avanza a la siguiente columna a la derecha
        if (e.Key == Key.Tab)
        {
            CommitAndMoveRight();
            e.Handled = true;
            return;
        }

        // Flecha Abajo (cuando autocompletado no está abierto): confirma y baja
        if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None)
        {
            CommitAndMoveDown();
            e.Handled = true;
            return;
        }

        // Flecha Arriba (cuando autocompletado no está abierto): confirma y sube
        if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            CommitAndMoveUp();
            e.Handled = true;
            return;
        }

        // Flecha Izquierda: si está al inicio o todo seleccionado, confirma y va a la izquierda
        if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None && tb != null)
        {
            if (tb.SelectionLength == tb.Text.Length || (tb.SelectionStart == 0 && tb.SelectionLength == 0))
            {
                CommitAndMoveLeft();
                e.Handled = true;
                return;
            }
        }

        // Flecha Derecha: si está al final o todo seleccionado, confirma y va a la derecha
        if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None && tb != null)
        {
            if (tb.SelectionLength == tb.Text.Length || (tb.SelectionStart == tb.Text.Length && tb.SelectionLength == 0))
            {
                CommitAndMoveRight();
                e.Handled = true;
                return;
            }
        }
    }

    private void OnCantidadPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox;

        if (e.Key is Key.Enter or Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            CommitAndMoveUp();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return || (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None))
        {
            CommitAndMoveDown();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            CommitAndMoveUp();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            CommitAndMoveLeft();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            CommitAndMoveRight();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None && tb != null)
        {
            if (tb.SelectionLength == tb.Text.Length || (tb.SelectionStart == 0 && tb.SelectionLength == 0))
            {
                CommitAndMoveLeft();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None && tb != null)
        {
            if (tb.SelectionLength == tb.Text.Length || (tb.SelectionStart == tb.Text.Length && tb.SelectionLength == 0))
            {
                CommitAndMoveRight();
                e.Handled = true;
                return;
            }
        }
    }

    private void OnObservacionesPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tb = sender as TextBox;

        if (e.Key is Key.Enter or Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            CommitAndMoveUp();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Return || (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None))
        {
            CommitAndMoveDown();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            CommitAndMoveUp();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            CommitAndMoveLeft();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            CommitAndMoveDown();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.None && tb != null)
        {
            if (tb.SelectionLength == tb.Text.Length || (tb.SelectionStart == 0 && tb.SelectionLength == 0))
            {
                CommitAndMoveLeft();
                e.Handled = true;
                return;
            }
        }
    }

    private void MoveAutocompleteSelection(int delta)
    {
        int count = AutocompleteList.Items.Count;
        if (count == 0) return;

        int current = AutocompleteList.SelectedIndex;
        if (current < 0 && delta > 0)
            current = -1;
        int next = Math.Clamp(current + delta, 0, count - 1);
        AutocompleteList.SelectedIndex = next;
        AutocompleteList.ScrollIntoView(AutocompleteList.SelectedItem);
    }

    private void ApplySuggestionAndAdvance(SolicitudAutocompleteSuggestionVm suggestion, bool advanceToQuantity)
    {
        var row = _activeRow ?? MaterialesGrid.CurrentItem as SolicitudItemRowVm;
        if (row == null) return;

        try
        {
            _isApplyingSuggestion = true;

            row.Codigo = suggestion.Codigo;
            row.Descripcion = suggestion.Descripcion;
            row.Unidad = suggestion.Unidad;

            if (_activeEditor != null)
            {
                if (ReferenceEquals(MaterialesGrid.CurrentCell.Column, CodigoColumn))
                    _activeEditor.Text = suggestion.Codigo;
                else if (ReferenceEquals(MaterialesGrid.CurrentCell.Column, DescripcionColumn))
                    _activeEditor.Text = suggestion.Descripcion;
            }

            if (Vm != null)
                Vm.AutocompleteVisible = false;

            MaterialesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            MaterialesGrid.CommitEdit(DataGridEditingUnit.Row, true);
            Vm?.SaveToSession();

            var targetCol = (advanceToQuantity && CantidadColumn != null) ? CantidadColumn : CodigoColumn;
            int rowIdx = MaterialesGrid.Items.IndexOf(row);
            if (rowIdx >= 0 && targetCol != null)
            {
                FocusCellDirectly(rowIdx, targetCol);
            }
        }
        finally
        {
            _isApplyingSuggestion = false;
        }
    }

    private void OnAutocompleteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is not DependencyObject dep) return;

        var listItem = FindParent<ListBoxItem>(dep);
        if (listItem?.DataContext is not SolicitudAutocompleteSuggestionVm suggestion)
            return;

        AutocompleteList.SelectedItem = suggestion;
        ApplySuggestionAndAdvance(suggestion, advanceToQuantity: true);
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent as T;
    }
}
