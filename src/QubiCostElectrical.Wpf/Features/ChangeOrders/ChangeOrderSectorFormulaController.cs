using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ChangeOrders;

/// <summary>
/// Controlador de navegación interactiva y pickeo de celdas por fórmula (F{fila}C{col})
/// en las casillas de sectores del editor de adicionales.
/// </summary>
internal sealed class ChangeOrderSectorFormulaController
{
    private TextBox? _currentSectorEditor;
    private int _sectorEditSlot = -1;
    private int _sectorEditRow = -1;

    private int _sectorPickRow = -1;
    private int _sectorPickCol = -1;
    private int _sectorPickTokenStart = -1;
    private int _sectorPickTokenLength;
    private bool _isApplyingPick;

    public TextBox? CurrentSectorEditor => _currentSectorEditor;
    public Action<int, int>? OnPickHighlightChanged { get; set; }

    public void OnPreparingCellForEdit(TextBox sectorBox, int slot, int rowIdx)
    {
        _currentSectorEditor = sectorBox;
        _sectorEditSlot = slot;
        _sectorEditRow = rowIdx >= 0 ? rowIdx : 0;
        FinalizeSectorPick();
    }

    public void OnCellEditEnding(TextBox? sectorBox)
    {
        FinalizeSectorPick();
        if (ReferenceEquals(_currentSectorEditor, sectorBox))
        {
            _currentSectorEditor = null;
            _sectorEditSlot = -1;
            _sectorEditRow = -1;
        }
    }

    public static bool IsFormulaArmed(TextBox? tb)
    {
        if (tb == null) return false;
        string t = (tb.Text ?? "").Trim();
        if (string.IsNullOrEmpty(t)) return false;
        if (t.StartsWith("=") || t.StartsWith("+"))
        {
            if (t.Length == 1 || t == "=+") return true;
            char last = t[^1];
            return last is '=' or '+' or '-' or '*' or '/' or '^' or '(' or ',' or ';';
        }
        return false;
    }

    public void FinalizeSectorPick()
    {
        _sectorPickRow = -1;
        _sectorPickCol = -1;
        _sectorPickTokenStart = -1;
        _sectorPickTokenLength = 0;
        OnPickHighlightChanged?.Invoke(-1, -1);
    }

    public void CancelSectorPick(TextBox tb)
    {
        if (_sectorPickTokenStart >= 0 && _sectorPickTokenLength > 0)
        {
            string text = tb.Text ?? "";
            int remLen = Math.Min(_sectorPickTokenLength, Math.Max(0, text.Length - _sectorPickTokenStart));
            if (remLen > 0)
            {
                tb.Text = text.Remove(_sectorPickTokenStart, remLen);
                tb.CaretIndex = _sectorPickTokenStart;
                tb.SelectionLength = 0;
            }
        }
        FinalizeSectorPick();
    }

    public void OnTextChanged(TextBox tb)
    {
        if (_isApplyingPick) return;

        string text = tb.Text ?? "";
        if (text.Length > 0 && text[^1] is '+' or '-' or '*' or '/' or '(' or '^')
        {
            _sectorPickRow = -1;
            _sectorPickCol = -1;
            _sectorPickTokenStart = -1;
            _sectorPickTokenLength = 0;
            OnPickHighlightChanged?.Invoke(-1, -1);
            return;
        }

        if (_sectorPickTokenStart >= 0)
        {
            if (tb.CaretIndex < _sectorPickTokenStart || tb.CaretIndex > _sectorPickTokenStart + _sectorPickTokenLength)
            {
                FinalizeSectorPick();
            }
        }
    }

