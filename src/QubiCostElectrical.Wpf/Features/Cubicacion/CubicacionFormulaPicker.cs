using System;
using System.Collections.Generic;
using System.Windows.Controls;

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

    public static void SetActiveUnidadBox(TextBox? box)
    {
        _activeUnidadBox = box;
        _unidadOriginalText = box?.Text ?? "";
        _unidadPickIndex = -1;
    }

    public static void ClearCantPickHighlight(IReadOnlyList<CubicacionRowVm>? rows)
    {
        if (rows != null && _cantPickIndex >= 0 && _cantPickIndex < rows.Count)
            rows[_cantPickIndex].IsFormulaPickTarget = false;
        if (rows != null && _unidadPickIndex >= 0 && _unidadPickIndex < rows.Count)
            rows[_unidadPickIndex].IsFormulaPickTarget = false;
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
        if (box == null) return;
        if (newIndex < 0)
        {
            RemoveCantPickToken(box);
            ClearCantPickHighlight(rows);
            _cantPickIndex = -1;
            return;
        }

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
        if (rows != null && _cantPickIndex >= 0 && _cantPickIndex < rows.Count)
        {
            rows[_cantPickIndex].IsFormulaPickTarget = true;
            if (grid != null && _cantPickIndex < grid.Items.Count)
                grid.ScrollIntoView(grid.Items[_cantPickIndex]);
        }
    }

    public static void ApplyUnidadPick(TextBox? box, int newIndex, IReadOnlyList<CubicacionRowVm>? rows, DataGrid? grid)
    {
        if (box == null || rows == null || newIndex < 0 || newIndex >= rows.Count) return;
        ClearCantPickHighlight(rows);

        _unidadPickIndex = newIndex;
        string token = $"+F{newIndex + 1}";
        box.Text = token;
        box.CaretIndex = token.Length;

        rows[_unidadPickIndex].IsFormulaPickTarget = true;
        if (grid != null && _unidadPickIndex < grid.Items.Count)
            grid.ScrollIntoView(grid.Items[_unidadPickIndex]);
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

    public static void CancelUnidadPick(TextBox? box, IReadOnlyList<CubicacionRowVm>? rows)
    {
        if (box != null && !string.IsNullOrEmpty(_unidadOriginalText))
            box.Text = _unidadOriginalText;
        ClearCantPickHighlight(rows);
        _unidadPickIndex = -1;
        _activeUnidadBox = null;
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
}
