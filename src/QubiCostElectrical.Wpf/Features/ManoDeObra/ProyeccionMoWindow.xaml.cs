using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace QubiCostElectrical.Wpf.Features.ManoDeObra;

public partial class ProyeccionMoWindow : Window
{
    private const double TaskMinWidth = 92;
    private const double BarMaxWidth = 62;
    private const double BarMinWidth = 22;
    private const double RowHeight = 26;
    private const double HeaderRowHeight = 34;
    private const double SigmaRowHeight = 28;

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CL");
    private static readonly Thickness FocusLine = new(1.5);
    private static readonly Thickness CellLine = new(0, 0, 0, 1);
    private static readonly Thickness SigmaLine = new(0, 1, 0, 0);

    private readonly ManoDeObraViewModel vm;
    private readonly Dictionary<(int Row, int Col), TextBox> cells = new();
    private readonly Dictionary<TextBox, Border> chips = new();
    private readonly DispatcherTimer flushTimer;
    private readonly DispatcherTimer toastTimer;
    private readonly DispatcherTimer flashTimer;
    private TextBlock?[] monthTotals = [];
    private Rectangle?[] monthBars = [];
    private TextBlock?[] colTotals = [];
    private Rectangle?[] sparkBars = [];
    private Border?[] mesCells = [];
    private Brush?[] mesRest = [];
    private Border?[] headerCells = [];
    private TextBlock? grandTotal;
    private int flashMonth = -1;
    private bool rebuilding;
    private bool syncingText;
    private bool reverting;