    public void ApplySectorPick(TextBox tb, int targetRow, int targetCol, int matCount, int colCount)
    {
        if (tb == null || matCount == 0) return;
        targetRow = Math.Clamp(targetRow, 0, matCount - 1);
        targetCol = Math.Clamp(targetCol, 0, Math.Max(0, colCount - 1));

        _sectorPickRow = targetRow;
        _sectorPickCol = targetCol;

        string token = $"F{targetRow + 1}C{targetCol + 1}";
        string raw = tb.Text ?? "";

        if (_sectorPickTokenStart < 0)
        {
            if (tb.SelectionLength > 0)
            {
                _sectorPickTokenStart = tb.SelectionStart;
                _sectorPickTokenLength = tb.SelectionLength;
            }
            else
            {
                int caret = Math.Clamp(tb.CaretIndex, 0, raw.Length);
                if (caret > 0 && (raw[caret - 1] is '+' or '-' or '*' or '/' or '(' or '=' or ',' or ';'))
                {
                    _sectorPickTokenStart = caret;
                    _sectorPickTokenLength = 0;
                }
                else if (string.IsNullOrWhiteSpace(raw) || raw == "0" || raw == "0,00")
                {
                    raw = "+";
                    _sectorPickTokenStart = 1;
                    _sectorPickTokenLength = 0;
                }
                else
                {
                    string op = (raw.EndsWith("+") || raw.EndsWith("-") || raw.EndsWith("*") || raw.EndsWith("/")) ? "" : "+";
                    raw = raw + op;
                    _sectorPickTokenStart = raw.Length;
                    _sectorPickTokenLength = 0;
                }
            }
        }

        int tokenStart = _sectorPickTokenStart;
        _isApplyingPick = true;
        try
        {
            int removeLength = Math.Min(_sectorPickTokenLength, Math.Max(0, raw.Length - tokenStart));
            string newText = raw.Remove(tokenStart, removeLength).Insert(tokenStart, token);
            tb.Text = newText;
            _sectorPickTokenStart = tokenStart;
            _sectorPickTokenLength = token.Length;
            tb.CaretIndex = tokenStart + token.Length;
            tb.SelectionLength = 0;
            tb.Focus();
        }
        finally
        {
            _isApplyingPick = false;
        }

        OnPickHighlightChanged?.Invoke(targetRow, targetCol);
    }

    public bool HandlePreviewKeyDown(KeyEventArgs e, int matCount, int colCount)
    {
        if (_currentSectorEditor == null || !IsFormulaArmed(_currentSectorEditor))
            return false;

        var tb = _currentSectorEditor;

        if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            int currentTargetRow = _sectorPickRow >= 0 ? _sectorPickRow : _sectorEditRow;
            int currentTargetCol = _sectorPickCol >= 0 ? _sectorPickCol : _sectorEditSlot;

            switch (e.Key)
            {
                case Key.Up:
                    currentTargetRow = _sectorPickRow >= 0 ? currentTargetRow - 1 : _sectorEditRow - 1;
                    break;
                case Key.Down:
                    currentTargetRow = _sectorPickRow >= 0 ? currentTargetRow + 1 : _sectorEditRow + 1;
                    break;
                case Key.Left:
                    currentTargetCol = _sectorPickCol >= 0 ? currentTargetCol - 1 : _sectorEditSlot - 1;
                    break;
                case Key.Right:
                    currentTargetCol = _sectorPickCol >= 0 ? currentTargetCol + 1 : _sectorEditSlot + 1;
                    break;
            }

            currentTargetRow = Math.Clamp(currentTargetRow, 0, Math.Max(0, matCount - 1));
            currentTargetCol = Math.Clamp(currentTargetCol, 0, Math.Max(0, colCount - 1));

            ApplySectorPick(tb, currentTargetRow, currentTargetCol, matCount, colCount);
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter or Key.Tab)
        {
            FinalizeSectorPick();
            return false;
        }

        if (e.Key == Key.Escape)
        {
            CancelSectorPick(tb);
            e.Handled = true;
            return true;
        }

        return false;
    }

    public bool HandleCellClick(DependencyObject dep, IList<DataGridColumn> sectorColumns, ChangeOrderEditorViewModel vm)
    {
        if (_currentSectorEditor == null || !IsFormulaArmed(_currentSectorEditor))
            return false;

        var cell = DataGridExcelNavigation.FindParent<DataGridCell>(dep);
        if (cell != null && cell.DataContext is ChangeOrderMaterialVm clickedMat)
        {
            int clickedRow = vm.ActiveMateriales.IndexOf(clickedMat);
            int clickedCol = sectorColumns.IndexOf(cell.Column);
            if (clickedRow >= 0 && clickedCol >= 0)
            {
                if (clickedRow != _sectorEditRow || clickedCol != _sectorEditSlot)
                {
                    ApplySectorPick(_currentSectorEditor, clickedRow, clickedCol, vm.ActiveMateriales.Count, sectorColumns.Count);
                    _currentSectorEditor.Focus();
                    return true;
                }
            }
        }
        return false;
    }
}
