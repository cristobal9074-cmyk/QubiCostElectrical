using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public partial class ManoDeObraView : UserControl
{
    private readonly DispatcherTimer heatFlushTimer;
    private readonly Dictionary<(string Cargo, int Month), TextBox> heatCells = new();
    private readonly List<string> heatRowOrder = new();
    private string? heatFocusCargo;
    private int heatFocusMonth = -1;
    private bool heatHadFocus;
    private bool heatSyncing;
    private int heatMonths;

    public ManoDeObraView()
    {
        InitializeComponent();
        heatFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
        heatFlushTimer.Tick += (_, _) =>
        {
            heatFlushTimer.Stop();
            if (DataContext is ManoDeObraViewModel vm)
                vm.FlushStaffingEdits();
        };
        Unloaded += (_, _) =>
        {
            heatFlushTimer.Stop();
            if (DataContext is ManoDeObraViewModel vm)
                vm.FlushStaffingEdits();
        };
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        CargoWheelZone.AddHandler(UIElement.MouseWheelEvent, new MouseWheelEventHandler(ForwardPageWheel));
        CostWheelZone.AddHandler(UIElement.MouseWheelEvent, new MouseWheelEventHandler(ForwardPageWheel));
        ChartWheelZone.AddHandler(UIElement.MouseWheelEvent, new MouseWheelEventHandler(ForwardPageWheel));
    }

    private void ForwardPageWheel(object sender, MouseWheelEventArgs e)
    {
        if (PageScroll is null)
            return;
        e.Handled = true;
        var forward = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
        };
        PageScroll.RaiseEvent(forward);
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
            RememberHeatFocus();
            BuildTables(vm);
            RestoreHeatFocus();
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
        heatCells.Clear();
        heatRowOrder.Clear();
        var rows = vm.DotacionRows;
        var n = vm.TotalMonths;
        heatMonths = n;
        var grid = CargoTableGrid;
        var peakSet = vm.Meses.Where(m => m.EsPeak).Select(m => m.Numero).ToHashSet();

        AddCol(grid, ColCargo);
        AddCol(grid, ColDotPico);
        AddCol(grid, ColBase);
        AddCol(grid, ColFraccion);
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
        AddCell(grid, r, 3, "Fracc.", bold: true, TextAlignment.Center, color: Amber, bg: b);
        AddCell(grid, r, 4, "Leyes", bold: true, TextAlignment.Right, color: Purple, bg: PurpleSoft);
        AddCell(grid, r, 5, "Total mens.", bold: true, TextAlignment.Right, color: Blue, bg: BlueFaint);
        for (var i = 0; i < n; i++)
        {
            var peak = peakSet.Contains(i + 1);
            AddCell(grid, r, 6 + i, $"M{i + 1}", bold: true, TextAlignment.Center,
                color: peak ? Green : c, bg: peak ? Color.FromRgb(0xF0, 0xFA, 0xF4) : b);
        }
        AddCell(grid, r, 6 + n, "Total", bold: true, TextAlignment.Right, color: c, bg: b);
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
            Grid.SetColumnSpan(rem, 7 + n);
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
        if (!isDir && !isTotal)
            AddDotPicoCell(grid, r, row, vm);
        else
            AddCell(grid, r, 1, row.DotPico.ToString(), bold: isDir || isTotal, TextAlignment.Right, mono: true,
                color: row.IsGrandTotal ? Colors.White : (isDir ? noColor : Muted),
                bg: row.IsGrandTotal ? Navy2 : (isTotal ? Surface2 : null), h: RowH);
        AddCell(grid, r, 2, row.SueldoBaseStr, bold: isDir || isTotal, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Colors.White : noColor,
            bg: row.IsGrandTotal ? Navy2 : BlueFaint, h: RowH);
        if (!isDir && !isTotal)
            AddFraccionCell(grid, r, row, vm);
        else
            AddCell(grid, r, 3, row.FraccionStr, bold: false, TextAlignment.Center, mono: true,
                color: row.IsGrandTotal ? Colors.White : (isDir ? noColor : Amber),
                bg: row.IsGrandTotal ? Navy2 : (isTotal ? Surface2 : null), h: RowH);
        AddCell(grid, r, 4, row.LeyesStr, bold: isDir || isTotal, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Color.FromRgb(0x93, 0xC5, 0xFD) : Purple,
            bg: row.IsGrandTotal ? Navy2 : null, h: RowH);
        AddCell(grid, r, 5, row.TotalMensualStr, bold: true, TextAlignment.Right, mono: true,
            color: row.IsGrandTotal ? Colors.White : (isDir ? Blue : Ink),
            bg: row.IsGrandTotal ? Navy2 : BlueFaint, h: RowH);

        var maxDot = row.Dotaciones.Length > 0 ? Math.Max(1, row.Dotaciones.Max()) : 1;

        if (isTotal)
        {
            for (var i = 0; i < n; i++)
            {
                AddCell(grid, r, 6 + i, row.Dotaciones[i].ToString(), bold: true, TextAlignment.Center, mono: true,
                    color: row.IsGrandTotal ? Colors.White : noColor,
                    bg: row.IsGrandTotal ? Navy2 : (peakSet.Contains(i + 1) ? PeakBg : Surface2), h: RowH);
            }
        }
        else
        {
            var cargoId = !string.IsNullOrEmpty(row.CargoId) ? row.CargoId : row.Tag;
            for (var i = 0; i < n; i++)
                AddHeatCell(grid, r, 6 + i, row.Dotaciones[i], maxDot, cargoId, i, vm, peakSet.Contains(i + 1), !row.EsDirecta);
        }

        var totalColor = row.IsGrandTotal ? Colors.White : (isDir ? Green2 : Amber);
        AddCell(grid, r, 6 + n, row.TotalStr, bold: true, TextAlignment.Right, mono: true,
            color: totalColor, bg: row.IsGrandTotal ? Navy2 : (isTotal ? Surface2 : null), h: RowH);
    }

    /// <summary>
    /// Celda editable de la columna Dot. para roles indirectos: muestra la dotación o fracción
    /// (ej: 0,3 o 0.3 o 1) y se confirma al presionar Enter o perder el foco.
    /// </summary>
    private void AddDotPicoCell(Grid grid, int rowIdx, MoTableRow row, ManoDeObraViewModel vm)
    {
        var cargoId = !string.IsNullOrEmpty(row.CargoId) ? row.CargoId : row.Tag;
        var tag = new HeatCellTag
        {
            CargoId = cargoId,
            Month = -1,
            ValueOnFocus = row.DotPico,
        };
        var currentText = !string.IsNullOrEmpty(row.DotacionStr) ? row.DotacionStr : row.DotPico.ToString(CultureInfo.InvariantCulture);
        var box = new TextBox
        {
            Text = currentText,
            FontFamily = MonoFont,
            FontSize = CellSz,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Amber),
            CaretBrush = new SolidColorBrush(Amber),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            IsReadOnly = !vm.CanEdit,
            Tag = tag,
            ToolTip = "Dotación o dedicación del rol indirecto (ej: 0,3 o 0.3 para 30%, 1 para 100%).",
        };

        var border = new Border
        {
            Child = box,
            Height = RowH,
            Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xF1, 0xDD)),
            BorderBrush = new SolidColorBrush(Line2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.IBeam,
            Margin = new Thickness(1, 2, 1, 2),
            CornerRadius = new CornerRadius(4),
        };

        void CommitDotPico()
        {
            if (!vm.CanEdit) return;
            var raw = (box.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                box.Text = currentText;
                return;
            }

            var clean = raw.Replace("%", "").Trim().Replace(',', '.');
            if (!decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
            {
                box.Text = currentText;
                return;
            }

            if (val > 1m && val <= 100m && (raw.Contains('%') || (val >= 5m && !raw.Contains('.') && !raw.Contains(','))))
            {
                val /= 100m;
            }

            if (val <= 1m)
            {
                vm.SetCargoFraccion(tag.CargoId, Math.Clamp(val, 0m, 1m));
            }
            else
            {
                vm.SetIndirectDotPico(tag.CargoId, (int)Math.Round(val));
            }
        }

        box.GotFocus += (_, _) =>
        {
            tag.ValueOnFocus = int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : row.DotPico;
            border.BorderBrush = new SolidColorBrush(Blue2);
            border.BorderThickness = new Thickness(1.5);
            box.SelectAll();
        };
        box.LostFocus += (_, _) =>
        {
            border.BorderBrush = new SolidColorBrush(Line2);
            border.BorderThickness = new Thickness(0, 0, 0, 1);
            CommitDotPico();
        };
        box.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!vm.CanEdit || box.IsKeyboardFocusWithin)
                return;
            box.Focus();
            box.SelectAll();
            e.Handled = true;
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitDotPico();
                box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                box.Text = currentText;
                box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
            else
            {
                OnHeatKeyDown(box, e);
            }
        };
        box.PreviewTextInput += (_, e) =>
        {
            if (e.Text.Length == 0)
            {
                e.Handled = true;
                return;
            }
            var ch = e.Text[0];
            if (!char.IsDigit(ch) && ch != ',' && ch != '.' && ch != '%')
            {
                e.Handled = true;
                return;
            }
            if (box.SelectionLength == box.Text.Length)
            {
                heatSyncing = true;
                box.Clear();
                heatSyncing = false;
            }
        };
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            var pasted = e.DataObject.GetData(DataFormats.Text) as string;
            if (pasted != null)
            {
                var clean = pasted.Trim().Replace(',', '.');
                if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) && parsed >= 0)
                    return;
            }
            e.CancelCommand();
        });

        heatCells[(cargoId, -1)] = box;
        Grid.SetRow(border, rowIdx);
        Grid.SetColumn(border, 1);
        grid.Children.Add(border);
    }

    /// <summary>
    /// Celda editable de la columna Fracc. para roles indirectos: permite ingresar
    /// fracciones decimales usando coma o punto (ej: 0,3 o 0.3 o 0,5 o 1).
    /// </summary>
    private void AddFraccionCell(Grid grid, int rowIdx, MoTableRow row, ManoDeObraViewModel vm)
    {
        var cargoId = !string.IsNullOrEmpty(row.CargoId) ? row.CargoId : row.Tag;
        var box = new TextBox
        {
            Text = row.FraccionStr,
            FontFamily = MonoFont,
            FontSize = CellSz,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Amber),
            CaretBrush = new SolidColorBrush(Amber),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            IsReadOnly = !vm.CanEdit,
            ToolTip = "Fracción de dedicación a la obra (ej: 0,3 o 0.3). Permite decimales con coma o punto.",
        };

        var border = new Border
        {
            Child = box,
            Height = RowH,
            Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF9, 0xEE)),
            BorderBrush = new SolidColorBrush(Line2),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = Cursors.IBeam,
            Margin = new Thickness(1, 2, 1, 2),
            CornerRadius = new CornerRadius(4),
        };

        void CommitFraccion()
        {
            if (!vm.CanEdit) return;
            var parsed = MoCargoEditorVm.ParseFraccion(box.Text, row.FraccionObra);
            vm.SetCargoFraccion(cargoId, parsed);
        }

        box.GotFocus += (_, _) =>
        {
            border.BorderBrush = new SolidColorBrush(Blue2);
            border.BorderThickness = new Thickness(1.5);
            box.SelectAll();
        };
        box.LostFocus += (_, _) =>
        {
            border.BorderBrush = new SolidColorBrush(Line2);
            border.BorderThickness = new Thickness(0, 0, 0, 1);
            CommitFraccion();
        };
        box.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!vm.CanEdit || box.IsKeyboardFocusWithin)
                return;
            box.Focus();
            box.SelectAll();
            e.Handled = true;
        };
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                CommitFraccion();
                box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                box.Text = row.FraccionStr;
                box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                e.Handled = true;
            }
        };
        box.PreviewTextInput += (_, e) =>
        {
            if (e.Text.Length == 0) return;
            var ch = e.Text[0];
            if (!char.IsDigit(ch) && ch != ',' && ch != '.' && ch != '%')
            {
                e.Handled = true;
                return;
            }
        };
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            var pasted = e.DataObject.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrWhiteSpace(pasted))
            {
                var clean = pasted.Trim().Replace(',', '.');
                if (decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d))
                    return;
            }
            e.CancelCommand();
        });

        Grid.SetRow(border, rowIdx);
        Grid.SetColumn(border, 3);
        grid.Children.Add(border);
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
        if (monthIdx == 0)
            heatRowOrder.Add(cargoId);

        var (bg, fg) = HeatColors(value, max, isFixed);
        var hasOverride = vm.HasOverride(cargoId, monthIdx);
        var tag = new HeatCellTag
        {
            CargoId = cargoId,
            Month = monthIdx,
            ValueOnFocus = value,
            HadOverride = hasOverride,
        };
        var box = new TextBox
        {
            Text = value.ToString(CultureInfo.InvariantCulture),
            FontFamily = MonoFont,
            FontSize = CellSz,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(fg),
            CaretBrush = new SolidColorBrush(fg),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0),
            IsReadOnly = !vm.CanEdit,
            Tag = tag,
        };

        var border = new Border
        {
            Child = box,
            Height = RowH,
            Background = new SolidColorBrush(isPeakCol && value == 0 ? PeakBg : bg),
            BorderBrush = new SolidColorBrush(hasOverride ? Amber : Line2),
            BorderThickness = hasOverride ? new Thickness(1.5) : new Thickness(0, 0, 0, 1),
            Cursor = Cursors.IBeam,
            Margin = new Thickness(1, 2, 1, 2),
            CornerRadius = new CornerRadius(4),
        };

        box.GotFocus += (_, _) =>
        {
            tag.ValueOnFocus = int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : value;
            tag.HadOverride = vm.HasOverride(cargoId, monthIdx);
            border.BorderBrush = new SolidColorBrush(Blue2);
            border.BorderThickness = new Thickness(1.5);
            box.SelectAll();
        };
        box.LostFocus += (_, _) =>
        {
            var still = vm.HasOverride(cargoId, monthIdx);
            border.BorderBrush = new SolidColorBrush(still ? Amber : Line2);
            border.BorderThickness = still ? new Thickness(1.5) : new Thickness(0, 0, 0, 1);
        };
        box.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!vm.CanEdit || box.IsKeyboardFocusWithin)
                return;
            box.Focus();
            box.SelectAll();
            e.Handled = true;
        };
        box.PreviewTextInput += (_, e) =>
        {
            if (e.Text.Length == 0 || !char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }
            if (box.SelectionLength == box.Text.Length)
            {
                heatSyncing = true;
                box.Clear();
                heatSyncing = false;
            }
        };
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            var pasted = e.DataObject.GetData(DataFormats.Text) as string;
            if (pasted != null && int.TryParse(pasted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
                return;
            e.CancelCommand();
        });
        box.TextChanged += (_, _) => OnHeatTextChanged(box, border, vm);
        box.PreviewKeyDown += (_, e) => OnHeatKeyDown(box, e);

        heatCells[(cargoId, monthIdx)] = box;
        Grid.SetRow(border, rowIdx);
        Grid.SetColumn(border, colIdx);
        grid.Children.Add(border);
    }

    private void OnHeatTextChanged(TextBox box, Border border, ManoDeObraViewModel vm)
    {
        if (heatSyncing || box.Tag is not HeatCellTag tag || !vm.CanEdit)
            return;
        var raw = (box.Text ?? "").Trim();
        if (raw.Length == 0)
        {
            if (!vm.StageClearOverride(tag.CargoId, tag.Month))
            {
                heatSyncing = true;
                box.Text = tag.ValueOnFocus.ToString(CultureInfo.InvariantCulture);
                heatSyncing = false;
                return;
            }
        }
        else if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                 || int.TryParse(raw, NumberStyles.Integer, CultureInfo.GetCultureInfo("es-CL"), out n))
            vm.StageOverride(tag.CargoId, tag.Month, n);
        else
            return;

        var has = vm.HasOverride(tag.CargoId, tag.Month);
        border.BorderBrush = new SolidColorBrush(has ? Amber : Blue2);
        border.BorderThickness = new Thickness(has ? 1.5 : 1);
        ScheduleHeatFlush();
    }

    private void OnHeatKeyDown(TextBox box, KeyEventArgs e)
    {
        if (box.Tag is not HeatCellTag tag)
            return;

        switch (e.Key)
        {
            case Key.Up:
                MoveHeat(tag, -1, 0);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Enter:
                MoveHeat(tag, 1, 0);
                e.Handled = true;
                break;
            case Key.Left:
                MoveHeat(tag, 0, -1, wrap: true);
                e.Handled = true;
                break;
            case Key.Right:
                MoveHeat(tag, 0, 1, wrap: true);
                e.Handled = true;
                break;
            case Key.Tab:
                MoveHeat(tag, 0, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1, wrap: true);
                e.Handled = true;
                break;
            case Key.Delete:
            case Key.Back:
                if (!box.IsReadOnly && DataContext is ManoDeObraViewModel vm)
                {
                    if (vm.StageClearOverride(tag.CargoId, tag.Month))
                    {
                        heatSyncing = true;
                        box.Clear();
                        heatSyncing = false;
                        ScheduleHeatFlush();
                    }
                    else
                    {
                        box.SelectAll();
                    }
                }
                e.Handled = true;
                break;
            case Key.Escape:
                RevertHeat(box, tag);
                e.Handled = true;
                break;
        }
    }

    private void MoveHeat(HeatCellTag tag, int dRow, int dCol, bool wrap = false)
    {
        var row = heatRowOrder.IndexOf(tag.CargoId);
        if (row < 0 || heatMonths <= 0)
            return;
        var col = tag.Month + dCol;
        var nextRow = row + dRow;
        if (wrap && dCol != 0)
        {
            if (col < 0 && col >= -1 && dRow == 0 && heatCells.ContainsKey((tag.CargoId, -1)))
            {
                // Izquierda desde M1 → columna Dot. de la misma fila (solo indirecta).
                col = -1;
                nextRow = row;
            }
            else if (col >= heatMonths)
            {
                col = 0;
                nextRow++;
            }
            else if (col < 0)
            {
                col = heatMonths - 1;
                nextRow--;
            }
        }
        if (nextRow < 0 || nextRow >= heatRowOrder.Count)
            return;
        var key = (heatRowOrder[nextRow], col);
        if (col < 0 && (col != -1 || !heatCells.ContainsKey(key)))
            return;
        if (!heatCells.TryGetValue(key, out var next))
            return;
        next.Focus();
        next.SelectAll();
        next.BringIntoView();
    }

    private void RevertHeat(TextBox box, HeatCellTag tag)
    {
        if (DataContext is ManoDeObraViewModel vm)
        {
            if (tag.Month < 0)
                vm.StageClearDotPico(tag.CargoId);
            else if (tag.HadOverride)
                vm.StageOverride(tag.CargoId, tag.Month, tag.ValueOnFocus);
            else
                vm.StageClearOverride(tag.CargoId, tag.Month);
            ScheduleHeatFlush();
        }

        heatSyncing = true;
        box.Text = tag.ValueOnFocus.ToString(CultureInfo.InvariantCulture);
        heatSyncing = false;
        box.SelectAll();
    }

    private void ScheduleHeatFlush()
    {
        heatFlushTimer.Stop();
        heatFlushTimer.Start();
    }

    private void RememberHeatFocus()
    {
        heatHadFocus = false;
        if (Keyboard.FocusedElement is not TextBox box || box.Tag is not HeatCellTag tag)
            return;
        heatHadFocus = true;
        heatFocusCargo = tag.CargoId;
        heatFocusMonth = tag.Month;
    }

    private void RestoreHeatFocus()
    {
        if (!heatHadFocus || heatFocusCargo == null)
            return;
        if (!heatCells.TryGetValue((heatFocusCargo, heatFocusMonth), out var box))
            return;
        heatSyncing = true;
        box.Focus();
        box.SelectAll();
        heatSyncing = false;
    }

    private sealed class HeatCellTag
    {
        public string CargoId { get; init; } = "";
        public int Month { get; init; }
        public int ValueOnFocus { get; set; }
        public bool HadOverride { get; set; }
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
