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

    private static int _unidadPickIndex = -1;
    private static string _unidadOriginalText = "";
    private static TextBox? _activeUnidadBox;

    public static int CantPickIndex => _cantPickIndex;
    public static int UnidadPickIndex => _unidadPickIndex;
    public static TextBox? ActiveUnidadBox => _activeUnidadBox;
    public static bool IsUnidadActive => _activeUnidadBox != null;

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

    public static void ClearCantPickHighlight(IReadOnlyList<CubicacionRowVm>? rows)
    {
        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsFormulaPickTarget)
                    rows[i].IsFormulaPickTarget = false;
            }
        }
        _cantPickIndex = -1;
        _unidadPickIndex = -1;
    }

    public static void FinalizeCantPick(IReadOnlyList<CubicacionRowVm>? rows)
    {
        ClearCantPickHighlight(rows);
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
    }

    public static void CancelCantPick(TextBox? box, IReadOnlyList<CubicacionRowVm>? rows)
    {
        if (box != null) RemoveCantPickToken(box);
        ClearCantPickHighlight(rows);
        _cantPickIndex = -1;
        _cantPickTokenStart = -1;
        _cantPickTokenLength = 0;
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

    public static void ApplyCantPick(TextBox? box, int newIndex, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid)
    {
        if (box == null || rows == null || rows.Count == 0) return;
        newIndex = Math.Clamp(newIndex, 0, rows.Count - 1);
        string token = $"C{newIndex + 1}";

        if (_cantPickIndex < 0)
        {
            if (box.SelectionLength > 0)
            {
                _cantPickTokenStart = box.SelectionStart;
                _cantPickTokenLength = box.SelectionLength;
            }
            else
            {
                _cantPickTokenStart = Math.Clamp(box.CaretIndex, 0, (box.Text ?? "").Length);
                _cantPickTokenLength = 0;
            }
        }

        string text = box.Text ?? "";
        int removeLength = Math.Min(_cantPickTokenLength, text.Length - _cantPickTokenStart);
        text = text.Remove(_cantPickTokenStart, Math.Max(removeLength, 0)).Insert(_cantPickTokenStart, token);
        box.Text = text;
        _cantPickTokenLength = token.Length;
        box.CaretIndex = _cantPickTokenStart + _cantPickTokenLength;

        ClearCantPickHighlight(rows);
        _cantPickIndex = newIndex;
        if (_cantPickIndex >= 0 && _cantPickIndex < rows.Count)
        {
            rows[_cantPickIndex].IsFormulaPickTarget = true;
            try { grid?.ScrollIntoView(rows[_cantPickIndex]); } catch { }
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
        if (tb == null) return -1;
        string text = (tb.Text ?? "").Trim();
        var m = System.Text.RegularExpressions.Regex.Match(text, @"[CcRrFf](?<idx>\d+)\b");
        if (m.Success && int.TryParse(m.Groups["idx"].Value, out int idx))
            return idx - 1;
        return _cantPickIndex;
    }

    public static void ApplyUnidadPick(TextBox? box, int newIndex, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid)
    {
        if (box == null || rows == null || rows.Count == 0) return;
        newIndex = Math.Clamp(newIndex, 0, rows.Count - 1);
        ClearCantPickHighlight(rows);

        _unidadPickIndex = newIndex;
        string token = $"+U{newIndex + 1}";
        box.Text = token;
        box.SelectAll();
        box.Focus();

        if (_unidadPickIndex >= 0 && _unidadPickIndex < rows.Count)
        {
            rows[_unidadPickIndex].IsFormulaPickTarget = true;
            try { grid?.ScrollIntoView(rows[_unidadPickIndex]); } catch { }
        }
    }

    public static void CommitUnidadPick(TextBox? box, CubicacionViewModel? vm)
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

        ClearCantPickHighlight(vm.Rows);
        _unidadPickIndex = -1;
        _activeUnidadBox = null;
    }

    public static void CancelUnidadPick(TextBox? box, IReadOnlyList<CubicacionRowVm>? rows, CubicacionViewModel? vm = null)
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
        ClearCantPickHighlight(rows ?? vm?.Rows);
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

    public static bool TryPickCant(TextBox? box, int rowIndex, int sectorSlot, CubicacionViewModel? vm)
    {
        if (box == null) return false;
        string token = sectorSlot >= 0 ? $"F{rowIndex + 1}C{sectorSlot + 1}" : $"C{rowIndex + 1}";
        string raw = box.Text ?? "";
        string cur = raw.Trim();
        bool allSel = box.SelectionLength > 0 && box.SelectionLength == raw.Length;

        if (allSel || string.IsNullOrEmpty(cur) || cur == "0" || cur == "0,00")
        {
            box.Text = "+" + token;
            box.CaretIndex = box.Text.Length;
        }
        else
        {
            int caret = Math.Clamp(box.CaretIndex, 0, raw.Length);
            if (caret > 0 && (raw[caret - 1] is '+' or '-' or '*' or '/' or '(' or '=' or ',' or ';'))
            {
                box.Text = raw.Insert(caret, token);
                box.CaretIndex = caret + token.Length;
            }
            else
            {
                string op = (raw.EndsWith("+") || raw.EndsWith("-") || raw.EndsWith("*") || raw.EndsWith("/")) ? "" : "+";
                box.Text = raw + op + token;
                box.CaretIndex = box.Text.Length;
            }
        }

        box.Focus();
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

    public static bool HandleUnidadNavigation(TextBox tb, KeyEventArgs e, CubicacionViewModel? vm, DataGrid? grid, DataGridColumn? colUnidad, Action focusCant)
    {
        if (vm == null) return false;
        if (e.Key == Key.Right)
        {
            if (GetCurrentUnidadIndex(tb) >= 0)
            {
                e.Handled = true;
                return true;
            }
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
            ApplyUnidadPick(tb, next, vm.Rows, grid);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Up)
        {
            int maxRow = (vm.Rows != null && vm.Rows.Count > 0) ? vm.Rows.Count - 1 : 0;
            int cur = GetCurrentUnidadIndex(tb);
            int next = cur < 0 ? 0 : Math.Max(cur - 1, 0);
            ApplyUnidadPick(tb, next, vm.Rows, grid);
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            CommitUnidadPick(tb, vm);
            FocusGridCell(grid, 0, colUnidad);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Escape)
        {
            CancelUnidadPick(tb, vm.Rows, vm);
            e.Handled = true;
            return true;
        }
        return false;
    }

    public static bool HandleCantNavigation(TextBox tb, KeyEventArgs e, CubicacionViewModel? vm, DataGrid? grid, DataGridColumn? colCant, Action focusUnidad, Action commitCant, bool isEditingStarted)
    {
        if (vm == null) return false;
        if (e.Key == Key.Left)
        {
            focusUnidad();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Down)
        {
            int maxRow = (vm.Rows != null && vm.Rows.Count > 0) ? vm.Rows.Count - 1 : 0;
            int cur = GetCurrentCantIndex(tb);
            int next = cur < 0 ? 0 : Math.Min(cur + 1, maxRow);
            ApplyCantPick(tb, next, vm.Rows, grid);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Up)
        {
            int cur = GetCurrentCantIndex(tb);
            if (cur > 0)
            {
                ApplyCantPick(tb, cur - 1, vm.Rows, grid);
            }
            else if (cur == 0)
            {
                ApplyCantPick(tb, 0, vm.Rows, grid);
            }
            e.Handled = true;
            return true;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            commitCant();
            FocusGridCell(grid, 0, colCant);
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Escape)
        {
            CancelCantPick(tb, vm.Rows);
            e.Handled = true;
            return true;
        }
        return false;
    }
}

