using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Lógica de inserción y selección interactiva tipo Excel de casillas (UD, CANT y sectores) al hacer clic o navegar.
/// </summary>
internal static class CubicacionFormulaPicker
{
    private static int _cantPickIndex = -1;
    private static int _cantPickTokenStart = -1;
    private static int _cantPickTokenLength;
    private static string _cantOriginalText = "";
    private static TextBox? _activeCantBox;

    private static int _unidadPickIndex = -1;
    private static string _unidadOriginalText = "";
    private static TextBox? _activeUnidadBox;
    private static bool _suppressLostFocusCommit;

    public static bool SuppressLostFocusCommit => _suppressLostFocusCommit;
    public static void SetSuppressLostFocus(bool suppress) => _suppressLostFocusCommit = suppress;
    public static int CantPickIndex => _cantPickIndex;
    public static int UnidadPickIndex => _unidadPickIndex;
    public static TextBox? ActiveUnidadBox => _activeUnidadBox;
    public static bool IsUnidadActive => _activeUnidadBox != null;
    public static TextBox? ActiveCantBox => _activeCantBox;
    public static bool IsCantActive => _activeCantBox != null;

    public static void SetActiveCantBox(TextBox? box, CubicacionViewModel? vm = null)
    {
        _activeCantBox = box;
        if (box == null) return;
        if (vm?.SelectedPartida != null && !string.IsNullOrWhiteSpace(vm.SelectedPartida.CantidadFormula))
        {
            box.Text = vm.SelectedPartida.CantidadFormula;
            _cantOriginalText = vm.SelectedPartida.CantidadFormula;
        }
        else
        {
            _cantOriginalText = box.Text ?? "";
        }
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
    }

    public static bool IsOperatorChar(char ch) => ch is '+' or '-' or '*' or '/' or '^' or '(' or '=' or ',' or ';';

    public static void NotifyCantKeyDown(Key key)
    {
        // Si el usuario escribe una tecla de operador o retroceso, el token actual ya quedó fijo
        // y el próximo pickeo debe ser un nuevo término
        if (key is Key.OemPlus or Key.Add or Key.OemMinus or Key.Subtract or Key.Multiply or Key.Divide or Key.Back or Key.Delete)
        {
            _cantPickIndex = -1;
            _cantPickTokenStart = -1;
            _cantPickTokenLength = 0;
        }
    }

    public static void NotifyCantTextInput(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        for (int i = 0; i < text.Length; i++)
        {
            if (IsOperatorChar(text[i]))
            {
                _cantPickIndex = -1;
                _cantPickTokenStart = -1;
                _cantPickTokenLength = 0;
                break;
            }
        }
    }

    public static bool EndsWithOperator(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.TrimEnd();
        return t.Length > 0 && IsOperatorChar(t[^1]);
    }

    public static bool IsFormulaMode(TextBox? box)
    {
        if (box == null) return false;
        if (_cantPickIndex >= 0) return true;
        string text = (box.Text ?? "").Trim();
        if (text.StartsWith("+") || text.StartsWith("=")) return true;
        return text.IndexOfAny(['+', '-', '*', '/', '^', '(']) >= 0;
    }

    public static void SetActiveUnidadBox(TextBox? box, CubicacionViewModel? vm = null)
    {
        _activeUnidadBox = box;
        string text = (box?.Text ?? "").Trim();
        if (!text.StartsWith("+") && !text.StartsWith("="))
        {
            if (!string.IsNullOrEmpty(text))
                _unidadOriginalText = text;
            else if (!string.IsNullOrEmpty(vm?.PartidaUnidad))
                _unidadOriginalText = vm.PartidaUnidad;
            else
                _unidadOriginalText = text;
        }
        else if (string.IsNullOrEmpty(_unidadOriginalText) || _unidadOriginalText.StartsWith("+") || _unidadOriginalText.StartsWith("="))
        {
            _unidadOriginalText = vm?.PartidaUnidad ?? "";
        }
        _unidadPickIndex = -1;
    }

