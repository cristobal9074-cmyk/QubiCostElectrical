using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScView
{
    private TextBox? _activeCatalogEditor;
    private List<string> _currentCatalogMaster = new();
    private List<string> _currentFilteredList = new();
    private bool _isInternalCatalogTextUpdate;
    private bool _isCatalogBackspacing;

    private static bool IsDirectManualInputTag(string? tag) =>
        string.Equals(tag, "Tipo", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tag, "Fase", StringComparison.OrdinalIgnoreCase);

    // Sist./Alim.: TextBox de texto libre con Binding Mode=OneWay
    internal void TipoEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit) row.Tipo = textBox.Text;
    }

    internal void FaseEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit) row.Fase = textBox.Text;
    }

    internal static void ApplyPropertyToCircuito(CircuitoScRowVm row, string target, string value)
    {
        if (!row.CanEdit) return;
        switch (target)
        {
            case "Tipo": row.Tipo = value; break;
            case "Fase": row.Fase = value; break;
            case "ConductorDistribucion": row.ConductorDistribucion = value; break;
            case "SeccionDistribucion": row.SeccionDistribucion = value; break;
            case "TipoTuberia": row.TipoTuberia = value; break;
            case "TipoTuberia2": row.TipoTuberia2 = value; break;
            case "SeccionTuberia": row.SeccionTuberia = value; break;
            case "SeccionTuberia2": row.SeccionTuberia2 = value; break;
            case "ConductorDerivacion": row.ConductorDerivacion = value; break;
            case "SeccionDerivacion": row.SeccionDerivacion = value; break;
        }
    }

    private List<string> GetCatalogOptionsByTag(string tag)
    {
        var vm = DataContext as CubicacionScViewModel ?? Vm;
        if (vm == null) return new List<string>();

        IEnumerable<string>? source = tag switch
        {
            "TipoTuberia" or "TipoTuberia2" => vm.TiposTuberia,
            "SeccionTuberia" or "SeccionTuberia2" => vm.DiametrosTuberia,
            "ConductorDistribucion" or "ConductorDerivacion" => vm.TiposConductor,
            "SeccionDistribucion" or "SeccionDerivacion" => vm.SeccionesConductor,
            "Tipo" => vm.Tipos,
            "Fase" => vm.Fases,
            "TipoRecorrido" => vm.TiposRecorrido,
            "TipoBandeja" => vm.TiposBandeja,
            _ => null
        };

        return source?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
    }

    private void CatalogEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        _activeCatalogEditor = textBox;
        _isCatalogBackspacing = false;

        string tag = textBox.Tag as string ?? "";
        if (IsDirectManualInputTag(tag))
        {
            CatalogPopup.IsOpen = false;
            _currentCatalogMaster = new List<string>();
            _currentFilteredList = new List<string>();
            textBox.Focus();
            if (textBox.Text.Length > 0)
            {
                textBox.SelectAll();
            }
            return;
        }

        _currentCatalogMaster = GetCatalogOptionsByTag(tag);

        CatalogPopup.PlacementTarget = textBox;
        if (CatalogPopup.Child is FrameworkElement popupBorder)
        {
            popupBorder.MinWidth = Math.Max(textBox.ActualWidth, 120);
        }

        FilterAndShowCatalog(textBox.Text, autoSelectFirst: true);

        textBox.Focus();
        if (textBox.Text.Length > 0 && textBox.SelectionLength == 0)
        {
            textBox.SelectAll();
        }
    }

    private void FilterAndShowCatalog(string currentText, bool autoSelectFirst)
    {
        if (_currentCatalogMaster.Count == 0)
        {
            CatalogPopup.IsOpen = false;
            return;
        }

        string query = (currentText ?? "").Trim();
        string queryNum = query.Replace(',', '.');

        _currentFilteredList = string.IsNullOrEmpty(query)
            ? _currentCatalogMaster.ToList()
            : _currentCatalogMaster.Where(x =>
                x.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (System.Text.RegularExpressions.Regex.IsMatch(x.Replace(',', '.'), @"^[0-9]+") &&
                 System.Text.RegularExpressions.Regex.Match(x.Replace(',', '.'), @"^([0-9]+(\.[0-9]+)?)").Value.StartsWith(queryNum, StringComparison.OrdinalIgnoreCase))
            )
            .OrderByDescending(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Length)
            .ToList();

        CatalogListBox.ItemsSource = _currentFilteredList;

        if (_currentFilteredList.Count == 0)
        {
            CatalogPopup.IsOpen = false;
            return;
        }

        var best = FindBestCatalogMatch(_currentFilteredList, query) ?? _currentFilteredList.FirstOrDefault();
        if (autoSelectFirst && best != null)
        {
            CatalogListBox.SelectedItem = best;
            CatalogListBox.ScrollIntoView(best);
        }
        else
        {
            CatalogListBox.SelectedItem = null;
        }

        CatalogPopup.IsOpen = true;
    }

    private void CatalogEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInternalCatalogTextUpdate) return;
        if (sender is not TextBox tb || tb != _activeCatalogEditor) return;

        string tag = tb.Tag as string ?? "";
        if (IsDirectManualInputTag(tag))
        {
            CatalogPopup.IsOpen = false;

            // En SIST y ALIM se escribe a mano sin lista: auto-mayúsculas y reflejo inmediato en el modelo
            string raw = tb.Text ?? "";
            string upper = raw.ToUpperInvariant();
            if (raw != upper)
            {
                _isInternalCatalogTextUpdate = true;
                int caret = tb.SelectionStart;
                tb.Text = upper;
                tb.SelectionStart = caret;
                _isInternalCatalogTextUpdate = false;
            }

            if (tb.DataContext is CircuitoScRowVm row && row.CanEdit)
            {
                ApplyPropertyToCircuito(row, tag, upper);
            }
            return;
        }

        string currentText = tb.Text ?? "";
        int selStart = tb.SelectionStart;
        int selLen = tb.SelectionLength;

        string query = (selLen > 0 && selStart < currentText.Length)
            ? currentText.Substring(0, selStart).Trim()
            : currentText.Trim();

        FilterAndShowCatalog(query, autoSelectFirst: true);

        // Auto-complete moderno (Auto-relleno estilo Excel):
        if (!_isCatalogBackspacing && !string.IsNullOrEmpty(query))
        {
            var best = FindBestCatalogMatch(_currentFilteredList, query) ?? FindBestCatalogMatch(_currentCatalogMaster, query);
            if (best != null)
            {
                CatalogListBox.SelectedItem = best;
                CatalogListBox.ScrollIntoView(best);

                _isInternalCatalogTextUpdate = true;
                try
                {
                    tb.Text = best;
                    int matchLen = best.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                        ? query.Length
                        : (best.IndexOf(query, StringComparison.OrdinalIgnoreCase) is int idx && idx >= 0 ? idx + query.Length : query.Length);
                    matchLen = Math.Min(matchLen, best.Length);
                    tb.SelectionStart = matchLen;
                    tb.SelectionLength = best.Length - matchLen;
                }
                finally
                {
                    _isInternalCatalogTextUpdate = false;
                }
            }
        }
    }

    private void CatalogEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string tag = tb.Tag as string ?? "";
        bool isDirectManual = IsDirectManualInputTag(tag);

        if (e.Key is Key.Back or Key.Delete)
        {
            _isCatalogBackspacing = true;
            return;
        }
        else if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Return or Key.Tab or Key.Escape or Key.F4))
        {
            _isCatalogBackspacing = false;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.D)
        {
            return;
        }

        bool isAlt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        if (isAlt && (e.SystemKey == Key.D || e.Key == Key.D))
        {
            if (TryToggleCircuitoDisponible())
            {
                e.Handled = true;
                return;
            }
        }
        if (e.Key == Key.F4 || (isAlt && (e.SystemKey == Key.Down || e.Key == Key.Down)))
        {
            if (isDirectManual)
            {
                e.Handled = true;
                return;
            }
            if (CatalogPopup.IsOpen)
            {
                CatalogPopup.IsOpen = false;
            }
            else
            {
                FilterAndShowCatalog(tb.Text, autoSelectFirst: true);
            }
            e.Handled = true;
            return;
        }

        // 1 Solo Enter: confirma valor inmediatamente y avanza de fila
        if (e.Key is Key.Enter or Key.Return)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CommitCatalogEditAndMove(tb, moveDown: true, shift: shift);
            e.Handled = true;
            return;
        }

        // 1 Solo Tab: confirma valor inmediatamente y avanza de columna
        if (e.Key == Key.Tab)
        {
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CommitCatalogEditAndMove(tb, moveDown: false, shift: shift);
            e.Handled = true;
            return;
        }

        // Escape: cierra popup si está abierto, o cancela edición de celda
        if (e.Key == Key.Escape)
        {
            if (CatalogPopup.IsOpen)
            {
                CatalogPopup.IsOpen = false;
                e.Handled = true;
                return;
            }
            CircuitosGrid.CancelEdit(DataGridEditingUnit.Cell);
            e.Handled = true;
            return;
        }

        // Navegación con flechas arriba/abajo
        if (e.Key is Key.Down or Key.Up)
        {
            if (isDirectManual)
            {
                // En SIST y ALIM se escribe a mano: cambiar de fila arriba o abajo como en Excel sin abrir lista
                CommitCatalogEditAndMove(tb, moveDown: true, shift: e.Key == Key.Up);
                e.Handled = true;
                return;
            }

            if (!CatalogPopup.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    FilterAndShowCatalog(tb.Text, autoSelectFirst: true);
                    e.Handled = true;
                    return;
                }
                else
                {
                    CommitCatalogEditAndMove(tb, moveDown: true, shift: true);
                    e.Handled = true;
                    return;
                }
            }

            int count = CatalogListBox.Items.Count;
            if (count > 0)
            {
                int cur = CatalogListBox.SelectedIndex;
                int next = e.Key == Key.Down
                    ? (cur < 0 ? 0 : (cur + 1 >= count ? 0 : cur + 1))
                    : (cur <= 0 ? count - 1 : cur - 1);

                CatalogListBox.SelectedIndex = next;
                if (CatalogListBox.SelectedItem is string sel)
                {
                    _isInternalCatalogTextUpdate = true;
                    try
                    {
                        tb.Text = sel;
                        tb.SelectAll();
                    }
                    finally
                    {
                        _isInternalCatalogTextUpdate = false;
                    }
                    CatalogListBox.ScrollIntoView(sel);
                }
                e.Handled = true;
                return;
            }
        }
    }

    private void CatalogEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (CatalogPopup.IsOpen && !CatalogPopup.IsKeyboardFocusWithin)
        {
            CatalogPopup.IsOpen = false;
        }
        if (sender is TextBox tb && tb.DataContext is CircuitoScRowVm row && row.CanEdit && tb.Tag is string target)
        {
            ApplyPropertyToCircuito(row, target, tb.Text);
        }
    }

    private void CatalogListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep && FindAncestor<ListBoxItem>(dep) is { } item)
        {
            if (item.DataContext is string val && _activeCatalogEditor != null)
            {
                CatalogListBox.SelectedItem = val;
                CommitCatalogEditAndMove(_activeCatalogEditor, moveDown: false);
                e.Handled = true;
            }
        }
    }

    private void CommitCatalogEditAndMove(TextBox tb, bool moveDown, bool shift = false)
    {
        string tag = tb.Tag as string ?? "";
        string finalVal;
        if (IsDirectManualInputTag(tag))
        {
            finalVal = (tb.Text ?? "").Trim().ToUpperInvariant();
        }
        else
        {
            finalVal = CatalogListBox.SelectedItem as string ?? tb.Text.Trim();
            if (string.IsNullOrWhiteSpace(finalVal) && !string.IsNullOrWhiteSpace(tb.Text))
            {
                finalVal = tb.Text.Trim();
            }
        }

        CatalogPopup.IsOpen = false;

        var cell = FindAncestor<DataGridCell>(tb);
        var column = cell?.Column ?? CircuitosGrid.CurrentCell.Column;
        var rowItem = tb.DataContext as CircuitoScRowVm ?? cell?.DataContext as CircuitoScRowVm ?? CircuitosGrid.CurrentCell.Item as CircuitoScRowVm ?? CircuitosGrid.SelectedItem as CircuitoScRowVm;
        int rowIndex = rowItem != null ? CircuitosGrid.Items.IndexOf(rowItem) : CircuitosGrid.SelectedIndex;

        if (tb.Tag is string target && rowItem != null)
        {
            ApplyPropertyToCircuito(rowItem, target, finalVal);
        }

        _isInternalCatalogTextUpdate = true;
        try
        {
            tb.Text = finalVal;
        }
        finally
        {
            _isInternalCatalogTextUpdate = false;
        }

        Keyboard.ClearFocus();
        CircuitosGrid.Focus();

        CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (moveDown)
        {
            int offset = shift ? -1 : 1;
            int targetRow = rowIndex + offset;
            if (targetRow >= 0 && targetRow < CircuitosGrid.Items.Count && column != null)
            {
                CircuitosGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    DataGridExcelNavigation.FocusCell(CircuitosGrid, targetRow, column);
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
        else
        {
            if (column != null)
            {
                CircuitosGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    DataGridExcelNavigation.MoveToColumnOffset(CircuitosGrid, column, shift ? -1 : 1, wrapRows: true, rowItem);
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
    }

    internal static string? FindBestCatalogMatch(IEnumerable<string> catalog, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        query = query.Trim();
        var list = catalog.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (list.Count == 0) return null;

        // 1. Igualdad exacta (case-insensitive)
        var exact = list.FirstOrDefault(x => string.Equals(x.Trim(), query, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // 2. Coincidencia numérica exacta inicial (ej. "25" -> "25mm" o "25mm2", "2.5" -> "2.5mm2", "50" -> "50mm")
        string queryNum = query.Replace(',', '.');
        var numericExact = list.FirstOrDefault(x =>
        {
            string itemNum = x.Trim().Replace(',', '.');
            var m = System.Text.RegularExpressions.Regex.Match(itemNum, @"^([0-9]+(\.[0-9]+)?)");
            return m.Success && string.Equals(m.Groups[1].Value, queryNum, StringComparison.OrdinalIgnoreCase);
        });
        if (numericExact != null) return numericExact;

        // 3. Prefijo exacto (ej. "emt" -> "EMT", "h" -> "H07V-U", "eva" -> "EVA")
        var startsWith = list.Where(x => x.Trim().StartsWith(query, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(x => x.Trim().Length)
                             .FirstOrDefault();
        if (startsWith != null) return startsWith;

        // 4. Subcadena contenida
        var contains = list.Where(x => x.Contains(query, StringComparison.OrdinalIgnoreCase))
                           .OrderBy(x => x.Trim().Length)
                           .FirstOrDefault();
        if (contains != null) return contains;

        return null;
    }

    /// <summary>
    /// Confirma síncronamente los datos del editor de celda antes de que DataGrid.CommitEdit
    /// desmonte la plantilla visual, evitando cualquier pérdida de texto al moverse con el teclado.
    /// </summary>
    internal static void CommitCircuitoCellEditor(TextBox? textBox, ComboBox? parentCombo, DataGridColumn column, CircuitoScRowVm row)
    {
        if (!row.CanEdit) return;

        if (textBox?.Tag is string target && !string.IsNullOrEmpty(target))
        {
            ApplyPropertyToCircuito(row, target, textBox.Text);
            return;
        }

        var header = column.Header?.ToString() ?? "";
        string text = textBox?.Text ?? "";
        if (header.Equals("SIST", StringComparison.OrdinalIgnoreCase))
        {
            row.Tipo = text;
        }
        else if (header.Equals("ALIM", StringComparison.OrdinalIgnoreCase))
        {
            row.Fase = text;
        }
        else if (header.Equals("UBICACIÓN", StringComparison.OrdinalIgnoreCase) || header.Equals("UBICACION", StringComparison.OrdinalIgnoreCase))
        {
            if (!row.TienePisos)
                row.Ubicacion = text;
        }
        else if (header.StartsWith("L. BAND", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.LargoBandeja = d;
        }
        else if (header.StartsWith("L. PVC", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.LargoTuberia = d;
        }
        else if (header.StartsWith("MONTAJ", StringComparison.OrdinalIgnoreCase) || header.Equals("LUMINARIA", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.MontajesAlumbrado = d;
        }
        else if (header.Equals("5/8\"", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.Cajas58 = d;
        }
        else if (header.Equals("A11", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.Cajas100x100 = d;
        }
        else if (header.Equals("A01", StringComparison.OrdinalIgnoreCase))
        {
            if (DataGridExcelNavigation.TryParseFlexibleDecimal(text, out var d)) row.Cajas100x65 = d;
        }
    }

    private static bool TryConsumeEditor(object sender, out TextBox textBox, out CircuitoScRowVm row)
    {
        textBox = (TextBox)sender;
        row = null!;
        if (Equals(textBox.Tag, CancelledEditTag)) { textBox.Tag = null; return false; }
        if (textBox.DataContext is not CircuitoScRowVm rowVm) return false;
        row = rowVm;
        return true;
    }
}
