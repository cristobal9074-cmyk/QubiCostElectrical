using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Controls;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class CubicacionTakeoffInspectorControl : UserControl
{
    private CubicacionTakeoffInspectorCoordinator? Coordinator => DataContext as CubicacionTakeoffInspectorCoordinator;

    public CubicacionTakeoffInspectorControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CubicacionTakeoffInspectorCoordinator oldCoord)
        {
            oldCoord.SelectedItemChanged -= OnSelectedItemChanged;
        }

        if (e.NewValue is CubicacionTakeoffInspectorCoordinator newCoord)
        {
            newCoord.SelectedItemChanged += OnSelectedItemChanged;
            RebuildColumns(newCoord.SelectedItem);
        }
    }

    private void OnSelectedItemChanged(HojaCubicacionItemVm? item)
    {
        Dispatcher.BeginInvoke(new Action(() => RebuildColumns(item)), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void RebuildColumns(HojaCubicacionItemVm? item)
    {
        TakeoffGrid.ItemsSource = null;
        TakeoffGrid.Columns.Clear();

        if (item == null) return;

        var excelCellStyle = Application.Current.TryFindResource("DataGridCell.Excel") as Style;
        var baseHeaderStyle = Application.Current.TryFindResource("DataGridColumnHeader.Prototype") as Style;

        var hitTestableCellStyle = new Style(typeof(DataGridCell), excelCellStyle);
        hitTestableCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        hitTestableCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));

        // 1. Columna Descripción
        var descCellStyle = new Style(typeof(DataGridCell), excelCellStyle);
        descCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        descCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));

        TakeoffGrid.Columns.Add(new DisplayBindingTextColumn
        {
            Header = "Descripción",
            Binding = new Binding(nameof(HojaCubicacionFilaVm.Descripcion)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            DisplayBinding = new Binding(nameof(HojaCubicacionFilaVm.DescripcionDisplay)),
            Width = 175,
            CellStyle = descCellStyle
        });

        // 2. Columna Suma
        var sumaHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader), baseHeaderStyle);
        sumaHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 253, 244))));
        sumaHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(22, 101, 52))));
        sumaHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        sumaHeaderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        sumaHeaderStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Suma de valores de la fila (clic para inyectar a CANT)"));

        var sumaCellStyle = new Style(typeof(DataGridCell), excelCellStyle);
        sumaCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(24, 16, 185, 129))));
        sumaCellStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(22, 101, 52))));
        sumaCellStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        sumaCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        sumaCellStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        sumaCellStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Clic para inyectar esta suma directo a CANT en el presupuesto"));

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(209, 250, 229))));
        hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(6, 95, 70))));
        sumaCellStyle.Triggers.Add(hoverTrigger);

        var pickedTrigger = new DataTrigger { Binding = new Binding(nameof(HojaCubicacionFilaVm.IsPicked)), Value = true };
        pickedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(16, 185, 129))));
        pickedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        pickedTrigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        sumaCellStyle.Triggers.Add(pickedTrigger);

        var sumaCol = new DataGridTextColumn
        {
            Header = "Suma",
            Binding = new Binding(nameof(HojaCubicacionFilaVm.SumaDisplay)),
            Width = 65,
            IsReadOnly = true,
            HeaderStyle = sumaHeaderStyle,
            CellStyle = sumaCellStyle
        };
        TakeoffGrid.Columns.Add(sumaCol);

        // 3. Columnas dinámicas de datos / sectores (C en adelante)
        int colCount = item.Filas.FirstOrDefault()?.Valores.Count ?? 20;
        var labelRow = item.Filas.FirstOrDefault(f => f.IsLabelRow);
        int targetSlot = Coordinator?.TargetSectorSlot ?? -1;

        for (int i = 0; i < colCount; i++)
        {
            string letter = HojaCubicacionColumns.DataColumnLetter(i);
            string zonaName = (labelRow != null && i < labelRow.Valores.Count && !string.IsNullOrWhiteSpace(labelRow.Valores[i]))
                ? labelRow.Valores[i].Trim()
                : "";
            string colHeader = string.IsNullOrEmpty(zonaName) ? letter : $"{letter} ({zonaName})";

            bool isTargetColumn = (targetSlot >= 0 && i == targetSlot);
            if (isTargetColumn)
            {
                colHeader = $"🎯 {colHeader}";
            }

            Style? colHeaderStyle = null;
            Style colCellStyle = hitTestableCellStyle;

            if (isTargetColumn)
            {
                colHeaderStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader), baseHeaderStyle);
                colHeaderStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(239, 246, 255))));
                colHeaderStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(29, 78, 216))));
                colHeaderStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
                colHeaderStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Columna vinculada al sector destino seleccionado"));

                var targetCellStyle = new Style(typeof(DataGridCell), excelCellStyle);
                targetCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromArgb(20, 37, 99, 235))));
                targetCellStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Color.FromRgb(30, 58, 138))));
                targetCellStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
                targetCellStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                targetCellStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
                targetCellStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Clic para inyectar este valor directo al sector de la fila"));

                var targetHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
                targetHover.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Color.FromRgb(219, 234, 254))));
                targetCellStyle.Triggers.Add(targetHover);

                colCellStyle = targetCellStyle;
            }

            var dataCol = new DisplayBindingTextColumn
            {
                Header = colHeader,
                Binding = new Binding($"Valores[{i}]") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                DisplayBinding = new Binding($"ValoresDisplay[{i}]"),
                Width = 72,
                CellStyle = colCellStyle
            };

            if (colHeaderStyle != null)
            {
                dataCol.HeaderStyle = colHeaderStyle;
            }

            TakeoffGrid.Columns.Add(dataCol);
        }

        TakeoffGrid.ItemsSource = item.Filas;
    }

    private void OnPartidaTabButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HojaCubicacionItemVm item && Coordinator != null)
        {
            Coordinator.SelectedItem = item;
        }
    }

    private void OnTakeoffGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        if (dep == null) return;

        var cell = VisualTreeHelperSafe.FindAncestor<DataGridCell>(dep);
        if (cell == null || cell.DataContext is not HojaCubicacionFilaVm fila || fila.IsLabelRow) return;

        var col = cell.Column;
        if (col == null) return;

        int colDisplayIndex = TakeoffGrid.Columns.IndexOf(col);
        // Si hizo clic en Suma (columna 1):
        if (colDisplayIndex == 1)
        {
            Coordinator?.TransferFilaValue(fila, null);
            e.Handled = true;
            return;
        }

        // Si hizo clic en una columna de datos (columna >= 2, es decir data index >= 0):
        if (colDisplayIndex >= 2)
        {
            int dataIndex = colDisplayIndex - 2;
            // Si es la columna de destino activa, inyecta de inmediato
            if (Coordinator != null && Coordinator.TargetSectorSlot >= 0 && dataIndex == Coordinator.TargetSectorSlot)
            {
                Coordinator.TransferFilaValue(fila, dataIndex);
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Coordinator?.TransferFilaValue(fila, dataIndex);
                e.Handled = true;
            }
        }
    }

    private void OnTakeoffGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var cell = TakeoffGrid.CurrentCell;
            if (cell.IsValid && cell.Item is HojaCubicacionFilaVm fila && !fila.IsLabelRow)
            {
                int colDisplayIndex = TakeoffGrid.Columns.IndexOf(cell.Column);
                if (colDisplayIndex == 1)
                {
                    Coordinator?.TransferFilaValue(fila, null);
                    e.Handled = true;
                }
                else if (colDisplayIndex >= 2)
                {
                    int dataIndex = colDisplayIndex - 2;
                    Coordinator?.TransferFilaValue(fila, dataIndex);
                    e.Handled = true;
                }
            }
        }
    }

    private void OnTakeoffInspectorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Coordinator?.CloseInspector();
            e.Handled = true;
        }
    }
}
