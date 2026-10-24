using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

internal static class HojaCubicacionGridHelpers
{
    public static DataGridCellInfo? HitTestCell(DataGrid grid, Point point)
    {
        var hit = VisualTreeHelper.HitTest(grid, point)?.VisualHit;
        if (hit == null) return null;

        var cell = hit as DataGridCell ?? FindAncestor<DataGridCell>(hit);
        if (cell?.Column == null) return null;

        var row = FindAncestor<DataGridRow>(cell);
        if (row == null) return null;

        return new DataGridCellInfo(row.Item, cell.Column);
    }

    public static void SelectCellRange(DataGrid grid, DataGridCellInfo anchor, DataGridCellInfo current)
    {
        var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
        int anchorRow = grid.Items.IndexOf(anchor.Item);
        int currentRow = grid.Items.IndexOf(current.Item);
        int anchorCol = columns.IndexOf(anchor.Column);
        int currentCol = columns.IndexOf(current.Column);
        if (anchorRow < 0 || currentRow < 0 || anchorCol < 0 || currentCol < 0) return;

        int rowMin = Math.Min(anchorRow, currentRow);
        int rowMax = Math.Max(anchorRow, currentRow);
        int colMin = Math.Min(anchorCol, currentCol);
        int colMax = Math.Max(anchorCol, currentCol);

        grid.SelectedCells.Clear();
        for (int r = rowMin; r <= rowMax; r++)
        {
            var item = grid.Items[r];
            for (int c = colMin; c <= colMax; c++)
                grid.SelectedCells.Add(new DataGridCellInfo(item, columns[c]));
        }

        grid.CurrentCell = current;
    }

    public static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        return QubiCostElectrical.Wpf.Infrastructure.VisualTreeHelperSafe.FindAncestor<T>(child);
    }

    public static T? FindDescendantByName<T>(DependencyObject root, string? name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && (name == null || match.Name == name))
                return match;
            var found = FindDescendantByName<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public static void UpdateFormulaPreview(TextBlock? previewBlock, string raw)
    {
        if (previewBlock == null) return;
        string text = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(text))
        {
            previewBlock.Text = "";
            return;
        }

        try
        {
            var singleRow = new List<string> { text };
            var eval = HojaCubicacionFormulaEngine.EvaluateRow(singleRow);
            if (eval.Cells.Count > 0 && eval.Cells[0].Success)
            {
                previewBlock.Text = $"= {eval.Cells[0].DisplayText}";
                previewBlock.Foreground = (Brush)Application.Current.FindResource("Brush.Green");
            }
            else if (eval.Cells.Count > 0)
            {
                previewBlock.Text = eval.Cells[0].DisplayText;
                previewBlock.Foreground = (Brush)Application.Current.FindResource("Brush.Red");
            }
        }
        catch
        {
            previewBlock.Text = "¡ERROR!";
            previewBlock.Foreground = (Brush)Application.Current.FindResource("Brush.Red");
        }
    }

    public static bool IsFormulaOpenForCellReference(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        string t = text.Trim();
        if (!t.StartsWith("=", StringComparison.Ordinal))
        {
            if (char.IsLetter(t[0]))
            {
                char lastCh = t[^1];
                return lastCh is '+' or '-' or '*' or '/' or '^' or '(' or ',' or ';' or ':';
            }
            return false;
        }
        if (t.Length == 1) return true;
        char last = t[^1];
        return last is '=' or '+' or '-' or '*' or '/' or '^' or '%' or '(' or ',' or ';' or ':';
    }

    public static void ShowFxMenu(Button btn, TextBox inputBox)
    {
        var menu = new ContextMenu();
        void Add(string header, string template, int cursorOffset)
        {
            var mi = new MenuItem { Header = header };
            mi.Click += (_, _) =>
            {
                inputBox.Focus();
                inputBox.Text = template;
                inputBox.CaretIndex = cursorOffset;
            };
            menu.Items.Add(mi);
        }

        Add("SUMA(rango) — Sumar serie o rango continuo", "=SUMA(C:E)", 6);
        Add("PROMEDIO(rango) — Promedio de celdas", "=PROMEDIO(C:E)", 10);
        Add("MIN(rango) — Valor mínimo", "=MIN(C:E)", 5);
        Add("MAX(rango) — Valor máximo", "=MAX(C:E)", 5);
        Add("REDONDEAR(celda; dec) — Redondeo con decimales", "=REDONDEAR(C; 2)", 11);
        Add("PORCENTAJE (%) — ej. C*15%", "* 15%", 0);
        Add("CONTAR(rango) — Contar celdas con datos", "=CONTAR(C:E)", 8);

        menu.PlacementTarget = btn;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }
}

internal sealed class FormulaPointModeState
{
    public DataGrid Grid { get; set; } = null!;
    public HojaCubicacionItemVm Item { get; set; } = null!;
    public TextBox Editor { get; set; } = null!;
    public int EditRowIndex { get; set; }
    public int EditColIndex { get; set; }
    public int TargetRowIndex { get; set; }
    public int TargetColIndex { get; set; }
    public int PrefixLength { get; set; }
    public bool IsActive { get; set; }
    public bool IsFormulaBar { get; set; }
}
