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
    // Sist./Alim.: TextBox de texto libre con Binding Mode=OneWay (ver comentario en el
    // XAML) — el commit hacia el ViewModel se hace acá, a mano, en vez de confiar en
    // UpdateSourceTrigger=LostFocus de un binding TwoWay dentro de un DataGridTemplateColumn,
    // que no confirma de forma confiable al cerrar la celda con Tab/Enter. internal (no
    // private) para que CubicacionScViewCodeBehindTests pueda invocarlos directamente sin
    // reflection ni montar el DataGrid completo en modo edición.
    internal void TipoEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit) row.Tipo = textBox.Text;
    }

    internal void FaseEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeEditor(sender, out TextBox textBox, out var row) && row.CanEdit) row.Fase = textBox.Text;
    }

    // Ducto Ø, Conductor Band./PVC, Sección Band./PVC: ComboBox editable con lista filtrada
    internal void SeccionTuberiaEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.SeccionTuberia = combo.Text;
    }

    internal void TipoTuberiaEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.TipoTuberia = combo.Text;
    }

    internal void ConductorBandEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.ConductorDistribucion = combo.Text;
    }

    internal void SeccionBandEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.SeccionDistribucion = combo.Text;
    }

    internal void ConductorPvcEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.ConductorDerivacion = combo.Text;
    }

    internal void SeccionPvcEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        if (TryConsumeFilterableCombo(sender, out ComboBox combo, out var row) && row.CanEdit) row.SeccionDerivacion = combo.Text;
    }

    // Tag del ComboBox = nombre de propiedad a confirmar (ConductorDistribucion, etc.).
    internal static List<string> GetMasterOptionsStatic(ComboBox combo)
    {
        if (ComboMasters.TryGetValue(combo, out var list) && list.Count > 0)
            return list;

        if (combo.ItemsSource is IEnumerable<string> strItems)
        {
            var masterList = strItems.ToList();
            ComboMasters.Remove(combo);
            ComboMasters.Add(combo, masterList);
            return masterList;
        }

        return new List<string>();
    }

    private List<string> GetMasterOptions(ComboBox combo)
    {
        if (ComboMasters.TryGetValue(combo, out var list) && list.Count > 0)
            return list;

        var vm = DataContext as CubicacionScViewModel ?? Vm;
        if (vm != null && combo.Tag is string tag)
        {
            IEnumerable<string>? source = tag switch
            {
                "Tipo" => vm.Tipos,
                "Fase" => vm.Fases,
                "TipoRecorrido" => vm.TiposRecorrido,
                "TipoBandeja" => vm.TiposBandeja,
                "TipoTuberia" => vm.TiposTuberia,
                "SeccionTuberia" => vm.DiametrosTuberia,
                "ConductorDistribucion" or "ConductorDerivacion" => vm.TiposConductor,
                "SeccionDistribucion" or "SeccionDerivacion" => vm.SeccionesConductor,
                _ => null
            };

            if (source != null)
            {
                var masterList = source.ToList();
                ComboMasters.Remove(combo);
                ComboMasters.Add(combo, masterList);
                return masterList;
            }
        }

        return GetMasterOptionsStatic(combo);
    }

    private void FilterableCombo_Loaded(object sender, RoutedEventArgs e)
    {
        var combo = (ComboBox)sender;
        GetMasterOptions(combo);

        combo.DropDownOpened -= FilterableCombo_DropDownOpened;
        combo.DropDownOpened += FilterableCombo_DropDownOpened;

        // Enter/Tab/flechas con la misma semántica Excel que las celdas TextBox.
        var editor = FindDescendant<TextBox>(combo);
        if (editor == null) return;
        editor.PreviewKeyDown -= FilterableComboEditor_PreviewKeyDown;
        editor.PreviewKeyDown += FilterableComboEditor_PreviewKeyDown;
        editor.PreviewTextInput -= FilterableComboEditor_PreviewTextInput;
        editor.PreviewTextInput += FilterableComboEditor_PreviewTextInput;

        editor.Focus();
        editor.CaretIndex = editor.Text.Length;
        editor.SelectionLength = 0;
    }

    private void FilterableCombo_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ComboBox combo) return;
        var editor = FindDescendant<TextBox>(combo);
        if (editor == null) return;

        editor.CaretIndex = editor.Text.Length;
        editor.SelectionLength = 0;

        // Deshacer el SelectAll() que WPF ejecuta internamente en ComboBox.OnDropDownOpened
        editor.Dispatcher.BeginInvoke(new Action(() =>
        {
            editor.CaretIndex = editor.Text.Length;
            editor.SelectionLength = 0;
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void FilterableComboEditor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is TextBox tb && tb.SelectionLength > 0 && tb.Text.Length > 0)
        {
            // Evitar que el siguiente carácter tecleado (ej. '5' después de '2') reemplace lo escrito
            tb.CaretIndex = tb.Text.Length;
            tb.SelectionLength = 0;
        }
    }

    // Filtra el desplegable a las opciones que CONTIENEN lo tecleado o cuyo prefijo numérico coincide.
    // NO altera combo.SelectedItem ni combo.Text durante el tecleo para permitir escribir libremente (ej. "25").
    private void FilterableCombo_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isNavigatingFilterCombo) return;
        if (sender is not ComboBox combo) return;

        var master = GetMasterOptions(combo);
        if (master.Count == 0) return;

        var query = (combo.Text ?? "").Trim();
        var editorBox = FindDescendant<TextBox>(combo);
        int caret = editorBox?.CaretIndex ?? query.Length;

        string queryNum = query.Replace(',', '.');

        var filtered = string.IsNullOrEmpty(query)
            ? master
            : master.Where(x =>
                x.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (System.Text.RegularExpressions.Regex.IsMatch(x.Replace(',', '.'), @"^[0-9]+") &&
                 System.Text.RegularExpressions.Regex.Match(x.Replace(',', '.'), @"^([0-9]+(\.[0-9]+)?)").Value.StartsWith(queryNum, StringComparison.OrdinalIgnoreCase))
            )
            .OrderByDescending(x => x.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Length)
            .ToList();

        if (combo.ItemsSource is not List<string> current || current.Count != filtered.Count || !current.SequenceEqual(filtered, StringComparer.Ordinal))
        {
            combo.ItemsSource = filtered;
            if (editorBox != null)
            {
                editorBox.CaretIndex = caret;
                editorBox.SelectionLength = 0;
            }
        }

        if (combo.Text != query)
        {
            combo.Text = query;
            if (editorBox != null) editorBox.CaretIndex = Math.Min(caret, query.Length);
        }

        combo.IsDropDownOpen = filtered.Count > 0 && combo.IsKeyboardFocusWithin;
        if (editorBox != null && editorBox.SelectionLength > 0)
        {
            editorBox.CaretIndex = editorBox.Text.Length;
            editorBox.SelectionLength = 0;
        }

        editorBox?.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (editorBox.IsKeyboardFocusWithin && editorBox.SelectionLength > 0)
            {
                editorBox.CaretIndex = editorBox.Text.Length;
                editorBox.SelectionLength = 0;
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    // Un Enter confirma y baja de fila; Tab avanza de celda. ↑↓ navegan opciones cuando está abierto.
    private void FilterableComboEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var combo = FindAncestor<ComboBox>(tb);
        if (combo == null) return;

        if (e.Key is Key.Up or Key.Down && combo.IsDropDownOpen && combo.Items.Count > 0)
        {
            int count = combo.Items.Count;
            int currentIdx = combo.SelectedIndex;
            int nextIdx = e.Key == Key.Down
                ? (currentIdx + 1 >= count ? 0 : currentIdx + 1)
                : (currentIdx - 1 < 0 ? count - 1 : currentIdx - 1);

            _isNavigatingFilterCombo = true;
            try
            {
                combo.SelectedIndex = nextIdx;
                if (combo.SelectedItem is string selText)
                {
                    tb.Text = selText;
                    tb.SelectAll();
                }
            }
            finally { _isNavigatingFilterCombo = false; }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (combo.IsDropDownOpen)
            {
                combo.IsDropDownOpen = false;
                e.Handled = true;
                return;
            }
            _cancelledFilterCombos.Add(combo);
            CircuitosGrid.CancelEdit(DataGridEditingUnit.Cell);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            if (e.Key == Key.Left && !(tb.CaretIndex == 0 || tb.SelectionLength == tb.Text.Length)) return;
            if (e.Key == Key.Right && !(tb.CaretIndex == tb.Text.Length || tb.SelectionLength == tb.Text.Length)) return;
        }
        else if (e.Key is not (Key.Enter or Key.Return or Key.Tab or Key.Up or Key.Down))
        {
            return;
        }

        var cell = FindAncestor<DataGridCell>(combo);
        if (cell?.Column == null) return;

        combo.IsDropDownOpen = false;
        ResolveFilterableComboText(combo);
        ApplyFilterableComboToRow(combo);

        var rowItem = combo.DataContext as CircuitoScRowVm ?? cell.DataContext as CircuitoScRowVm;
        int rowIndex = rowItem != null ? CircuitosGrid.Items.IndexOf(rowItem) : CircuitosGrid.Items.IndexOf(CircuitosGrid.CurrentCell.Item);

        if (e.Key is Key.Enter or Key.Return)
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int target = rowIndex + ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? -1 : 1);
            if (target >= 0 && target < CircuitosGrid.Items.Count)
            {
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            bool backward = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            DataGridExcelNavigation.MoveToColumnOffset(CircuitosGrid, cell.Column, backward ? -1 : 1, wrapRows: true, rowItem);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int target = rowIndex + (e.Key == Key.Down ? 1 : -1);
            if (target >= 0 && target < CircuitosGrid.Items.Count)
            {
                DataGridExcelNavigation.FocusCell(CircuitosGrid, target, cell.Column);
            }
            e.Handled = true;
            return;
        }

        CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
        int offset = e.Key == Key.Right ? 1 : -1;
        DataGridExcelNavigation.MoveToColumnOffset(CircuitosGrid, cell.Column, offset, wrapRows: false, rowItem);
        e.Handled = true;
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

        // 3. Prefijo exacto (ej. "emt" -> "EMT", "ca" -> "CAÑERIA", "eva" -> "EVA")
        // Ordenado por longitud para preferir la opción más directa ("EVA" antes de "EVANOTOX")
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

    private void ResolveFilterableComboText(ComboBox combo)
    {
        var master = GetMasterOptions(combo);
        var query = (combo.Text ?? "").Trim();

        if (master.Count > 0 && query.Length > 0)
        {
            var match = FindBestCatalogMatch(master, query);
            if (match != null)
            {
                combo.Text = match;
                combo.SelectedItem = match;
                return;
            }
        }

        // Si el usuario escribió un texto y no hay match en catálogo, RESPETAR el texto que escribió.
        // NUNCA sobreescribir con combo.SelectedItem si query no está vacío.
        if (string.IsNullOrWhiteSpace(query))
        {
            if (combo.SelectedItem is string selected && !string.IsNullOrWhiteSpace(selected))
            {
                combo.Text = selected;
            }
        }
        else
        {
            combo.Text = query;
        }
    }

    internal void ApplyFilterableComboToRow(ComboBox combo)
    {
        if (_cancelledFilterCombos.Remove(combo)) return;
        if (combo.DataContext is not CircuitoScRowVm row || !row.CanEdit) return;
        ResolveFilterableComboText(combo);
        if (combo.Tag is not string target || string.IsNullOrEmpty(target)) return;

        switch (target)
        {
            case "ConductorDistribucion": row.ConductorDistribucion = combo.Text; break;
            case "SeccionDistribucion": row.SeccionDistribucion = combo.Text; break;
            case "TipoTuberia": row.TipoTuberia = combo.Text; break;
            case "SeccionTuberia": row.SeccionTuberia = combo.Text; break;
            case "ConductorDerivacion": row.ConductorDerivacion = combo.Text; break;
            case "SeccionDerivacion": row.SeccionDerivacion = combo.Text; break;
        }
    }

    /// <summary>
    /// Confirma síncronamente los datos del editor de celda antes de que DataGrid.CommitEdit
    /// desmonte la plantilla visual, evitando cualquier pérdida de texto al moverse con el teclado.
    /// </summary>
    internal static void CommitCircuitoCellEditor(TextBox textBox, ComboBox? parentCombo, DataGridColumn column, CircuitoScRowVm row)
    {
        if (!row.CanEdit) return;

        if (parentCombo != null)
        {
            parentCombo.IsDropDownOpen = false;
            var master = GetMasterOptionsStatic(parentCombo);
            var query = (textBox.Text ?? "").Trim();
            string finalText = query;
            if (master.Count > 0 && query.Length > 0)
            {
                var match = FindBestCatalogMatch(master, query);
                if (match != null)
                    finalText = match;
            }

            parentCombo.Text = finalText;
            if (parentCombo.Tag is string target && !string.IsNullOrEmpty(target))
            {
                switch (target)
                {
                    case "ConductorDistribucion": row.ConductorDistribucion = finalText; break;
                    case "SeccionDistribucion": row.SeccionDistribucion = finalText; break;
                    case "TipoTuberia": row.TipoTuberia = finalText; break;
                    case "SeccionTuberia": row.SeccionTuberia = finalText; break;
                    case "ConductorDerivacion": row.ConductorDerivacion = finalText; break;
                    case "SeccionDerivacion": row.SeccionDerivacion = finalText; break;
                }
            }
            return;
        }

        var header = column.Header?.ToString() ?? "";
        string text = textBox.Text;
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

    private bool TryConsumeFilterableCombo(object sender, out ComboBox combo, out CircuitoScRowVm row)
    {
        combo = (ComboBox)sender;
        row = null!;
        if (_cancelledFilterCombos.Remove(combo)) return false;
        if (combo.DataContext is not CircuitoScRowVm rowVm) return false;
        ResolveFilterableComboText(combo);
        row = rowVm;
        return true;
    }
}
