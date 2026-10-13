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
                if (rows[i].IsFormulaPickTarget)
                    rows[i].IsFormulaPickTarget = false;
            }
        }
        if (grid != null)
        {
            try { grid.SelectedCells.Clear(); } catch { }
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

    public static void CancelCantPick(TextBox? box, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid = null)
    {
        var targetBox = box ?? _activeCantBox;
        if (targetBox != null && !string.IsNullOrEmpty(_cantOriginalText))
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

        int caret = Math.Clamp(box.CaretIndex, 0, raw.Length);
        bool afterOp = (caret > 0 && IsOperatorChar(raw[caret - 1])) || EndsWithOperator(raw);

        if (afterOp || _cantPickIndex < 0 || _cantPickTokenStart < 0)
        {
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

            if (caret > 0 && IsOperatorChar(raw[caret - 1]))
            {
                _cantPickTokenStart = caret;
                _cantPickTokenLength = 0;
            }
            else if (EndsWithOperator(raw))
            {
                _cantPickTokenStart = raw.Length;
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
                }
                _cantPickTokenStart = Math.Clamp(box.CaretIndex, 0, raw.Length);
                _cantPickTokenLength = 0;
            }
        }

        string text = box.Text ?? "";
        int removeLength = Math.Min(_cantPickTokenLength, text.Length - _cantPickTokenStart);
        text = text.Remove(_cantPickTokenStart, Math.Max(removeLength, 0)).Insert(_cantPickTokenStart, token);
        box.Text = text;
        _cantPickTokenLength = token.Length;
        box.CaretIndex = _cantPickTokenStart + _cantPickTokenLength;

        UpdateCantGridHighlight(newIndex, rows, grid, col);
    }

    private static void UpdateCantGridHighlight(int index, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid, DataGridColumn? col)
    {
        ClearCantPickHighlight(rows, grid);
        _cantPickIndex = index;
        if (rows != null && index >= 0 && index < rows.Count)
        {
            var targetRow = rows[index];
            targetRow.IsFormulaPickTarget = true;
            if (grid != null)
            {
                var targetCol = col ?? FindColumn(grid, "CANT");
                if (targetCol != null)
                {
                    try
                    {
                        grid.SelectedCells.Clear();
                        var cell = new DataGridCellInfo(targetRow, targetCol);
                        grid.SelectedCells.Add(cell);
                        grid.CurrentCell = cell;
                    }
                    catch { }
                }
                try { grid.ScrollIntoView(targetRow); } catch { }
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
        ClearCantPickHighlight(rows, grid);

        _unidadPickIndex = newIndex;
        string token = $"+U{newIndex + 1}";
        box.Text = token;
        box.SelectAll();
        box.Focus();

        if (_unidadPickIndex >= 0 && _unidadPickIndex < rows.Count)
        {
            var targetRow = rows[_unidadPickIndex];
            targetRow.IsFormulaPickTarget = true;
            if (grid != null)
            {
                var targetCol = col ?? FindColumn(grid, "UD");
                if (targetCol != null)
                {
                    try
                    {
                        grid.SelectedCells.Clear();
                        var cell = new DataGridCellInfo(targetRow, targetCol);
                        grid.SelectedCells.Add(cell);
                        grid.CurrentCell = cell;
                    }
                    catch { }
                }
                try { grid.ScrollIntoView(targetRow); } catch { }
            }
        }
    }

    public static void CommitUnidadPick(TextBox? box, CubicacionViewModel? vm, DataGrid? grid = null)
    {
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
        if (e.Key == Key.Right)
        {
            CommitUnidadPick(tb, vm, grid);
            focusCant();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Left)
        {
            if (GetCurrentUnidadIndex(tb) >= 0)
            {
                e.Handled = true;
                return true;
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
            int next = cur < 0 ? 0 : Math.Max(cur - 1, 0);
            ApplyUnidadPick(tb, next, vm.Rows, grid, colUnidad);
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            int targetRow = _unidadPickIndex >= 0 ? _unidadPickIndex : 0;
            CommitUnidadPick(tb, vm, grid);
            var targetCol = FindColumn(grid, "CÓDIGO") ?? colUnidad;
            FocusGridCell(grid, targetRow, targetCol);
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
        if (e.Key == Key.Left)
        {
            focusUnidad();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Right)
        {
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
            int caret = Math.Clamp(tb.CaretIndex, 0, raw.Length);
            bool afterOp = (caret > 0 && IsOperatorChar(raw[caret - 1])) || EndsWithOperator(raw);

            if (afterOp)
            {
                _cantPickIndex = -1;
                _cantPickTokenStart = caret > 0 ? caret : raw.Length;
                _cantPickTokenLength = 0;
                ApplyCantPick(tb, 0, vm.Rows, grid, colCant);
            }
            else
            {
                int cur = _cantPickIndex >= 0 ? _cantPickIndex : GetCurrentCantIndex(tb);
                int next = cur < 0 ? 0 : Math.Min(cur + 1, maxRow);
                ApplyCantPick(tb, next, vm.Rows, grid, colCant);
            }
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

            int caret = Math.Clamp(tb.CaretIndex, 0, raw.Length);
            bool afterOp = (caret > 0 && IsOperatorChar(raw[caret - 1])) || EndsWithOperator(raw);

            if (afterOp)
            {
                _cantPickIndex = -1;
                _cantPickTokenStart = caret > 0 ? caret : raw.Length;
                _cantPickTokenLength = 0;
                ApplyCantPick(tb, 0, vm.Rows, grid, colCant);
            }
            else
            {
                int cur = _cantPickIndex >= 0 ? _cantPickIndex : GetCurrentCantIndex(tb);
                int next = cur <= 0 ? 0 : cur - 1;
                ApplyCantPick(tb, next, vm.Rows, grid, colCant);
            }
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            int targetRow = _cantPickIndex >= 0 ? _cantPickIndex : 0;
            commitCant();
            var targetCol = FindColumn(grid, "CÓDIGO") ?? colCant;
            FocusGridCell(grid, targetRow, targetCol);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Escape)
        {
            CancelCantPick(tb, vm.Rows, grid);
            var targetCol = FindColumn(grid, "CÓDIGO") ?? colCant;
            FocusGridCell(grid, 0, targetCol);
            e.Handled = true;
            return true;
        }
        return false;
    }
}

