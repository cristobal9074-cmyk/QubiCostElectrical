using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

/// <summary>
/// Controlador de navegación interactiva y pickeo de celdas por fórmula (F{fila}C{col})
/// en las columnas de sector de la grilla de cubicación.
/// </summary>
internal sealed class CubicacionSectorFormulaController
{
    private static readonly Regex SectorCellReferenceRegex =
        new(@"[Ff](\d+)[Cc](\d+)", RegexOptions.Compiled);

    private int _sectorPickRow = -1;
    private int _sectorPickCol = -1;
    private int _sectorPickTokenStart = -1;
    private int _sectorPickTokenLength;
    private int _sectorLastPickedRow = -1;
    private int _sectorLastPickedCol = -1;
    private readonly HashSet<int> _sectorHighlightedRows = new();

    public bool IsPickingInProgress => _sectorPickRow >= 0;

    public void OnCellEditEnding(CubicacionViewModel? vm)
    {
        if (_sectorPickRow >= 0)
            FinalizeSectorPick();
        ClearAllSectorReferenceHighlights(vm);
        _sectorLastPickedRow = -1;
        _sectorLastPickedCol = -1;
    }

    public void FinalizeSectorPick()
    {
        _sectorPickRow = -1;
        _sectorPickCol = -1;
        _sectorPickTokenStart = -1;
        _sectorPickTokenLength = 0;
    }

    public void ClearAllSectorReferenceHighlights(CubicacionViewModel? vm)
    {
        if (vm != null)
        {
            foreach (var rowIndex in _sectorHighlightedRows)
            {
                if (rowIndex >= 0 && rowIndex < vm.Rows.Count)
                {
                    vm.Rows[rowIndex].IsFormulaPickTarget = false;
                    vm.Rows[rowIndex].ClearFormulaPickTargetOffsets();
                }
            }
        }
        _sectorHighlightedRows.Clear();
    }

    public static bool EndsWithFormulaOperator(TextBox textBox)
    {
        int idx = textBox.CaretIndex;
        if (idx <= 0 || idx > textBox.Text.Length) return false;
        return textBox.Text[idx - 1] is '+' or '-' or '*' or '/' or '=' or '(';
    }

    public static bool CanStartSectorPick(TextBox textBox)
        => EndsWithFormulaOperator(textBox)
           || (textBox.CaretIndex == textBox.Text.Length && SectorCellReferenceRegex.IsMatch(textBox.Text));

    public void HandleSectorPick(
        Key key,
        TextBox textBox,
        CubicacionViewModel vm,
        DataGrid resourceGrid,
        IList<DataGridColumn> sectorColumns)
    {
        if (vm.Rows.Count == 0) return;

        if (_sectorPickRow < 0)
        {
            if (!EndsWithFormulaOperator(textBox) && textBox.CaretIndex == textBox.Text.Length)
            {
                int insertAt = textBox.Text.Length;
                textBox.Text = textBox.Text.Insert(insertAt, "+");
                textBox.CaretIndex = insertAt + 1;
            }

            if (_sectorLastPickedRow >= 0)
            {
                _sectorPickRow = _sectorLastPickedRow;
                _sectorPickCol = _sectorLastPickedCol;
            }
            else
            {
                var currentRow = resourceGrid.CurrentCell.Item as CubicacionRowVm;
                _sectorPickRow = currentRow != null ? vm.Rows.IndexOf(currentRow) : 0;
                if (_sectorPickRow < 0) _sectorPickRow = 0;
                int currentSlot = sectorColumns.IndexOf(resourceGrid.CurrentCell.Column);
                _sectorPickCol = currentSlot >= 0 ? currentSlot : 0;
            }
        }

        int rangeEnd = sectorColumns.Count - 1;

        switch (key)
        {
            case Key.Up: _sectorPickRow = Math.Max(0, _sectorPickRow - 1); break;
            case Key.Down: _sectorPickRow = Math.Min(vm.Rows.Count - 1, _sectorPickRow + 1); break;
            case Key.Left: _sectorPickCol = Math.Max(0, _sectorPickCol - 1); break;
            case Key.Right: _sectorPickCol = Math.Min(rangeEnd, _sectorPickCol + 1); break;
        }

        ApplySectorPick(textBox, _sectorPickRow, _sectorPickCol, vm);
    }

    public void ApplySectorPick(TextBox textBox, int rowIndex, int colOffset, CubicacionViewModel vm)
    {
        _sectorLastPickedRow = rowIndex;
        _sectorLastPickedCol = colOffset;

        string token = $"F{rowIndex + 1}C{colOffset + 1}";
        bool firstPick = _sectorPickTokenStart < 0;

        if (firstPick)
        {
            if (textBox.SelectionLength > 0)
            {
                _sectorPickTokenStart = textBox.SelectionStart;
                _sectorPickTokenLength = textBox.SelectionLength;
            }
            else
            {
                _sectorPickTokenStart = Math.Clamp(textBox.CaretIndex, 0, textBox.Text.Length);
                _sectorPickTokenLength = 0;
            }
        }

        string text = textBox.Text;
        int removeLength = Math.Min(_sectorPickTokenLength, Math.Max(0, text.Length - _sectorPickTokenStart));
        text = text.Remove(_sectorPickTokenStart, removeLength).Insert(_sectorPickTokenStart, token);
        textBox.Text = text;
        _sectorPickTokenLength = token.Length;
        textBox.CaretIndex = _sectorPickTokenStart + _sectorPickTokenLength;

        RefreshSectorReferenceHighlights(textBox, vm);
        textBox.Focus();
    }

    public void RefreshSectorReferenceHighlights(TextBox textBox, CubicacionViewModel vm)
    {
        var referencedByRow = new Dictionary<int, HashSet<int>>();
        foreach (Match m in SectorCellReferenceRegex.Matches(textBox.Text))
        {
            if (!int.TryParse(m.Groups[1].Value, out int rowNumber)) continue;
            if (!int.TryParse(m.Groups[2].Value, out int colNumber)) continue;
            int rowIndex = rowNumber - 1;
            if (rowIndex < 0 || rowIndex >= vm.Rows.Count) continue;
            if (!referencedByRow.TryGetValue(rowIndex, out var offsets))
                referencedByRow[rowIndex] = offsets = new HashSet<int>();
            offsets.Add(colNumber - 1);
        }

        foreach (var rowIndex in _sectorHighlightedRows)
        {
            if (referencedByRow.ContainsKey(rowIndex)) continue;
            if (rowIndex >= 0 && rowIndex < vm.Rows.Count)
            {
                vm.Rows[rowIndex].IsFormulaPickTarget = false;
                vm.Rows[rowIndex].ClearFormulaPickTargetOffsets();
            }
        }

        foreach (var (rowIndex, offsets) in referencedByRow)
        {
            var row = vm.Rows[rowIndex];
            row.IsFormulaPickTarget = true;
            row.ClearFormulaPickTargetOffsets();
            foreach (var offset in offsets)
                row.AddFormulaPickTargetOffset(offset);
        }

        _sectorHighlightedRows.Clear();
        foreach (var rowIndex in referencedByRow.Keys)
            _sectorHighlightedRows.Add(rowIndex);
    }
}