    public static void ClearCantPickHighlight(IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid = null)
    {
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsCantPickTarget)
                    rows[i].IsCantPickTarget = false;
                if (rows[i].IsFormulaPickTarget)
                    rows[i].IsFormulaPickTarget = false;
            }
        }
        _cantPickIndex = -1;
        _unidadPickIndex = -1;
    }

    public static void FinalizeCantPick(IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid = null)
    {
        ClearCantPickHighlight(rows, grid);
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
        _activeCantBox = null;
    }

    public static void CancelCantPick(TextBox? box, IReadOnlyList<CubicacionRowVm>? rows, CubicacionViewModel? vm = null, DataGrid? grid = null)
    {
        var targetBox = box ?? _activeCantBox;
        if (targetBox != null && vm != null)
        {
            targetBox.Text = vm.PartidaCantidadText;
        }
        else if (targetBox != null && !string.IsNullOrEmpty(_cantOriginalText))
        {
            targetBox.Text = _cantOriginalText;
            targetBox.SelectAll();
        }
        else if (targetBox != null)
        {
            RemoveCantPickToken(targetBox);
        }
        ClearCantPickHighlight(rows, grid);
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
        _activeCantBox = null;
    }

    public static void RemoveCantPickToken(TextBox? box)
    {
        if (box == null || _cantPickTokenLength <= 0 || _cantPickTokenStart < 0) return;
        string text = box.Text ?? "";
        int removeLength = Math.Min(_cantPickTokenLength, text.Length - _cantPickTokenStart);
        if (removeLength <= 0) return;
        box.Text = text.Remove(_cantPickTokenStart, removeLength);
        box.CaretIndex = _cantPickTokenStart;
        _cantPickTokenLength = 0;
    }

    public static void ApplyCantPick(TextBox? box, int newIndex, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid, DataGridColumn? col = null)
    {
        if (box == null || rows == null || rows.Count == 0) return;
        newIndex = Math.Clamp(newIndex, 0, rows.Count - 1);
        string token = $"C{newIndex + 1}";
        string raw = box.Text ?? "";
        // Si el texto termina con un operador o el cursor está justo tras un operador, debe empezar un nuevo token
        int caret = Math.Clamp(box.CaretIndex, 0, raw.Length);
        bool afterOp = (caret > 0 && IsOperatorChar(raw[caret - 1])) || EndsWithOperator(raw);

        // Si ya hay un pick activo de esta sesión y NO acabamos de tipear un operador, lo reemplazamos
        if (!afterOp && _cantPickIndex >= 0 && _cantPickTokenStart >= 0 && _cantPickTokenStart <= raw.Length)
        {
            int removeLength = Math.Min(_cantPickTokenLength, raw.Length - _cantPickTokenStart);
            string updated = raw.Remove(_cantPickTokenStart, Math.Max(removeLength, 0)).Insert(_cantPickTokenStart, token);
            box.Text = updated;
            _cantPickTokenLength = token.Length;
            _cantPickIndex = newIndex;
            box.CaretIndex = _cantPickTokenStart + _cantPickTokenLength;
            UpdateCantGridHighlight(newIndex, rows, grid, col);
            return;
        }

        // Si no hay token activo, determinamos dónde insertarlo
        if (box.SelectionLength > 0 && box.SelectionLength == raw.Length)
        {
            box.Text = "+" + token;
            _cantPickTokenStart = 1;
            _cantPickTokenLength = token.Length;
            _cantPickIndex = newIndex;
            box.CaretIndex = box.Text.Length;
            UpdateCantGridHighlight(newIndex, rows, grid, col);
            return;
        }

        if (afterOp)
        {
            _cantPickTokenStart = caret > 0 ? caret : raw.Length;
            _cantPickTokenLength = 0;
        }
        else if (box.SelectionLength > 0)
        {
            _cantPickTokenStart = box.SelectionStart;
            _cantPickTokenLength = box.SelectionLength;
        }
        else
        {
            if (string.IsNullOrEmpty(raw) || raw == "0" || raw == "0,00")
            {
                box.Text = "+";
                raw = "+";
                _cantPickTokenStart = 1;
                _cantPickTokenLength = 0;
            }
            else
            {
                // Si había un número plano o texto sin operador al final, anexamos '+'
                string op = "+";
                raw += op;
                box.Text = raw;
                _cantPickTokenStart = raw.Length;
                _cantPickTokenLength = 0;
            }
        }

        string current = box.Text ?? "";
        int startPos = Math.Clamp(_cantPickTokenStart, 0, current.Length);
        int remLen = Math.Min(_cantPickTokenLength, current.Length - startPos);
        string text = current.Remove(startPos, Math.Max(remLen, 0)).Insert(startPos, token);
        box.Text = text;
        _cantPickTokenStart = startPos;
        _cantPickTokenLength = token.Length;
        _cantPickIndex = newIndex;
        box.CaretIndex = _cantPickTokenStart + _cantPickTokenLength;

        UpdateCantGridHighlight(newIndex, rows, grid, col);
    }

    private static void UpdateCantGridHighlight(int index, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid, DataGridColumn? col = null)
    {
        _cantPickIndex = index;
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].IsCantPickTarget = (i == _cantPickIndex);
                if (rows[i].IsFormulaPickTarget)
                    rows[i].IsFormulaPickTarget = false;
            }
        }
        _suppressLostFocusCommit = true;
        try
        {
            if (grid != null && _cantPickIndex >= 0 && rows != null && _cantPickIndex < rows.Count)
            {
                try { grid.ScrollIntoView(rows[_cantPickIndex]); } catch { }
            }
        }
        finally
        {
            if (_activeCantBox != null)
            {
                _activeCantBox.Focus();
                Keyboard.Focus(_activeCantBox);
                _activeCantBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _activeCantBox?.Focus();
                    if (_activeCantBox != null) Keyboard.Focus(_activeCantBox);
                    _suppressLostFocusCommit = false;
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                _suppressLostFocusCommit = false;
            }
        }
    }

    private static readonly System.Text.RegularExpressions.Regex UnidadRefRegex = new(@"^[\+=]?[UuCcRrFf](?<idx>\d+)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static int GetCurrentUnidadIndex(TextBox? tb)
    {
        if (_unidadPickIndex >= 0)
            return _unidadPickIndex;
        if (tb == null) return -1;
        string text = (tb.Text ?? "").Trim();
        var m = System.Text.RegularExpressions.Regex.Match(text, @"[UuCcRrFf](?<idx>\d+)");
        if (m.Success && int.TryParse(m.Groups["idx"].Value, out int idx))
            return idx - 1;
        return -1;
    }

    public static int GetCurrentCantIndex(TextBox? tb)
    {
        if (_cantPickIndex >= 0) return _cantPickIndex;
        if (tb == null) return -1;
        string text = (tb.Text ?? "").Trim();
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"[CcRrFf](?<idx>\d+)\b");
        if (matches.Count > 0)
        {
            var last = matches[^1];
            if (int.TryParse(last.Groups["idx"].Value, out int idx))
                return idx - 1;
        }
        return -1;
    }

    public static void ApplyUnidadPick(TextBox? box, int newIndex, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid, DataGridColumn? col = null)
    {
        if (box == null || rows == null || rows.Count == 0) return;
        newIndex = Math.Clamp(newIndex, 0, rows.Count - 1);
        _unidadPickIndex = newIndex;
        string token = $"+U{newIndex + 1}";
        box.Text = token;
        box.CaretIndex = box.Text.Length;

        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].IsFormulaPickTarget = (i == _unidadPickIndex);
                if (rows[i].IsCantPickTarget)
                    rows[i].IsCantPickTarget = false;
            }
        }

        _suppressLostFocusCommit = true;
        try
        {
            if (grid != null && rows != null && _unidadPickIndex >= 0 && _unidadPickIndex < rows.Count)
            {
                try { grid.ScrollIntoView(rows[_unidadPickIndex]); } catch { }
            }
        }
        finally
        {
            box.Focus();
            Keyboard.Focus(box);
            box.Dispatcher.BeginInvoke(new Action(() =>
            {
                box.Focus();
                Keyboard.Focus(box);
                _suppressLostFocusCommit = false;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    public static void CommitUnidadPick(TextBox? box, CubicacionViewModel? vm, DataGrid? grid = null, bool force = false)
    {
        if (!force && _suppressLostFocusCommit) return;
        if (vm == null) return;
        var targetBox = box ?? _activeUnidadBox;
        string raw = (targetBox?.Text ?? "").Trim();
        string resolved = CubicacionPartidaHelper.ResolveUnidadReference(raw, vm.Rows);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            if (targetBox != null) targetBox.Text = resolved;
            vm.PartidaUnidadText = resolved;
        }
        else if (!string.IsNullOrWhiteSpace(raw) && !raw.StartsWith("+") && !raw.StartsWith("="))
        {
            vm.PartidaUnidadText = raw;
        }

        ClearCantPickHighlight(vm.Rows, grid);
        _unidadPickIndex = -1;
        _activeUnidadBox = null;
    }

    public static void CancelUnidadPick(TextBox? box, IReadOnlyList<CubicacionRowVm>? rows, CubicacionViewModel? vm = null, DataGrid? grid = null)
    {
        var targetBox = box ?? _activeUnidadBox;
        string orig = (!string.IsNullOrEmpty(_unidadOriginalText) && !_unidadOriginalText.StartsWith("+") && !_unidadOriginalText.StartsWith("="))
            ? _unidadOriginalText
            : (vm?.PartidaUnidad ?? "");

        if (targetBox != null)
        {
            targetBox.Text = orig;
            targetBox.SelectAll();
            targetBox.Focus();
        }
        if (vm != null)
        {
            vm.PartidaUnidadText = orig;
        }
        ClearCantPickHighlight(rows ?? vm?.Rows, grid);
        _unidadPickIndex = -1;
        _activeUnidadBox = null;
        _unidadOriginalText = orig;
    }

    public static bool TryPickUnidad(TextBox? focusedTb, CubicacionRowVm? targetRow, TextBox? unidadHeaderBox, CubicacionViewModel? vm)
    {
        if (focusedTb == null || targetRow == null || string.IsNullOrWhiteSpace(targetRow.Unidad)) return false;

        focusedTb.Text = targetRow.Unidad;
        if (vm != null && (focusedTb == unidadHeaderBox || focusedTb.Tag as string == "PartidaUnidad"))
            vm.PartidaUnidadText = targetRow.Unidad;

        focusedTb.Dispatcher.BeginInvoke(new Action(() => focusedTb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);
        focusedTb.Focus();
        return true;
    }

    public static void PickCantCell(TextBox? box, int rowIndex, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid, DataGridColumn? colCant)
    {
        if (box == null || rows == null || rowIndex < 0 || rowIndex >= rows.Count) return;
        string token = $"C{rowIndex + 1}";
        string raw = box.Text ?? "";
        bool allSel = box.SelectionLength > 0 && box.SelectionLength == raw.Length;

        if (allSel || string.IsNullOrEmpty(raw.Trim()) || raw.Trim() == "0" || raw.Trim() == "0,00")
        {
            box.Text = "+" + token;
            _cantPickTokenStart = 1;
            _cantPickTokenLength = token.Length;
            _cantPickIndex = rowIndex;
            box.CaretIndex = box.Text.Length;
        }
        else
        {
            int caret = Math.Clamp(box.CaretIndex, 0, raw.Length);
            bool afterOp = (caret > 0 && IsOperatorChar(raw[caret - 1])) || EndsWithOperator(raw);
            if (afterOp)
            {
                int insertPos = caret > 0 ? caret : raw.Length;
                box.Text = raw.Insert(insertPos, token);
                _cantPickTokenStart = insertPos;
                _cantPickTokenLength = token.Length;
                _cantPickIndex = rowIndex;
                box.CaretIndex = insertPos + token.Length;
            }
            else if (_cantPickIndex >= 0 && _cantPickTokenStart >= 0)
            {
                int removeLen = Math.Min(_cantPickTokenLength, raw.Length - _cantPickTokenStart);
                box.Text = raw.Remove(_cantPickTokenStart, Math.Max(removeLen, 0)).Insert(_cantPickTokenStart, token);
                _cantPickTokenLength = token.Length;
                _cantPickIndex = rowIndex;
                box.CaretIndex = _cantPickTokenStart + token.Length;
            }
            else
            {
                string op = (raw.EndsWith("+") || raw.EndsWith("-") || raw.EndsWith("*") || raw.EndsWith("/")) ? "" : "+";
                int startPos = raw.Length + op.Length;
                box.Text = raw + op + token;
                _cantPickTokenStart = startPos;
                _cantPickTokenLength = token.Length;
                _cantPickIndex = rowIndex;
                box.CaretIndex = box.Text.Length;
            }
        }

        UpdateCantGridHighlight(rowIndex, rows, grid, colCant);
        box.Focus();
    }

    public static bool TryPickCant(TextBox? box, int rowIndex, int sectorSlot, CubicacionViewModel? vm)
    {
        if (box == null) return false;
        PickCantCell(box, rowIndex, vm?.Rows, null, null);
        return true;
    }

    public static bool TryPickSector(TextBox? focusedTb, int rowIndex, int sectorSlot)
    {
        if (focusedTb == null) return false;
        string token = sectorSlot >= 0 ? $"F{rowIndex + 1}C{sectorSlot + 1}" : $"C{rowIndex + 1}";
        string raw = focusedTb.Text ?? "";
        int caret = Math.Clamp(focusedTb.CaretIndex, 0, raw.Length);

        if (caret > 0 && (raw[caret - 1] is '+' or '-' or '*' or '/' or '(' or '=' or ',' or ';'))
        {
            focusedTb.Text = raw.Insert(caret, token);
            focusedTb.CaretIndex = caret + token.Length;
        }
        else
        {
            string op = (raw.EndsWith("+") || raw.EndsWith("-") || raw.EndsWith("*") || raw.EndsWith("/")) ? "" : "+";
            focusedTb.Text = raw + op + token;
            focusedTb.CaretIndex = focusedTb.Text.Length;
        }

        focusedTb.Focus();
        return true;
    }

    public static void FocusGridCell(DataGrid? grid, int rowIndex, DataGridColumn? col)
    {
        if (grid == null || grid.Items.Count == 0 || rowIndex < 0 || rowIndex >= grid.Items.Count || col == null) return;
        var row = grid.Items[rowIndex];
        grid.ScrollIntoView(row);
        if (grid.SelectionUnit != DataGridSelectionUnit.FullRow)
        {
            grid.SelectedCells.Clear();
            var cell = new DataGridCellInfo(row, col);
            grid.SelectedCells.Add(cell);
            grid.CurrentCell = cell;
        }
        else
        {
            grid.SelectedItem = row;
        }
        grid.Focus();
    }

    public static DataGridColumn? FindColumn(DataGrid? grid, string headerText)
    {
        if (grid == null) return null;
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            if (string.Equals(grid.Columns[i].Header?.ToString(), headerText, StringComparison.OrdinalIgnoreCase))
                return grid.Columns[i];
        }
        return null;
    }

    public static bool TryNavigateUpFromFirstRow(DataGrid? grid, KeyEventArgs e, TextBox? unidadBox, TextBox? cantBox, DataGridColumn? cantCol, IReadOnlyList<DataGridColumn>? sectorCols)
    {
        if (grid == null || e.Key != Key.Up || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)) != ModifierKeys.None)
            return false;

        int currentRow = grid.CurrentCell.IsValid && grid.CurrentCell.Item != null ? grid.Items.IndexOf(grid.CurrentCell.Item) : grid.SelectedIndex;
        if (currentRow > 0) return false;

        grid.CommitEdit(DataGridEditingUnit.Cell, true);
        grid.CommitEdit(DataGridEditingUnit.Row, true);

        var curCol = grid.CurrentCell.Column;
        bool isQtySide = (cantCol != null && curCol == cantCol) || (sectorCols != null && curCol != null && sectorCols.Contains(curCol));
        var targetBox = isQtySide ? cantBox : unidadBox;
        if (targetBox != null)
        {
            targetBox.Focus();
            targetBox.SelectAll();
            e.Handled = true;
            return true;
        }
        return false;
    }

    public static void FocusFirstCubicacionCell(DataGrid? grid, IReadOnlyList<DataGridColumn>? sectorCols, DataGridColumn? cantCol)
    {
        if (grid == null || grid.Items.Count == 0) return;
        DataGridColumn? targetCol = (sectorCols != null && sectorCols.Count > 0)
            ? sectorCols[0]
            : (cantCol ?? FindColumn(grid, "CÓDIGO"));
        if (targetCol != null)
        {
            FocusGridCell(grid, 0, targetCol);
        }
    }

    public static bool HandleUnidadNavigation(TextBox tb, KeyEventArgs e, CubicacionViewModel? vm, DataGrid? grid, DataGridColumn? colUnidad, Action focusCant)
    {
        if (vm == null) return false;
        if (e.Key == Key.Tab)
        {
            CommitUnidadPick(tb, vm, grid);
            focusCant();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Right)
        {
            if (_unidadPickIndex >= 0)
            {
                CommitUnidadPick(tb, vm, grid);
                focusCant();
                e.Handled = true;
                return true;
            }
            if (tb.SelectionLength == 0 && tb.CaretIndex < (tb.Text ?? "").Length)
            {
                return false; // permitir que la flecha derecha mueva el cursor dentro del texto
            }
            CommitUnidadPick(tb, vm, grid);
            focusCant();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Left)
        {
            if (_unidadPickIndex >= 0)
            {
                e.Handled = true;
                return true;
            }
            if (tb.SelectionLength == 0 && tb.CaretIndex > 0)
            {
                return false; // permitir que la flecha izquierda mueva el cursor dentro del texto
            }
        }
        if (e.Key == Key.Down)
        {
            int maxRow = (vm.Rows != null && vm.Rows.Count > 0) ? vm.Rows.Count - 1 : 0;
            int cur = GetCurrentUnidadIndex(tb);
            int next = cur < 0 ? 0 : Math.Min(cur + 1, maxRow);
            ApplyUnidadPick(tb, next, vm.Rows, grid, colUnidad);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Up)
        {
            int maxRow = (vm.Rows != null && vm.Rows.Count > 0) ? vm.Rows.Count - 1 : 0;
            int cur = GetCurrentUnidadIndex(tb);
            int next = cur <= 0 ? 0 : cur - 1;
            ApplyUnidadPick(tb, next, vm.Rows, grid, colUnidad);
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            CommitUnidadPick(tb, vm, grid, force: true);
            focusCant();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Escape)
        {
            CancelUnidadPick(tb, vm.Rows, vm, grid);
            var targetCol = FindColumn(grid, "CÓDIGO") ?? colUnidad;
            FocusGridCell(grid, 0, targetCol);
            e.Handled = true;
            return true;
        }
        return false;
    }

    public static bool HandleCantNavigation(TextBox tb, KeyEventArgs e, CubicacionViewModel? vm, DataGrid? grid, DataGridColumn? colCant, Action focusUnidad, Action commitCant, Action? focusFirstSector = null, bool isEditingStarted = false)
    {
        if (vm == null) return false;
        if (e.Key == Key.Tab)
        {
            commitCant();
            if (focusFirstSector != null) focusFirstSector();
            else FocusGridCell(grid, 0, FindColumn(grid, "CÓDIGO") ?? colCant);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Left)
        {
            if (_cantPickIndex >= 0)
            {
                e.Handled = true;
                return true;
            }
            if (tb.SelectionLength == 0 && tb.CaretIndex > 0)
            {
                return false; // permitir que la flecha izquierda mueva el cursor dentro del texto
            }
            focusUnidad();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Right)
        {
            if (_cantPickIndex >= 0)
            {
                e.Handled = true;
                return true;
            }
            if (tb.SelectionLength == 0 && tb.CaretIndex < (tb.Text ?? "").Length)
            {
                return false; // permitir que la flecha derecha mueva el cursor dentro del texto
            }
            if (focusFirstSector != null)
            {
                commitCant();
                focusFirstSector();
                e.Handled = true;
                return true;
            }
        }
        if (e.Key == Key.Down)
        {
            string raw = tb.Text ?? "";
            bool isFormula = IsFormulaMode(tb);

            if (!isFormula && _cantPickIndex < 0)
            {
                commitCant();
                FocusGridCell(grid, 0, FindColumn(grid, "CÓDIGO") ?? colCant);
                e.Handled = true;
                return true;
            }

            int maxRow = (vm.Rows != null && vm.Rows.Count > 0) ? vm.Rows.Count - 1 : 0;
            int cur = _cantPickIndex >= 0 ? _cantPickIndex : GetCurrentCantIndex(tb);
            int next = cur < 0 ? 0 : Math.Min(cur + 1, maxRow);
            ApplyCantPick(tb, next, vm.Rows, grid, colCant);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Up)
        {
            string raw = tb.Text ?? "";
            bool isFormula = IsFormulaMode(tb);

            if (!isFormula && _cantPickIndex < 0)
            {
                e.Handled = true;
                return true;
            }

            int cur = _cantPickIndex >= 0 ? _cantPickIndex : GetCurrentCantIndex(tb);
            int next = cur <= 0 ? 0 : cur - 1;
            ApplyCantPick(tb, next, vm.Rows, grid, colCant);
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            int targetRow = _cantPickIndex >= 0 ? _cantPickIndex : 0;
            _suppressLostFocusCommit = false;
            commitCant();
            var targetCol = FindColumn(grid, "CÓDIGO") ?? colCant;
            FocusGridCell(grid, targetRow, targetCol);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Escape)
        {
            _suppressLostFocusCommit = false;
            CancelCantPick(tb, vm.Rows, vm, grid);
            var targetCol = FindColumn(grid, "CÓDIGO") ?? colCant;
            FocusGridCell(grid, 0, targetCol);
            e.Handled = true;
            return true;
        }
        return false;
    }
}

