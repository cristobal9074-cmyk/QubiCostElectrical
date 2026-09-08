using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public partial class ManoDeObraView : UserControl
{
    public ManoDeObraView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ManoDeObraViewModel vm)
        {
            vm.Load();
            BuildTables(vm);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ManoDeObraViewModel oldVm)
            oldVm.PropertyChanged -= OnVmPropertyChanged;
        if (e.NewValue is ManoDeObraViewModel newVm)
        {
            newVm.PropertyChanged -= OnVmPropertyChanged;
            newVm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ManoDeObraViewModel vm)
            return;

        if (e.PropertyName is nameof(ManoDeObraViewModel.TotalMeses)
            or nameof(ManoDeObraViewModel.CargosDef)
            or nameof(ManoDeObraViewModel.ViewInUf)
            or nameof(ManoDeObraViewModel.LeyesSocialesPct)
            or nameof(ManoDeObraViewModel.CurvePreset)
            or nameof(ManoDeObraViewModel.PeakHeadcount)
            or nameof(ManoDeObraViewModel.DotacionRows)
            or nameof(ManoDeObraViewModel.CostRows))
        {
            BuildTables(vm);
        }
    }

    private void OnSalaryLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: MoCargoEditorVm editor })
            editor.CommitEdit();
    }

    private void OnParamKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox tb)
            return;
        tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void BuildTables(ManoDeObraViewModel vm)
    {
        CargoTableGrid.Children.Clear();
        CargoTableGrid.RowDefinitions.Clear();
        CargoTableGrid.ColumnDefinitions.Clear();
        CostTableGrid.Children.Clear();
        CostTableGrid.RowDefinitions.Clear();
        CostTableGrid.ColumnDefinitions.Clear();
        BuildCargoTable(vm);
        BuildCostTable(vm);
    }

    private void BuildCargoTable(ManoDeObraViewModel vm)
    {
        var rows = vm.DotacionRows;
        var n = vm.TotalMonths;
        var grid = CargoTableGrid;
        var peakSet = vm.Meses.Where(m => m.EsPeak).Select(m => m.Numero).ToHashSet();

        AddCol(grid, ColCargo);
        AddCol(grid, ColDotPico);
        AddCol(grid, ColBase);
        AddCol(grid, ColLeyes);
        AddCol(grid, ColTotalMes);
        for (var i = 0; i < n; i++) AddCol(grid, ColMonth);
        AddCol(grid, ColFinal);

        var r = 0;
        AddHeaderRow(grid, r++, n, peakSet);
        foreach (var row in rows)
            AddCargoDataRow(grid, r++, row, n, vm, peakSet);
    }

    private void AddHeaderRow(Grid grid, int r, int n, System.Collections.Generic.HashSet<int> peakSet)
    {
        AddRowDef(grid, RowH);
        var c = Muted;
        var b = Surface2;
        AddCell(grid, r, 0, "Cargo", bold: true, color: c, bg: b);
        AddCell(grid, r, 1, "Dot.", bold: true, TextAlignment.Right, color: c, bg: b);
        AddCell(grid, r, 2, "Sueldo base", bold: true, TextAlignment.Right, color: Blue, bg: BlueFaint);
        AddCell(grid, r, 3, "Leyes", bold: true, TextAlignment.Right, color: Purple, bg: PurpleSoft);
        AddCell(grid, r, 4, "Total mens.", bold: true, TextAlignment.Right, color: Blue, bg: BlueFaint);
        for (var i = 0; i < n; i++)
        {
            var peak = peakSet.Contains(i + 1);
            AddCell(grid, r, 5 + i, $"M{i + 1}", bold: true, TextAlignment.Center,
                color: peak ? Green : c, bg: peak ? Color.FromRgb(0xF0, 0xFA, 0xF4) : b);
        }
        AddCell(grid, r, 5 + n, "Total", bold: true, TextAlignment.Right, color: c, bg: b);
    }

    private void AddCargoDataRow(Grid grid, int r, MoTableRow row, int n, ManoDeObraViewModel vm, System.Collections.Generic.HashSet<int> peakSet)
    {
        AddRowDef(grid, RowH);

        if (row.IsGroupHeader)
        {
            var bg = row.GroupBadgeType == "dir" ? Color.FromRgb(0xF4, 0xFB, 0xF7) : Color.FromRgb(0xFD, 0xF8, 0xEE);
            var tb = new TextBlock
            {
                Text = row.GroupLabel.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Ink),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            var tagBg = row.GroupBadgeType == "dir" ? Green : Amber;
            var tagTb = new TextBlock
            {
                Text = row.GroupBadge,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(tagBg),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(tb);
            panel.Children.Add(tagTb);
            if (row.GroupBadgeType == "ind")
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"  Misma dotación todos los meses ({n}/{n})",
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Muted),
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            var rem = new Border
            {
                Child = panel,
                Background = new SolidColorBrush(bg),
                Height = RowH,
                BorderBrush = new SolidColorBrush(Line),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            Grid.SetRow(rem, r);
            Grid.SetColumn(rem, 0);
            Grid.SetColumnSpan(rem, 6 + n);
            grid.Children.Add(rem);
            return;
        }

        var isDir = row.EsDirecta;
        var isTotal = row.IsSubtotal || row.IsGrandTotal;

        var tagBorder = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Background = new SolidColorBrush(isTotal ? Muted : (isDir ? Green : Amber)),
            Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(row.Tag) ? (isTotal ? "Σ" : "") : row.Tag,
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
            },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Visibility = string.IsNullOrEmpty(row.Tag) && !isTotal ? Visibility.Collapsed : Visibility.Visible,
        };
        var namePanel = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(row.Tag) || isTotal)
            namePanel.Children.Add(tagBorder);
        namePanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(row.Nombre) ? (isTotal ? (isDir ? "Subtotal" : "Subtotal") : "") : row.Nombre,
            FontSize = CellSz,
            FontWeight = isTotal ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = new SolidColorBrush(isDir && !isTotal ? Green : Ink),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var nameBorder = new Border
        {
            Child = namePanel,
            Height = RowH,
            VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = new SolidColorBrush(Line2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = isTotal ? new SolidColorBrush(row.IsGrandTotal ? Navy2 : Surface2) : null,
            Padding = new Thickness(6, 0, 0, 0),
        };
        if (row.IsGrandTotal)
            ((TextBlock)namePanel.Children[^1]).Foreground = Brushes.White;
        Grid.SetRow(nameBorder, r);
        Grid.SetColumn(nameBorder, 0);
        grid.Children.Add(nameBorder);

        var noColor = (Color?)null;
        AddCell(grid, r, 1, row.DotPico.ToString(), bold: isDir || isTotal, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Colors.White : (isDir ? noColor : Muted),
            bg: row.IsGrandTotal ? Navy2 : (isTotal ? Surface2 : null), h: RowH);
        AddCell(grid, r, 2, row.SueldoBaseStr, bold: isDir || isTotal, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Colors.White : noColor,
            bg: row.IsGrandTotal ? Navy2 : BlueFaint, h: RowH);
        AddCell(grid, r, 3, row.LeyesStr, bold: isDir || isTotal, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Color.FromRgb(0x93, 0xC5, 0xFD) : Purple,
            bg: row.IsGrandTotal ? Navy2 : null, h: RowH);
        AddCell(grid, r, 4, row.TotalMensualStr, bold: true, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Colors.White : (isDir ? Blue : Ink),
            bg: row.IsGrandTotal ? Navy2 : BlueFaint, h: RowH);

        var maxDot = row.Dotaciones.Length > 0 ? Math.Max(1, row.Dotaciones.Max()) : 1;

        if (isTotal)
        {
            for (var i = 0; i < n; i++)
            {
                AddCell(grid, r, 5 + i, row.Dotaciones[i].ToString(), bold: true, TextAlignment.Center, mono: true,
                    color: row.IsGrandTotal ? Colors.White : noColor,
                    bg: row.IsGrandTotal ? Navy2 : (peakSet.Contains(i + 1) ? PeakBg : Surface2), h: RowH);
            }
        }
        else
        {
            for (var i = 0; i < n; i++)
                AddHeatCell(grid, r, 5 + i, row.Dotaciones[i], maxDot, row.Tag, i, vm, peakSet.Contains(i + 1), !row.EsDirecta);
        }

        var totalColor = row.IsGrandTotal ? Colors.White : (isDir ? Green2 : Amber);
        AddCell(grid, r, 5 + n, row.TotalStr, bold: true, TextAlignment.Right, mono: true,
            color: totalColor, bg: row.IsGrandTotal ? Navy2 : (isTotal ? Surface2 : null), h: RowH);
    }

    private void BuildCostTable(ManoDeObraViewModel vm)
    {
        var rows = vm.CostRows;
        var n = vm.TotalMonths;
        var grid = CostTableGrid;
        var peakSet = vm.Meses.Where(m => m.EsPeak).Select(m => m.Numero).ToHashSet();

        AddCol(grid, 180);
        AddCol(grid, ColBase);
        AddCol(grid, ColLeyes);
        AddCol(grid, ColTotalMes);
        for (var i = 0; i < n; i++) AddCol(grid, Math.Max(ColMonth, 52));
        AddCol(grid, ColFinal);

        var r = 0;
        AddRowDef(grid, RowH);
        var c = Muted;
        var hbg = Surface2;
        AddCell(grid, r, 0, "Concepto", bold: true, color: c, bg: hbg);
        AddCell(grid, r, 1, "Base / mes", bold: true, TextAlignment.Right, color: Blue, bg: BlueFaint);
        AddCell(grid, r, 2, "Leyes / mes", bold: true, TextAlignment.Right, color: Purple, bg: PurpleSoft);
        AddCell(grid, r, 3, "Total / mes", bold: true, TextAlignment.Right, color: Blue, bg: BlueFaint);
        for (var i = 0; i < n; i++)
        {
            var peak = peakSet.Contains(i + 1);
            AddCell(grid, r, 4 + i, $"M{i + 1}", bold: true, TextAlignment.Center,
                color: peak ? Green : c, bg: peak ? Color.FromRgb(0xF0, 0xFA, 0xF4) : hbg);
        }
        AddCell(grid, r, 4 + n, "Total", bold: true, TextAlignment.Right, color: c, bg: hbg);
        r++;

        foreach (var row in rows)
            AddCostDataRow(grid, r++, row, n, peakSet);
    }

    private void AddCostDataRow(Grid grid, int r, MoCostRow row, int n, System.Collections.Generic.HashSet<int> peakSet)
    {
        AddRowDef(grid, RowH);
        var isTotal = row.IsGrandTotal;
        var isSub = row.IsSubtotal;
        var isDir = row.Tag == "dir";
        var cargoColor = isDir ? Green2 : Amber;
        Color? rowBg = isTotal ? Navy2 : (isSub ? Surface2 : null);
        Color? textColor = isTotal ? Colors.White : (isDir || row.Tag == "ind" ? cargoColor : null);

        AddCell(grid, r, 0, row.Label, bold: isDir || isTotal || isSub, color: textColor, bg: rowBg);
        AddCell(grid, r, 1, row.BaseStr, bold: isDir || isTotal || isSub, TextAlignment.Right, mono: true,
            color: isTotal ? Colors.White : null, bg: isTotal ? Navy2 : BlueFaint);
        AddCell(grid, r, 2, row.LeyesStr, bold: isDir || isTotal || isSub, TextAlignment.Right, mono: true,
            color: isTotal ? Color.FromRgb(0x93, 0xC5, 0xFD) : Purple, bg: rowBg);
        AddCell(grid, r, 3, row.TotalStr, bold: true, TextAlignment.Right, mono: true,
            color: isTotal ? Colors.White : Blue, bg: isTotal ? Navy2 : BlueFaint);

        for (var i = 0; i < n; i++)
        {
            AddCell(grid, r, 4 + i, row.CostosStr[i], bold: isDir || isTotal, TextAlignment.Center, mono: true,
                color: isTotal ? Colors.White : (peakSet.Contains(i + 1) ? Green : null),
                bg: isTotal ? Navy2 : (peakSet.Contains(i + 1) ? PeakBg : rowBg),
                fontSize: 9);
        }

        AddCell(grid, r, 4 + n, row.TotalFinalStr, bold: true, TextAlignment.Right, mono: true,
            color: isTotal ? Colors.White : cargoColor, bg: rowBg);
    }

    private void AddHeatCell(Grid grid, int rowIdx, int colIdx, int value, int max, string cargoId, int monthIdx,
        ManoDeObraViewModel vm, bool isPeakCol, bool isFixed)
    {
        var (bg, fg) = HeatColors(value, max, isFixed);
        var tb = new TextBlock
        {
            Text = value.ToString(),
            FontFamily = MonoFont,
            FontSize = CellSz,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(fg),
            IsHitTestVisible = false,
        };

        var border = new Border
        {
            Child = tb,
            Height = RowH,
            Background = new SolidColorBrush(isPeakCol && value == 0 ? PeakBg : bg),
            BorderBrush = new SolidColorBrush(Line2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = System.Windows.Input.Cursors.IBeam,
            Margin = new Thickness(2, 3, 2, 3),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2, 0, 2, 0),
            ToolTip = "Clic para editar · Enter confirma · Esc cancela",
        };

        if (vm.HasOverride(cargoId, monthIdx))
        {
            border.BorderBrush = new SolidColorBrush(Amber);
            border.BorderThickness = new Thickness(1.5);
        }

        border.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!vm.CanEdit)
                return;

            // Evita reiniciar el editor si ya está editando (clic dentro del TextBox).
            if (border.Child is TextBox)
                return;

            e.Handled = true;
            BeginCellEdit(border, tb, value, cargoId, monthIdx, vm);
        };

        Grid.SetRow(border, rowIdx);
        Grid.SetColumn(border, colIdx);
        grid.Children.Add(border);
    }

    private static void BeginCellEdit(Border border, TextBlock display, int value, string cargoId, int monthIdx,
        ManoDeObraViewModel vm)
    {
        var textBox = new TextBox
        {
            Text = value.ToString(),
            MinWidth = 36,
            Width = double.NaN,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = MonoFont,
            FontSize = CellSz,
            FontWeight = FontWeights.SemiBold,
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Blue2),
            Padding = new Thickness(2, 0, 2, 0),
        };

        var committed = false;

        void RestoreDisplay()
        {
            if (border.Child == textBox)
                border.Child = display;
        }

        void CommitValue()
        {
            if (committed)
                return;
            committed = true;

            var raw = (textBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(raw))
            {
                // Vacío = quitar override y volver a la curva
                vm.ClearOverride(cargoId, monthIdx);
                return;
            }

            if (int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var newVal)
                || int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.GetCultureInfo("es-CL"), out newVal))
            {
                vm.SetOverride(cargoId, monthIdx, newVal);
            }
            else
            {
                RestoreDisplay();
            }
        }

        void CancelEdit()
        {
            if (committed)
                return;
            committed = true;
            RestoreDisplay();
        }

        border.Child = textBox;
        textBox.Loaded += (_, _) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        textBox.LostFocus += (_, _) => CommitValue();

        textBox.PreviewKeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter)
            {
                ke.Handled = true;
                CommitValue();
                Keyboard.ClearFocus();
            }
            else if (ke.Key == Key.Escape)
            {
                ke.Handled = true;
                CancelEdit();
                Keyboard.ClearFocus();
            }
        };
    }

    private static (Color bg, Color fg) HeatColors(int value, int max, bool fixedRole)
    {
        if (fixedRole)
            return (Color.FromRgb(0xFB, 0xF1, 0xDD), Amber);
        if (value <= 0)
            return (Colors.Transparent, Muted);

        var ratio = (double)value / Math.Max(1, max);
        if (ratio < 0.25) return (Color.FromRgb(0xDB, 0xEA, 0xFE), Color.FromRgb(0x1E, 0x40, 0xAF));
        if (ratio < 0.50) return (Color.FromRgb(0xBF, 0xDB, 0xFE), Color.FromRgb(0x1E, 0x3A, 0x8A));
        if (ratio < 0.75) return (Color.FromRgb(0x93, 0xC5, 0xFD), Color.FromRgb(0x1E, 0x3A, 0x8A));
        if (ratio < 0.95) return (Blue2, Colors.White);
        return (Color.FromRgb(0x1D, 0x4E, 0xD8), Colors.White);
    }

    private static void AddCol(Grid grid, int width)
        => grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });

    private static void AddRowDef(Grid grid, int height)
        => grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(height) });

    private static void AddCell(Grid grid, int row, int col, string text, bool bold = false,
        TextAlignment align = TextAlignment.Left, bool mono = false, Color? color = null,
        Color? bg = null, int h = RowH, int fontSize = CellSz)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = mono ? MonoFont : UiFont,
            FontSize = fontSize,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = new SolidColorBrush(color ?? Ink),
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = align,
            TextTrimming = TextTrimming.None,
            Margin = new Thickness(align == TextAlignment.Right ? 6 : 4, 0, align == TextAlignment.Right ? 4 : 0, 0),
        };

        var border = new Border
        {
            Child = tb,
            Height = h,
            BorderBrush = new SolidColorBrush(Line2),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        if (bg.HasValue)
            border.Background = new SolidColorBrush(bg.Value);

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        grid.Children.Add(border);
    }
}