    public ProyeccionMoWindow(ManoDeObraViewModel viewModel)
    {
        vm = viewModel;
        InitializeComponent();
        ApplyAverageButton.Command = vm.ApplyAverageProjectionCommand;
        ApplyButton.Command = vm.TakeProjectionCommand;
        SheetScroll.SizeChanged += (_, _) => FitSheet();
        flushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        flushTimer.Tick += (_, _) =>
        {
            flushTimer.Stop();
            vm.FlushProjectionEdits();
            SetSaved(busy: false);
        };
        toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2100) };
        toastTimer.Tick += (_, _) =>
        {
            toastTimer.Stop();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
            fade.Completed += (_, _) => ToastBorder.Visibility = Visibility.Collapsed;
            ToastBorder.BeginAnimation(OpacityProperty, fade);
        };
        flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        flashTimer.Tick += (_, _) =>
        {
            flashTimer.Stop();
            if (flashMonth >= 0)
                HighlightRow(flashMonth, on: false);
            flashMonth = -1;
        };
        vm.PropertyChanged += OnVmChanged;
        Closed += (_, _) =>
        {
            flushTimer.Stop();
            toastTimer.Stop();
            flashTimer.Stop();
            vm.PropertyChanged -= OnVmChanged;
            vm.FlushProjectionEdits();
        };
        Rebuild();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (rebuilding)
            return;
        if ((e.PropertyName == nameof(ManoDeObraViewModel.Meses)
             || e.PropertyName == nameof(ManoDeObraViewModel.TotalMeses)
             || e.PropertyName == nameof(ManoDeObraViewModel.TotalMonths)
             || e.PropertyName == nameof(ManoDeObraViewModel.ProjectionTasks))
            && !vm.IsApplyingProjection)
        {
            Rebuild();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (!vm.ProjectionTasks.Any(t => t.Cantidades.Any(v => v > 0)))
            return;
        ShowToast();
    }

    private void OnApplyAverage(object sender, RoutedEventArgs e)
    {
        if (!vm.ProjectionTasks.Any(t => t.Cantidades.Any(v => v > 0)))
            return;
        ShowToast();
    }

    private void ShowToast()
    {
        toastTimer.Stop();
        ToastBorder.Visibility = Visibility.Visible;
        ToastBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        toastTimer.Start();
    }

    public void Rebuild()
    {
        if (rebuilding)
            return;
        rebuilding = true;
        try
        {
            BuildSheet();
            RefreshTotals();
            FitSheet();
        }
        finally
        {
            rebuilding = false;
        }
    }

    private void FitSheet()
    {
        if (SheetScroll.ViewportWidth > 0)
            Sheet.MinWidth = SheetScroll.ViewportWidth;
    }

    private void BuildSheet()
    {
        var grid = Sheet;
        cells.Clear();
        chips.Clear();
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        var tasks = vm.ProjectionTasks;
        var months = vm.TotalMonths;
        var taskCount = tasks.Length;
        var addCol = taskCount + 1;
        var totalCol = taskCount + 2;

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        for (var i = 0; i < taskCount; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = TaskMinWidth });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderRowHeight) });
        for (var m = 0; m < months; m++)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowHeight) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SigmaRowHeight) });

        monthTotals = new TextBlock?[months];
        monthBars = new Rectangle?[months];
        colTotals = new TextBlock?[taskCount];
        headerCells = new Border?[taskCount + 1];
        mesCells = new Border?[months];
        mesRest = new Brush?[months];
        grandTotal = null;

        PutMesHeader(grid);
        for (var c = 0; c < taskCount; c++)
            PutHeader(grid, c + 1, tasks[c]);
        PutAddHeader(grid, addCol);
        PutTotalHeader(grid, totalCol);
        for (var m = 0; m < months; m++)
            PutMonthRow(grid, m, tasks);
        PutSigmaRow(grid, tasks, months, addCol, totalCol);
        BuildSpark(months);
    }

    private void PutMesHeader(Grid grid)
    {
        var cell = Cell(grid, 0, 0, HeaderBrush, CellLine);
        cell.Child = MakeLabel("Mes", 9.5, FontWeights.SemiBold, MutedBrush);
    }

    private void PutHeader(Grid grid, int col, MoProjectionTaskVm task)
    {
        var name = new TextBox
        {
            Text = task.Nombre,
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = InkBrush,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            CaretBrush = InkBrush,
            IsReadOnly = !vm.CanEdit,
            AcceptsReturn = false,
            Padding = new Thickness(6, 0, vm.CanEdit ? 16 : 6, 0),
            ToolTip = task.Nombre,
        };
        var id = task.Id;
        name.LostFocus += (_, _) =>
        {
            if (!reverting)
                vm.RenameProjectionTask(id, name.Text);
        };
        name.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                vm.RenameProjectionTask(id, name.Text);
                FocusCell(1, col);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                FocusCell(1, col);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                reverting = true;
                name.Text = task.Nombre;
                Keyboard.ClearFocus();
                reverting = false;
                e.Handled = true;
            }
        };

        var layer = new Grid();
        layer.Children.Add(name);
        if (vm.CanEdit)
        {
            var remove = new Button
            {
                Content = "×",
                Style = (Style)FindResource("Proj.HeaderRemove"),
                ToolTip = "Quitar partida",
            };
            remove.Click += (_, _) =>
            {
                vm.RemoveProjectionTask(id);
                Rebuild();
            };
            layer.Children.Add(remove);
        }

        var cell = Cell(grid, 0, col, HeaderBrush, CellLine);
        cell.Child = layer;
        headerCells[col] = cell;
        cells[(0, col)] = name;
    }

    private void PutAddHeader(Grid grid, int col)
    {
        var cell = Cell(grid, 0, col, HeaderBrush, CellLine);
        var add = new Button
        {
            Content = "+",
            Style = (Style)FindResource("Proj.AddBtn"),
            IsEnabled = vm.CanEdit,
            ToolTip = "Agregar partida",
        };
        add.Click += (_, _) => StartAdd(cell);
        cell.Child = add;
    }

    private void StartAdd(Border cell)
    {
        if (!vm.CanEdit)
            return;
        var input = new TextBox
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            BorderBrush = BlueBrush,
            Background = Brushes.White,
            Margin = new Thickness(4, 5, 4, 5),
            Padding = new Thickness(4, 0, 4, 0),
            ToolTip = "Nombre de la partida",
        };
        var done = false;
        void Commit()
        {
            if (done)
                return;
            done = true;
            var value = input.Text.Trim();
            if (value.Length == 0)
            {
                Rebuild();
                return;
            }
            vm.AddProjectionTask(value);
            Rebuild();
            FocusCell(1, vm.ProjectionTasks.Length);
        }
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                done = true;
                Rebuild();
                e.Handled = true;
            }
        };
        input.LostFocus += (_, _) => Commit();
        cell.Child = input;
        input.Focus();
        input.SelectAll();
    }

    private void PutTotalHeader(Grid grid, int col)
    {
        var cell = Cell(grid, 0, col, HeaderBrush, CellLine);
        cell.Child = MakeLabel("Total", 11, FontWeights.SemiBold, BlueDarkBrush);
    }

    private void PutMonthRow(Grid grid, int month, MoProjectionTaskVm[] tasks)
    {
        var row = month + 1;
        var rest = month % 2 == 0 ? SurfaceBrush : ZebraBrush;
        var peak = vm.ProjectionMonthIsPeak(month);

        var mes = Cell(grid, row, 0, rest, CellLine);
        var number = MakeLabel(
            (month + 1).ToString(CultureInfo.InvariantCulture),
            10.5,
            peak ? FontWeights.Bold : FontWeights.SemiBold,
            peak ? GreenBrush : Ink4Brush,
            mono: true);
        if (peak)
        {
            mes.Child = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = PeakBrush,
                BorderBrush = OkLineBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 0, 5, 0),
                MinWidth = 22,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = number,
            };
        }
        else
        {
            mes.Child = number;
        }
        mesCells[month] = mes;
        mesRest[month] = rest;

        for (var c = 0; c < tasks.Length; c++)
            PutQuantity(grid, row, c + 1, tasks[c], month, rest);

        var total = Cell(grid, row, tasks.Length + 2, TotalBrush, CellLine);
        var layer = new Grid();
        var bar = new Rectangle
        {
            Fill = BarBrush,
            Height = 16,
            RadiusX = 3,
            RadiusY = 3,
            Width = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        var value = MakeLabel("", 12, FontWeights.Bold, BlueDarkBrush, mono: true);
        layer.Children.Add(bar);
        layer.Children.Add(value);
        total.Child = layer;
        monthBars[month] = bar;
        monthTotals[month] = value;
    }

    private void PutQuantity(Grid grid, int row, int col, MoProjectionTaskVm task, int month, Brush rest)
    {
        var value = month < task.Cantidades.Length ? task.Cantidades[month] : 0;
        var cell = Cell(grid, row, col, rest, CellLine);
        var chip = new Border { Style = (Style)FindResource("Proj.QtyChip") };
        var box = new TextBox
        {
            Text = value == 0 ? "" : value.ToString(CultureInfo.InvariantCulture),
            FontFamily = MonoFont,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = BlueDarkBrush,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            CaretBrush = BlueBrush,
            IsReadOnly = !vm.CanEdit,
            Tag = (task.Id, month),
            Padding = new Thickness(0),
        };
        if (value > 0)
            chip.Visibility = Visibility.Visible;
        var layer = new Grid();
        layer.Children.Add(chip);
        layer.Children.Add(box);
        cell.Child = layer;
        cells[(row, col)] = box;
        chips[box] = chip;

        box.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (box.IsKeyboardFocusWithin)
                return;
            box.Focus();
            box.SelectAll();
            e.Handled = true;
        };
        box.GotFocus += (_, _) =>
        {
            cell.BorderBrush = BlueBrush;
            cell.BorderThickness = FocusLine;
            cell.Background = SurfaceBrush;
            if (chip.Visibility == Visibility.Visible)
            {
                chip.Background = ChipFocusBrush;
                box.Foreground = WhiteBrush;
            }
            box.SelectAll();
            HighlightRow(month, on: true);
            HighlightCol(col, on: true);
        };
        box.LostFocus += (_, _) =>
        {
            cell.BorderBrush = LineBrush;
            cell.BorderThickness = CellLine;
            cell.Background = cell.IsMouseOver ? HoverBrush : rest;
            chip.ClearValue(Border.BackgroundProperty);
            box.Foreground = BlueDarkBrush;
            HighlightRow(month, cell.IsMouseOver);
            HighlightCol(col, cell.IsMouseOver);
            if (!reverting)
                PushQuantity(box, rewrite: true);
        };
        cell.MouseEnter += (_, _) =>
        {
            cell.Background = HoverBrush;
            HighlightRow(month, on: true);
            HighlightCol(col, on: true);
        };
        cell.MouseLeave += (_, _) =>
        {
            var focused = box.IsKeyboardFocusWithin;
            cell.Background = focused ? SurfaceBrush : rest;
            HighlightRow(month, focused);
            HighlightCol(col, focused);
        };
        box.PreviewTextInput += (_, e) =>
        {
            if (e.Text.Length == 0 || !char.IsDigit(e.Text[0]))
            {
                e.Handled = true;
                return;
            }
            if (box.SelectionLength == box.Text.Length)
                box.Clear();
        };
        DataObject.AddPastingHandler(box, (_, e) =>
        {
            var pasted = e.DataObject.GetData(DataFormats.Text) as string;
            if (pasted != null && int.TryParse(pasted, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
                return;
            e.CancelCommand();
        });
        box.TextChanged += (_, _) =>
        {
            if (rebuilding || syncingText || reverting)
                return;
            PushQuantity(box, rewrite: false);
        };
        box.PreviewKeyDown += OnQuantityKeyDown;
    }

    private void PutSigmaRow(Grid grid, MoProjectionTaskVm[] tasks, int months, int addCol, int totalCol)
    {
        var row = months + 1;
        var sigma = Cell(grid, row, 0, HeaderBrush, SigmaLine);
        sigma.Child = MakeLabel("Σ", 12, FontWeights.SemiBold, MutedBrush, mono: true);
        for (var c = 0; c < tasks.Length; c++)
        {
            var cell = Cell(grid, row, c + 1, SigmaBgBrush, SigmaLine);
            var label = MakeLabel("0", 11, FontWeights.SemiBold, MutedBrush, mono: true);
            cell.Child = label;
            colTotals[c] = label;
        }
        Cell(grid, row, addCol, SigmaBgBrush, SigmaLine);
        var grand = Cell(grid, row, totalCol, TotalBrush, SigmaLine);
        grandTotal = MakeLabel("0", 12, FontWeights.Bold, BlueDarkBrush, mono: true);
        grand.Child = grandTotal;
    }

    private void BuildSpark(int months)
    {
        SparkPanel.Children.Clear();
        sparkBars = new Rectangle?[months];
        for (var m = 0; m < months; m++)
        {
            var bar = new Rectangle
            {
                Width = 7,
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = SparkBrush,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.Hand,
            };
            var month = m;
            bar.MouseLeftButtonUp += (_, _) => GotoMonth(month);
            SparkPanel.Children.Add(bar);
            sparkBars[m] = bar;
        }
    }

    private void GotoMonth(int month)
    {
        if (month < 0 || month >= mesCells.Length || mesCells[month] == null)
            return;
        FocusCell(month + 1, 1);
        flashMonth = month;
        mesCells[month]!.Background = FlashBrush;
        flashTimer.Stop();
        flashTimer.Start();
    }

    private void OnQuantityKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        switch (e.Key)
        {
            case Key.Up:
                Move(box, -1, 0);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Enter:
                Move(box, 1, 0);
                e.Handled = true;
                break;
            case Key.Left:
                Move(box, 0, -1);
                e.Handled = true;
                break;
            case Key.Right:
                Move(box, 0, 1);
                e.Handled = true;
                break;
            case Key.Tab:
                Move(box, 0, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1, wrap: true);
                e.Handled = true;
                break;
            case Key.Delete:
            case Key.Back:
                if (!box.IsReadOnly)
                {
                    syncingText = true;
                    box.Clear();
                    syncingText = false;
                    PushQuantity(box, rewrite: false);
                }
                e.Handled = true;
                break;
            case Key.Escape:
                reverting = true;
                ShowStored(box);
                Keyboard.ClearFocus();
                reverting = false;
                e.Handled = true;
                break;
        }
    }

    private void Move(TextBox current, int dRow, int dCol, bool wrap = false)
    {
        var border = current.Parent switch
        {
            Grid layer => layer.Parent as Border,
            Border direct => direct,
            _ => null,
        };
        if (border == null)
            return;
        var row = Grid.GetRow(border);
        var col = Grid.GetColumn(border);
        var tasks = vm.ProjectionTasks.Length;
        var targetRow = row + dRow;
        var targetCol = col + dCol;
        if (wrap && dCol != 0 && tasks > 0)
        {
            if (targetCol > tasks)
            {
                targetCol = 1;
                targetRow++;
            }
            else if (targetCol < 1)
            {
                targetCol = tasks;
                targetRow--;
            }
        }
        if (targetCol < 1 || targetCol > tasks)
            return;
        FocusCell(targetRow, targetCol);
    }

    private void FocusCell(int row, int col)
    {
        if (!cells.TryGetValue((row, col), out var next))
            return;
        next.Focus();
        next.SelectAll();
        next.BringIntoView();
    }

    private void PushQuantity(TextBox box, bool rewrite)
    {
        if (box.Tag is not (string id, int month))
            return;
        var raw = (box.Text ?? "").Trim();
        var value = 0;
        if (raw.Length > 0
            && !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
            && !int.TryParse(raw, NumberStyles.Integer, Es, out value))
            value = 0;
        value = Math.Max(0, value);
        if (vm.SetProjectionQuantity(id, month, value))
            ScheduleFlush();
        if (rebuilding)
            return;
        if (rewrite)
            ShowStored(box);
        UpdateChip(box);
        RefreshTotals();
    }

    private void UpdateChip(TextBox box)
    {
        if (!chips.TryGetValue(box, out var chip))
            return;
        var hasValue = int.TryParse((box.Text ?? "").Trim(), NumberStyles.Integer, Es, out var parsed) && parsed > 0;
        chip.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ScheduleFlush()
    {
        SetSaved(busy: true);
        flushTimer.Stop();
        flushTimer.Start();
    }

    private void SetSaved(bool busy)
    {
        SavedPill.SetResourceReference(Border.BackgroundProperty, busy ? "Proj.BusyBg" : "Proj.OkBg");
        SavedPill.SetResourceReference(Border.BorderBrushProperty, busy ? "Proj.BusyEdge" : "Proj.OkEdge");
        SavedText.SetResourceReference(TextBlock.ForegroundProperty, busy ? "Proj.BusyInk" : "Proj.OkInk");
        SavedDot.SetResourceReference(Shape.FillProperty, busy ? "Proj.BusyDot" : "Proj.OkDot");
        SavedText.Text = busy ? "guardando…" : "guardado";
    }

    private void ShowStored(TextBox box)
    {
        if (box.Tag is not (string id, int month))
            return;
        var task = Array.Find(vm.ProjectionTasks, t => t.Id == id);
        var value = task != null && month < task.Cantidades.Length ? task.Cantidades[month] : 0;
        var display = value == 0 ? "" : value.ToString(CultureInfo.InvariantCulture);
        if (box.Text == display)
            return;
        syncingText = true;
        box.Text = display;
        syncingText = false;
    }

    private void RefreshTotals()
    {
        var months = Math.Min(vm.TotalMonths, monthTotals.Length);
        var tasks = vm.ProjectionTasks;
        var totals = new int[months];
        var sum = 0;
        var max = 0;
        for (var m = 0; m < months; m++)
        {
            var total = vm.ProjectionMonthTotal(m);
            totals[m] = total;
            sum += total;
            if (total > max)
                max = total;
        }
        for (var m = 0; m < months; m++)
        {
            if (monthTotals[m] != null)
                monthTotals[m]!.Text = totals[m].ToString(CultureInfo.InvariantCulture);
            var bar = monthBars[m];
            if (bar != null)
                bar.Width = totals[m] <= 0 ? 0 : Math.Max(BarMinWidth, max <= 0 ? 0 : totals[m] / (double)max * BarMaxWidth);
        }
        for (var c = 0; c < colTotals.Length && c < tasks.Length; c++)
            colTotals[c]!.Text = ColumnSum(tasks[c], months).ToString("N0", Es);
        if (grandTotal != null)
            grandTotal.Text = sum.ToString("N0", Es);

        var avg = vm.ProjectionMonthlyAverage();
        var avgRounded = vm.ProjectionMonthlyAverageRounded();
        AverageText.Text = FormatAverage(avg);
        AverageText.ToolTip = $"Promedio exacto: {avg:N2} trabajadores/mes · Redondeado: {avgRounded} trabajadores/mes";
        if (ApplyAverageText != null)
            ApplyAverageText.Text = $"Aplicar promedio ({avgRounded} pers.)";
        TotalSumText.Text = sum.ToString("N0", Es);
        MaxValueText.Text = max.ToString(CultureInfo.InvariantCulture);
        var peakMonths = new List<string>();
        for (var m = 0; m < months; m++)
        {
            if (max > 0 && totals[m] == max)
                peakMonths.Add("m" + (m + 1).ToString(CultureInfo.InvariantCulture));
        }
        MaxMonthsText.Text = peakMonths.Count > 0 ? string.Join(", ", peakMonths) : "—";
        ScopeText.Text = tasks.Length + " × " + vm.TotalMonths;

        for (var m = 0; m < sparkBars.Length && m < months; m++)
        {
            var bar = sparkBars[m];
            if (bar == null)
                continue;
            bar.Height = max <= 0 ? 3 : Math.Max(3, Math.Round(totals[m] / (double)max * 32));
            bar.Fill = max > 0 && totals[m] == max ? SparkPeakBrush : SparkBrush;
            bar.ToolTip = "Mes " + (m + 1).ToString(CultureInfo.InvariantCulture) + " · " + totals[m]
                + (max > 0 && totals[m] == max ? " (pico)" : "");
        }
    }

    private static int ColumnSum(MoProjectionTaskVm task, int months)
    {
        var sum = 0;
        for (var m = 0; m < months && m < task.Cantidades.Length; m++)
            sum += task.Cantidades[m];
        return sum;
    }

    private void HighlightRow(int month, bool on)
    {
        if (month < 0 || month >= mesCells.Length)
            return;
        var cell = mesCells[month];
        var rest = mesRest[month];
        if (cell == null || rest == null)
            return;
        cell.Background = on ? RowHiBrush : rest;
    }

    private void HighlightCol(int col, bool on)
    {
        if (col < 1 || headerCells == null || col >= headerCells.Length || headerCells[col] == null)
            return;
        var header = headerCells[col]!;
        header.Background = on ? SurfaceBrush : HeaderBrush;
        header.BorderBrush = on ? BlueBrush : LineBrush;
        header.BorderThickness = on ? new Thickness(0, 0, 0, 2) : CellLine;
    }

    private TextBlock MakeLabel(string text, double size, FontWeight weight, Brush foreground, bool mono = false)
        => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            FontFamily = mono ? MonoFont : UiFont,
            Foreground = foreground,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

    private Border Cell(Grid grid, int row, int col, Brush bg, Thickness thickness)
    {
        var border = new Border
        {
            Background = bg,
            BorderBrush = LineBrush,
            BorderThickness = thickness,
            SnapsToDevicePixels = true,
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        grid.Children.Add(border);
        return border;
    }

    private static string FormatAverage(double value)
    {
        // Redondeo a entero (ej: 12,9 -> 13, 13,6 -> 14) según requerimiento y fórmula Excel =ROUNDUP(AVERAGE(...), 0)
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded.ToString("N0", Es);
    }

    private Brush InkBrush => (Brush)FindResource("Brush.Ink");
    private Brush Ink4Brush => (Brush)FindResource("Brush.Ink4");
    private Brush MutedBrush => (Brush)FindResource("Brush.Muted");
    private Brush BlueBrush => (Brush)FindResource("Brush.Blue");
    private Brush BlueDarkBrush => (Brush)FindResource("Brush.BlueDark");
    private Brush GreenBrush => (Brush)FindResource("Brush.Green");
    private Brush WhiteBrush => (Brush)FindResource("Brush.White");
    private Brush LineBrush => (Brush)FindResource("Brush.Line");
    private Brush SurfaceBrush => (Brush)FindResource("Brush.Surface");
    private Brush ZebraBrush => (Brush)FindResource("Brush.Surface2");
    private Brush SigmaBgBrush => (Brush)FindResource("Brush.Surface2");
    private Brush HoverBrush => (Brush)FindResource("Brush.BlueHover");
    private Brush RowHiBrush => (Brush)FindResource("Brush.BlueFaint");
    private Brush HeaderBrush => (Brush)FindResource("Brush.CellFormulaBg");
    private Brush TotalBrush => (Brush)FindResource("Brush.BlueFaint");
    private Brush PeakBrush => (Brush)FindResource("Brush.GreenSoft");
    private Brush FlashBrush => (Brush)FindResource("Brush.BlueSoft");
    private Brush OkLineBrush => (Brush)FindResource("Proj.OkLine");
    private Brush ChipFocusBrush => (Brush)FindResource("Proj.ChipFocus");
    private Brush BarBrush => (Brush)FindResource("Proj.TotalBar");
    private Brush SparkBrush => (Brush)FindResource("Proj.SparkBar");
    private Brush SparkPeakBrush => (Brush)FindResource("Proj.SparkPeak");
    private FontFamily MonoFont => (FontFamily)FindResource("Font.Mono");
    private FontFamily UiFont => (FontFamily)FindResource("Font.Ui");
}
