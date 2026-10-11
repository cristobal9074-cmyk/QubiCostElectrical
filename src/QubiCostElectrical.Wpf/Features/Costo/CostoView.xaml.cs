using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Costo;

public partial class CostoView : UserControl
{
    private bool coefEditing;
    private bool coefReplaceNext;
    private bool coefEnteringEdit;
    private string coefOriginal = "";
    private List<(TextBox Mult, TextBox Disc)>? coefRows;
    private bool goEditing;
    private bool goReplaceNext;
    private bool goEnteringEdit;
    private string goOriginal = "";

    public CostoView() => InitializeComponent();

    private void OnCoefTablePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox)
            return;
        if (e.Key is not (Key.Down or Key.Enter or Key.Right or Key.F2))
            return;
        if (Keyboard.Modifiers != ModifierKeys.None)
            return;

        var rows = CoefRows();
        if (rows.Count == 0 || !rows[0].Mult.IsEnabled)
            return;
        rows[0].Mult.Focus();
        e.Handled = true;
    }

    private void OnGoCellGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        if (goEnteringEdit)
            return;
        goEditing = false;
        goReplaceNext = true;
        goOriginal = box.Text ?? "";
        box.SelectAll();
    }

    private void OnGoCellLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        goEditing = false;
        goReplaceNext = false;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void OnGoCellPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled)
            return;
        if (e.ClickCount >= 2)
        {
            goEnteringEdit = true;
            goEditing = true;
            goReplaceNext = false;
            box.Focus();
            box.SelectionLength = 0;
            box.CaretIndex = box.Text?.Length ?? 0;
            goEnteringEdit = false;
            e.Handled = true;
            return;
        }

        goEditing = false;
        goReplaceNext = true;
        if (!box.IsKeyboardFocused)
            box.Focus();
        else
            goOriginal = box.Text ?? "";
        box.SelectAll();
        e.Handled = true;
    }

    private void OnGoCellPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled || string.IsNullOrEmpty(e.Text))
            return;
        if (!AllowGoChar(box, e.Text[0]))
        {
            e.Handled = true;
            return;
        }

        var current = box.Text ?? "";
        if (goReplaceNext || !goEditing || box.SelectionLength == current.Length)
        {
            goReplaceNext = false;
            goEditing = true;
            box.Text = e.Text;
            box.CaretIndex = box.Text.Length;
            box.SelectionLength = 0;
            e.Handled = true;
            return;
        }

        int start = box.SelectionStart;
        int len = box.SelectionLength;
        if (start < 0 || start > current.Length)
            start = current.Length;
        if (start + len > current.Length)
            len = current.Length - start;
        box.Text = current.Remove(start, len).Insert(start, e.Text);
        box.CaretIndex = start + e.Text.Length;
        box.SelectionLength = 0;
        e.Handled = true;
    }

    private void OnGoCellPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled)
            return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            return;

        if (e.Key == Key.F2)
        {
            goEditing = true;
            goReplaceNext = false;
            box.SelectionLength = 0;
            box.CaretIndex = box.Text?.Length ?? 0;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            goEditing = false;
            goReplaceNext = true;
            box.Text = goOriginal;
            box.SelectAll();
            e.Handled = true;
            return;
        }

        if (goReplaceNext && e.Key is Key.Back or Key.Delete)
        {
            goReplaceNext = false;
            goEditing = true;
            box.Text = "";
            e.Handled = true;
            return;
        }

        if (goEditing && e.Key is Key.Left or Key.Right)
            return;

        int colDelta = 0;
        int rowDelta = 0;
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        switch (e.Key)
        {
            case Key.Up:
                rowDelta = -1;
                break;
            case Key.Down:
                rowDelta = 1;
                break;
            case Key.Enter:
                rowDelta = shift ? -1 : 1;
                break;
            case Key.Left:
            case Key.Tab when shift:
                colDelta = -1;
                break;
            case Key.Right:
            case Key.Tab:
                colDelta = 1;
                break;
            default:
                return;
        }

        MoveGoCell(box, colDelta, rowDelta);
        e.Handled = true;
    }

    private static bool AllowGoChar(TextBox box, char ch)
    {
        var tag = box.Tag as string;
        if (tag is "gc" or "gu")
            return true;
        return char.IsDigit(ch) || ch is ',' or '.';
    }

    private void MoveGoCell(TextBox from, int colDelta, int rowDelta)
    {
        var rows = GoSheetRows(from);
        int row = -1;
        int col = -1;
        for (int i = 0; i < rows.Count; i++)
        {
            for (int c = 0; c < rows[i].Length; c++)
            {
                if (!ReferenceEquals(rows[i][c], from))
                    continue;
                row = i;
                col = c;
                break;
            }

            if (row >= 0)
                break;
        }

        if (row < 0 || col < 0)
            return;

        int nextCol = col;
        int nextRow = row;
        if (colDelta != 0)
        {
            nextCol = col + colDelta;
            if (nextCol >= rows[row].Length)
            {
                nextCol = 0;
                nextRow = row + 1;
            }
            else if (nextCol < 0)
            {
                nextCol = rows[row].Length - 1;
                nextRow = row - 1;
            }
        }
        else
        {
            nextRow = row + rowDelta;
        }

        if (nextRow < 0 || nextRow >= rows.Count)
            return;
        nextCol = Math.Clamp(nextCol, 0, rows[nextRow].Length - 1);
        var target = rows[nextRow][nextCol];
        if (!target.IsEnabled || ReferenceEquals(target, from))
            return;
        target.Focus();
        target.BringIntoView();
    }

    private static List<TextBox[]> GoSheetRows(TextBox from)
    {
        var host = FindVisualParent<ItemsControl>(from);
        var rows = new List<TextBox[]>();
        if (host == null)
            return rows;
        foreach (var box in FindVisualChildren<TextBox>(host))
        {
            if ((box.Tag as string) is not ("gc" or "gu" or "gn" or "gp"))
                continue;
            if (rows.Any(r => r.Contains(box)))
                continue;
            if (box.Parent is not Grid grid)
                continue;
            var line = grid.Children.OfType<TextBox>()
                .Where(b => (b.Tag as string) is "gc" or "gu" or "gn" or "gp")
                .OrderBy(Grid.GetColumn)
                .ToArray();
            if (line.Length > 0)
                rows.Add(line);
        }

        return rows;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T match)
                return match;
        }

        return null;
    }

    private void OnCoefCellGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        SetCoefSuspend(box, true);
        if (coefEnteringEdit)
            return;
        coefEditing = false;
        coefReplaceNext = true;
        coefOriginal = box.Text ?? "";
        box.SelectAll();
    }

    private void OnCoefCellLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box)
            return;
        coefEditing = false;
        coefReplaceNext = false;
        SetCoefSuspend(box, false);
        CommitCoefBox(box);
    }

    private void OnCoefCellPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled)
            return;

        if (e.ClickCount >= 2)
        {
            coefEnteringEdit = true;
            coefEditing = true;
            coefReplaceNext = false;
            box.Focus();
            box.SelectionLength = 0;
            box.CaretIndex = box.Text.Length;
            coefEnteringEdit = false;
            e.Handled = true;
            return;
        }

        coefEditing = false;
        coefReplaceNext = true;
        if (!box.IsKeyboardFocused)
            box.Focus();
        else
        {
            coefOriginal = box.Text ?? "";
            SetCoefSuspend(box, true);
        }
        box.SelectAll();
        e.Handled = true;
    }

    private void OnCoefCellPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled || string.IsNullOrEmpty(e.Text))
            return;
        var ch = e.Text[0] == '.' ? ',' : e.Text[0];
        if (ch == ' ' || !AllowCoefChar(box, ch))
        {
            e.Handled = true;
            return;
        }

        var typed = ch.ToString();
        var current = box.Text ?? "";
        if (coefReplaceNext || !coefEditing || box.SelectionLength == current.Length)
        {
            coefReplaceNext = false;
            coefEditing = true;
            box.Text = typed;
            box.CaretIndex = box.Text.Length;
            box.SelectionLength = 0;
            e.Handled = true;
            return;
        }

        int start = box.SelectionStart;
        int len = box.SelectionLength;
        if (start < 0 || start > current.Length)
            start = current.Length;
        if (start + len > current.Length)
            len = current.Length - start;
        box.Text = current.Remove(start, len).Insert(start, typed);
        box.CaretIndex = start + typed.Length;
        box.SelectionLength = 0;
        e.Handled = true;
    }

    private void OnCoefCellPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled)
            return;

        var mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control) && e.Key is Key.Up or Key.Down)
        {
            NudgeCoefCell(box, e.Key == Key.Up ? 1 : -1);
            coefEditing = false;
            coefReplaceNext = true;
            box.SelectAll();
            e.Handled = true;
            return;
        }

        if (mods.HasFlag(ModifierKeys.Control) || mods.HasFlag(ModifierKeys.Alt))
            return;

        if (e.Key == Key.F2)
        {
            coefEditing = true;
            coefReplaceNext = false;
            box.SelectionLength = 0;
            box.CaretIndex = box.Text.Length;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            coefEditing = false;
            coefReplaceNext = true;
            box.Text = coefOriginal;
            box.SelectAll();
            e.Handled = true;
            return;
        }

        if (coefReplaceNext && e.Key is Key.Back or Key.Delete)
        {
            coefReplaceNext = false;
            coefEditing = true;
            box.Text = "";
            e.Handled = true;
            return;
        }

        if (coefEditing && e.Key is Key.Left or Key.Right)
            return;

        int colDelta = 0;
        int rowDelta = 0;
        bool shift = mods.HasFlag(ModifierKeys.Shift);
        switch (e.Key)
        {
            case Key.Up:
                rowDelta = -1;
                break;
            case Key.Down:
                rowDelta = 1;
                break;
            case Key.Enter:
                rowDelta = shift ? -1 : 1;
                break;
            case Key.Left:
            case Key.Tab when shift:
                colDelta = -1;
                break;
            case Key.Right:
            case Key.Tab:
                colDelta = 1;
                break;
            default:
                return;
        }

        MoveCoefCell(box, colDelta, rowDelta);
        e.Handled = true;
    }

    private bool AllowCoefChar(TextBox box, char ch)
    {
        if ((box.Tag as string) == "d")
            return char.IsDigit(ch);

        if (ch == '.')
            ch = ',';
        if (!char.IsDigit(ch) && ch != ',')
            return false;

        var current = box.Text ?? "";
        bool replaceAll = coefReplaceNext || !coefEditing || box.SelectionLength == current.Length;
        int start = replaceAll ? 0 : box.SelectionStart;
        int len = replaceAll ? current.Length : box.SelectionLength;
        if (start < 0 || start > current.Length)
            start = current.Length;
        if (start + len > current.Length)
            len = current.Length - start;
        var next = (replaceAll ? "" : current.Remove(start, len)).Insert(replaceAll ? 0 : start, ch.ToString());
        next = next.Replace('.', ',');
        if (next.Count(c => c == ',') > 1)
            return false;

        var sep = next.IndexOf(',');
        if (sep < 0)
            return next.All(char.IsDigit);
        var whole = next[..sep];
        var frac = next[(sep + 1)..];
        return frac.Length <= 2
            && (whole.Length == 0 || whole.All(char.IsDigit))
            && frac.All(char.IsDigit);
    }

    private static void NudgeCoefCell(TextBox box, int direction)
    {
        if (box.DataContext is not CoefFactorVm factor)
            return;
        SetCoefSuspend(box, false);
        CommitCoefBox(box);
        if ((box.Tag as string) == "d")
            factor.NudgeDiscount(direction);
        else
            factor.NudgeValue(direction);
        SetCoefSuspend(box, true);
    }

    private static void SetCoefSuspend(TextBox box, bool suspend)
    {
        if (box.DataContext is CoefFactorVm factor)
            factor.SuspendPush = suspend;
    }

    private static void CommitCoefBox(TextBox box)
        => box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

    private void MoveCoefCell(TextBox from, int colDelta, int rowDelta)
    {
        var rows = CoefRows();
        int row = -1;
        int col = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i].Mult, from))
            {
                row = i;
                col = 0;
                break;
            }

            if (ReferenceEquals(rows[i].Disc, from))
            {
                row = i;
                col = 1;
                break;
            }
        }

        if (row < 0)
            return;

        int nextCol = col;
        int nextRow = row;
        if (colDelta != 0)
        {
            nextCol = col + colDelta;
            if (nextCol > 1)
            {
                nextCol = 0;
                nextRow = row + 1;
            }
            else if (nextCol < 0)
            {
                nextCol = 1;
                nextRow = row - 1;
            }
        }
        else
        {
            nextRow = row + rowDelta;
        }

        if (nextRow < 0 || nextRow >= rows.Count)
            return;

        var target = nextCol == 0 ? rows[nextRow].Mult : rows[nextRow].Disc;
        if (!target.IsEnabled || ReferenceEquals(target, from))
            return;

        target.Focus();
    }

    private List<(TextBox Mult, TextBox Disc)> CoefRows()
    {
        if (coefRows is { Count: > 0 } && coefRows[0].Mult.IsLoaded)
            return coefRows;

        coefRows = new List<(TextBox Mult, TextBox Disc)>();
        TextBox? pending = null;
        foreach (var box in FindVisualChildren<TextBox>(CoefFactorsList))
        {
            switch (box.Tag as string)
            {
                case "m":
                    pending = box;
                    break;
                case "d" when pending != null:
                    coefRows.Add((pending, box));
                    pending = null;
                    break;
            }
        }

        return coefRows;
    }

    private Point precioDragStart;
    private bool precioDragArmed;

    private void OnPrecioFamiliaMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PrecioFamiliaChipVm })
            return;
        precioDragArmed = true;
        precioDragStart = e.GetPosition(null);
    }

    private void OnPrecioFamiliaMouseMove(object sender, MouseEventArgs e)
    {
        if (!precioDragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - precioDragStart.X) < 4 && Math.Abs(pos.Y - precioDragStart.Y) < 4)
            return;
        precioDragArmed = false;
        if (sender is not FrameworkElement { DataContext: PrecioFamiliaChipVm chip })
            return;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(PrecioFamiliaChipVm), chip), DragDropEffects.Move);
    }

    private void OnCoefRowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(PrecioFamiliaChipVm)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCoefRowDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not CostoViewModel vm)
            return;
        if (e.Data.GetData(typeof(PrecioFamiliaChipVm)) is not PrecioFamiliaChipVm chip)
            return;
        string? target = sender is FrameworkElement { DataContext: CoefFactorVm row } ? row.FamilyKey : null;
        vm.MovePrecioFamilia(chip, target);
        e.Handled = true;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;
            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }
}
