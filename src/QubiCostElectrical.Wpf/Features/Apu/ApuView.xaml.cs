using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Features.Apu;

public partial class ApuView : UserControl, IInGridSearchable
{
    private TextBox? _activeEditor;

    public ApuView(ApuWorkspaceVm vm)
    {
        InitializeComponent();
        ApuSearchOverlay.TargetGrid = ApuGrid;
        ApuGrid.BeginningEdit += OnApuGridBeginningEdit;
        DataContext = vm;
    }

    private ProjectFileData? _cellPreEditSnapshot;

    private void OnApuGridBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (!Vm.CanEdit)
        {
            e.Cancel = true;
            return;
        }
        _cellPreEditSnapshot = Vm.CaptureSnapshot();
    }

    public void OpenGridSearch() => ApuSearchOverlay.Open();
    public void StepGridSearch(bool backward) { if (backward) ApuSearchOverlay.FindPrevious(); else ApuSearchOverlay.FindNext(); }
    public void CloseGridSearch() => ApuSearchOverlay.Close();
    public bool IsGridSearchOpen => ApuSearchOverlay.IsOpen;

    internal ApuWorkspaceVm Vm => (ApuWorkspaceVm)DataContext;

    public void ApplyNavContext(ApuNavContext ctx)
    {
        Vm.NavigateToItem(ctx.ItemId);
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

    private void OnRowEditEnding(object? sender, DataGridRowEditEndingEventArgs e)
    {
        var preEditSnapshot = _cellPreEditSnapshot;
        _cellPreEditSnapshot = null;

        if (e.EditAction == DataGridEditAction.Cancel)
        {
            Vm.ClearPendingUndoForEdit();
            return;
        }
        // Prioridad Normal: con Background, ediciones seguidas dejaban refrescos
        // acumulados en cola hasta que algo forzaba al dispatcher a vaciarla.
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            Vm.OnRowEdited(preEditSnapshot);
            Vm.ClearPendingUndoForEdit();
        });
    }

    private void OnUserControlKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.G)
        {
            if (Vm.OpenQuickJumpCommand.CanExecute(null))
            {
                Vm.OpenQuickJumpCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        if ((e.KeyboardDevice.Modifiers == ModifierKeys.Control && e.Key == Key.M) ||
            (e.KeyboardDevice.Modifiers == ModifierKeys.None && e.Key == Key.Insert))
        {
            if (Vm.CanEdit && Vm.TieneApu)
            {
                AddMaterialInlineAndFocus();
                e.Handled = true;
            }
        }
    }

    private void OnOpenSearchClick(object sender, RoutedEventArgs e)
    {
        ApuSearchOverlay.Open();
    }

    private void OnFindExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ApuSearchOverlay.Open();
    }

    // ─── Eventos de Grilla e IntelliSense ─────────────────────────────────────

    private void OnApuGridPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if ((ReferenceEquals(e.Column, CodigoColumn) || ReferenceEquals(e.Column, DescripcionColumn)) && e.EditingElement is TextBox tb)
        {
            _activeEditor = tb;
            tb.TextChanged += OnEditorTextChanged;
            tb.PreviewKeyDown += OnEditorPreviewKeyDown;

            ApuAutocompletePopup.PlacementTarget = tb;

            if (e.EditingEventArgs is KeyEventArgs or TextCompositionEventArgs)
                tb.CaretIndex = tb.Text.Length;
            else
                tb.SelectAll();
        }
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && ApuGrid.SelectedItem is ApuRecursoVm)
        {
            _ = Vm.SearchAutocompleteAsync(tb.Text);
        }
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Vm.AutocompleteVisible) return;

        switch (e.Key)
        {
            case Key.Down:
                MoveAutocompleteSelection(+1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveAutocompleteSelection(-1);
                e.Handled = true;
                break;

            case Key.Escape:
                Vm.AutocompleteVisible = false;
                e.Handled = true;
                break;

            case Key.Enter:
            case Key.Tab:
                var selected = ApuAutocompleteList.SelectedItem as ApuAutocompleteSuggestionVm;
                if (selected == null && e.Key == Key.Tab)
                {
                    selected = Vm.AutocompleteFirstItem;
                }

                if (selected != null && ApuGrid.SelectedItem is ApuRecursoVm row)
                {
                    ApplySuggestionAndAdvance(row, selected);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    Vm.AutocompleteVisible = false;
                }
                break;
        }
    }

    private void OnApuGridCellEditEnding(object? sender, DataGridCellEditEndingEventArgs e)
    {
        if (_activeEditor != null)
        {
            _activeEditor.TextChanged -= OnEditorTextChanged;
            _activeEditor.PreviewKeyDown -= OnEditorPreviewKeyDown;
            _activeEditor = null;
            ApuAutocompletePopup.PlacementTarget = null;
            Vm.AutocompleteVisible = false;
        }
    }

    private void OnApuGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers == ModifierKeys.None))
        {
            // Si está editando en la columna de Rendimiento y pulsa Enter, avanzar o crear siguiente fila
            if (ReferenceEquals(ApuGrid.CurrentColumn, RendimientoColumn) && ApuGrid.SelectedItem is ApuRecursoVm row)
            {
                int currentIndex = ApuGrid.Items.IndexOf(row);
                int lastIndex = ApuGrid.Items.Count - 1;

                if (currentIndex == lastIndex && Vm.CanEdit)
                {
                    // Confirmar edición actual y agregar automáticamente el siguiente recurso inline
                    ApuGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    AddMaterialInlineAndFocus();
                    e.Handled = true;
                }
                else if (currentIndex < lastIndex && DescripcionColumn != null)
                {
                    // Saltar a la descripción de la siguiente fila
                    ApuGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    int nextIndex = currentIndex + 1;
                    Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                    {
                        DataGridExcelNavigation.FocusCell(ApuGrid, nextIndex, DescripcionColumn);
                        ApuGrid.BeginEdit();
                        FocusEditorTextBox(DescripcionColumn, ApuGrid.Items[nextIndex]);
                    }));
                    e.Handled = true;
                }
            }
        }
    }

    private void OnAutocompleteMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem lbi && lbi.DataContext is ApuAutocompleteSuggestionVm item && ApuGrid.SelectedItem is ApuRecursoVm row)
        {
            ApplySuggestionAndAdvance(row, item);
            e.Handled = true;
        }
    }

    private void MoveAutocompleteSelection(int delta)
    {
        int count = ApuAutocompleteList.Items.Count;
        if (count == 0) return;

        int current = ApuAutocompleteList.SelectedIndex;
        int next = Math.Clamp(current + delta, 0, count - 1);
        ApuAutocompleteList.SelectedIndex = next;
        ApuAutocompleteList.ScrollIntoView(ApuAutocompleteList.SelectedItem);
    }

    private void ApplySuggestionAndAdvance(ApuRecursoVm row, ApuAutocompleteSuggestionVm item)
    {
        Vm.ApplyAutocompleteSuggestion(row, item);
        Vm.AutocompleteVisible = false;
        ApuGrid.CommitEdit(DataGridEditingUnit.Cell, true);

        // Salto automático inmediato a la celda de Rendimiento de la misma fila
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            int rowIndex = ApuGrid.Items.IndexOf(row);
            if (rowIndex >= 0 && RendimientoColumn != null)
            {
                DataGridExcelNavigation.FocusCell(ApuGrid, rowIndex, RendimientoColumn);
                ApuGrid.BeginEdit();
                FocusEditorTextBox(RendimientoColumn, row);
            }
        }));
    }

    public void AddMaterialInlineAndFocus()
    {
        var newVm = Vm.AddMaterialInline();
        if (newVm != null && DescripcionColumn != null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                int newIndex = ApuGrid.Items.IndexOf(newVm);
                if (newIndex >= 0)
                {
                    DataGridExcelNavigation.FocusCell(ApuGrid, newIndex, DescripcionColumn);
                    ApuGrid.BeginEdit();
                    FocusEditorTextBox(DescripcionColumn, newVm);
                }
            }));
        }
    }

    private void FocusEditorTextBox(DataGridColumn column, object item)
    {
        if (column == null || item == null) return;
        var cellContent = column.GetCellContent(item);
        if (cellContent is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
        else if (cellContent != null)
        {
            var childTb = FindVisualChild<TextBox>(cellContent);
            childTb?.Focus();
            childTb?.SelectAll();
        }
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
