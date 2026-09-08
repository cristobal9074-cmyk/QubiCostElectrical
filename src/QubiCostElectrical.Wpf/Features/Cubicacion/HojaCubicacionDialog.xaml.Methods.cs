using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Controls;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class HojaCubicacionDialog : Window
{
    public static (decimal? PickedValue, List<HojaCubicacionItemVm> Items) PickValue(
        Window? owner, IEnumerable<HojaCubicacionItemVm>? existingItems)
    {
        var dialog = new HojaCubicacionDialog(existingItems) { Owner = owner, IsPickMode = true };
        dialog.ApplyPickModeChrome();
        dialog.ShowDialog();
        return (dialog.PickedValue, dialog.Items.ToList());
    }

    public HojaCubicacionDialog() : this(null) { }

    public HojaCubicacionDialog(IEnumerable<HojaCubicacionItemVm>? existingItems)
    {
        InitializeComponent();
        ItemsHost.ItemsSource = items;

        var previos = existingItems?.ToList();
        if (previos != null && previos.Count > 0)
        {
            foreach (var existente in previos)
            {
                AttachItem(existente);
                items.Add(existente);
            }
        }
        else
        {
            var primerItem = new HojaCubicacionItemVm("", "Nuevo ítem", "ml", DefaultColumnCount);
            AttachItem(primerItem);
            items.Add(primerItem);
        }

        RefreshGrandTotal();
    }

    private void ApplyPickModeChrome()
    {
        Title = "Hoja de Cubicación — elegir cantidad";
        PickModeBanner.Visibility = Visibility.Visible;
    }

    private void AttachItem(HojaCubicacionItemVm item)
    {
        item.PropertyChanged += OnItemPropertyChanged;
    }

    private void DetachItem(HojaCubicacionItemVm item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        if (itemGrids.TryGetValue(item, out var grid))
            loadedGrids.Remove(grid);
        itemGrids.Remove(item);
    }

    // Los ítems se conservan en memoria y se le pasan al PRÓXIMO diálogo que se abra (ver
    // CubicacionViewModel.OpenHojaCubicacion) — sin desuscribirse acá, cada reapertura sumaría
    // otro OnItemPropertyChanged de esta instancia ya cerrada, acumulando manejadores muertos.
    private void OnDialogClosed(object? sender, System.EventArgs e)
    {
        foreach (var item in items)
            item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not HojaCubicacionItemVm item) return;

        if (e.PropertyName == nameof(HojaCubicacionItemVm.Subtotal))
            RefreshGrandTotal();
        else if (e.PropertyName == nameof(HojaCubicacionItemVm.ColumnCount) && itemGrids.TryGetValue(item, out var grid))
            BuildColumns(grid, item);
    }

    private void RefreshGrandTotal()
    {
        decimal total = 0m;
        foreach (var item in items)
            total += item.Subtotal;
        GrandTotalRun.Text = total.ToString("N2");
    }

    private void OnItemGridLoaded(object sender, RoutedEventArgs e)
    {
        var grid = (DataGrid)sender;
        if (grid.DataContext is not HojaCubicacionItemVm item) return;

        itemGrids[item] = grid;

        // Loaded puede dispararse más de una vez para la misma grilla (ej. al colapsar/expandir
        // el ítem). Reconstruir las columnas de nuevo en ese caso deja las filas ya generadas
        // (sobre todo la primera) con celdas apuntando a columnas viejas — de ahí que solo se
        // vuelva a intentar escribir ahí y no funcione. Por eso BuildColumns solo corre una vez
        // por instancia de grilla; los cambios reales de cantidad de columnas (+Agregar columna)
        // se reconstruyen aparte, vía OnItemPropertyChanged.
        if (loadedGrids.Add(grid))
            BuildColumns(grid, item);

        grid.PreviewKeyDown -= OnItemGridPreviewKeyDown;
        grid.PreviewKeyDown += OnItemGridPreviewKeyDown;
        grid.CurrentCellChanged -= OnItemGridCurrentCellChanged;
        grid.CurrentCellChanged += OnItemGridCurrentCellChanged;

        // Shift+clic para seleccionar un rango: clic en la primera celda (selección normal),
        // mantener Shift y clic en la última celda del rango. Se prefiere sobre "clic y arrastrar"
        // porque no depende de rastrear MouseMove durante la captura del mouse (frágil de
        // depurar) — es un solo evento, determinístico, y es un gesto igualmente válido en Excel.
        grid.PreviewMouseLeftButtonDown -= OnItemGridPreviewMouseLeftButtonDown;
        grid.PreviewMouseLeftButtonDown += OnItemGridPreviewMouseLeftButtonDown;

        // Deja el foco de teclado listo en la primera celda de datos apenas se abre el ítem —
        // sin esto, hasta el primer clic del usuario la grilla puede no tener foco real
        // (solo selección visual por mouse), y las teclas no llegan a ninguna celda.
        Dispatcher.BeginInvoke(new System.Action(() =>
        {
            if (grid.Items.Count > 0 && grid.Columns.Count > 2)
                DataGridExcelNavigation.FocusCell(grid, 0, grid.Columns[2]);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnItemGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DataGrid grid || grid.DataContext is not HojaCubicacionItemVm item) return;

        // Agregar/quitar fila SOLO con Ctrl+Plus / Ctrl+Minus (o el botón "+ Agregar fila") —
        // Enter ya NO agrega filas automáticamente: eso hacía que cualquier Enter normal (para
        // confirmar una celda y bajar a la de abajo) terminara creando filas de sobra sin que el
        // usuario lo pidiera.
        bool editingTextForRows = Keyboard.FocusedElement is TextBoxBase;
        if (!editingTextForRows && Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key is Key.Add or Key.OemPlus)
        {
            item.AddFila();
            e.Handled = true;
            return;
        }

        if (!editingTextForRows && Keyboard.Modifiers == ModifierKeys.Control &&
            e.Key is Key.Subtract or Key.OemMinus &&
            grid.CurrentItem is HojaCubicacionFilaVm filaActual)
        {
            item.RemoveFila(filaActual);
            e.Handled = true;
            return;
        }

        // Ctrl+C / Ctrl+X / Ctrl+V solo cuando NO se está escribiendo dentro de una celda —
        // si el foco está en el TextBox de edición, se deja el copiar/pegar nativo del TextBox.
        bool editingText = Keyboard.FocusedElement is TextBoxBase;
        if (!editingText && Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.C or Key.X)
        {
            DataGridExcelNavigation.CopySelectionToClipboard(grid);
            // El marco punteado animado de "esto se copió" se calcula para una grilla fija de
            // una sola tabla; acá hay varias grillas apiladas (una por ítem) con su propio
            // scroll y layout, así que termina posicionado mal — se saca de inmediato, el
            // copiado al portapapeles ya funcionó sin necesidad de esa marca visual.
            DataGridExcelNavigation.ClearCopyMarker(grid);
            if (e.Key == Key.X)
                DataGridExcelNavigation.ClearSelection(grid);
            e.Handled = true;
            return;
        }

        if (!editingText && Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            string text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            if (!string.IsNullOrEmpty(text))
                DataGridExcelNavigation.TryPasteIntoSelection(grid, text, _ => false);
            e.Handled = true;
            return;
        }
    }

    // ── Barra de fórmulas: muestra "celda actual" (ej. "C4") y su contenido crudo, editable
    // desde ahí igual que en Excel — sirve además como "fila de entrada de datos" siempre
    // visible, sin depender de encontrar la celda exacta entre 300 columnas.
    private void OnItemGridCurrentCellChanged(object? sender, EventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var itemRoot = FindAncestor<Border>(grid);
        if (itemRoot == null) return;

        var refText = FindDescendantByName<TextBlock>(itemRoot, "FormulaBarRef");
        var inputBox = FindDescendantByName<TextBox>(itemRoot, "FormulaBarInput");
        if (refText == null || inputBox == null) return;

        var cellInfo = grid.CurrentCell;
        if (!cellInfo.IsValid || cellInfo.Column == null)
        {
            refText.Text = "";
            inputBox.Text = "";
            inputBox.Tag = null;
            inputBox.IsEnabled = false;
            return;
        }

        int rowNumber = grid.Items.IndexOf(cellInfo.Item) + 1;
        refText.Text = $"{cellInfo.Column.Header}{rowNumber}";
        inputBox.Tag = cellInfo;
        inputBox.IsEnabled = !cellInfo.Column.IsReadOnly;
        inputBox.Text = DataGridExcelNavigation.GetCellText(cellInfo.Item, cellInfo.Column);
    }

    private void OnFormulaBarKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter && e.Key != Key.Escape) return;
        var box = (TextBox)sender;
        if (e.Key == Key.Enter)
            CommitFormulaBar(box);
        e.Handled = true;
        Keyboard.ClearFocus();
    }

    private void OnFormulaBarLostFocus(object sender, RoutedEventArgs e) => CommitFormulaBar((TextBox)sender);

    private void CommitFormulaBar(TextBox box)
    {
        if (box.Tag is not DataGridCellInfo cellInfo || !cellInfo.IsValid || cellInfo.Column == null)
            return;
        DataGridExcelNavigation.SetCellText(cellInfo.Item, cellInfo.Column, box.Text);
    }

    // ── Shift+clic: selecciona el rectángulo de celdas entre la celda activa y la clickeada ──
    private void OnItemGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;

        // Modo selección: un clic normal (sin modificadores) sobre la columna Suma (B), en una
        // fila de datos con total, entrega ese valor a quien abrió el diálogo. Se revisa ANTES
        // que el resto para no competir con Shift+clic (rango) ni con el clic normal nativo.
        if (IsPickMode && Keyboard.Modifiers == ModifierKeys.None)
        {
            var hitCell = HitTestCell(grid, e.GetPosition(grid));
            if (hitCell?.Item is HojaCubicacionFilaVm fila && !fila.IsLabelRow &&
                hitCell.Value.Column?.Header is string header && header == "Suma" &&
                !string.IsNullOrWhiteSpace(fila.SumaDisplay) &&
                decimal.TryParse(fila.SumaDisplay.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal picked))
            {
                PickedValue = picked;
                DialogResult = true;
                Close();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.Shift) return; // clic normal: que lo maneje el DataGrid nativo

        var anchor = grid.CurrentCell;
        if (!anchor.IsValid) return;

        var target = HitTestCell(grid, e.GetPosition(grid));
        if (target == null) return;

        SelectCellRange(grid, anchor, target.Value);
        e.Handled = true;
    }

    private static DataGridCellInfo? HitTestCell(DataGrid grid, Point point)
    {
        var hit = VisualTreeHelper.HitTest(grid, point)?.VisualHit;
        if (hit == null) return null;

        var cell = hit as DataGridCell ?? FindAncestor<DataGridCell>(hit);
        if (cell?.Column == null) return null;

        var row = FindAncestor<DataGridRow>(cell);
        if (row == null) return null;

        return new DataGridCellInfo(row.Item, cell.Column);
    }

    private static void SelectCellRange(DataGrid grid, DataGridCellInfo anchor, DataGridCellInfo current)
    {
        var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).OrderBy(c => c.DisplayIndex).ToList();
        int anchorRow = grid.Items.IndexOf(anchor.Item);
        int currentRow = grid.Items.IndexOf(current.Item);
        int anchorCol = columns.IndexOf(anchor.Column);
        int currentCol = columns.IndexOf(current.Column);
        if (anchorRow < 0 || currentRow < 0 || anchorCol < 0 || currentCol < 0) return;

        int rowMin = System.Math.Min(anchorRow, currentRow);
        int rowMax = System.Math.Max(anchorRow, currentRow);
        int colMin = System.Math.Min(anchorCol, currentCol);
        int colMax = System.Math.Max(anchorCol, currentCol);

        grid.SelectedCells.Clear();
        for (int r = rowMin; r <= rowMax; r++)
        {
            var item = grid.Items[r];
            for (int c = colMin; c <= colMax; c++)
                grid.SelectedCells.Add(new DataGridCellInfo(item, columns[c]));
        }

        grid.CurrentCell = current;
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(child);
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && match.Name == name)
                return match;
            var found = FindDescendantByName<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void BuildColumns(DataGrid grid, HojaCubicacionItemVm item)
    {
        grid.Columns.Clear();

        // Estilo de celda "hit-testeable": DataGridCell.Excel no fija un Background por defecto
        // (solo lo hace vía el trigger IsSelected), así que en el estado normal la celda queda
        // con Background=null — y en WPF una celda sin Background explícito NO recibe hit-test
        // de mouse en sus zonas vacías, solo donde hay texto dibujado. Eso rompía por completo el
        // arrastre para seleccionar un rango: la mayoría de los puntos del mouse no "tocaban"
        // ninguna celda. Con un Background transparente pero explícito, toda el área de la celda
        // vuelve a ser detectable, sin cambiar cómo se ve.
        var hitTestableCellStyle = new Style(typeof(DataGridCell), (Style)Application.Current.FindResource("DataGridCell.Excel"));
        hitTestableCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));

        // Placeholder ("Nombra las columnas aquí →") en cursiva/atenuado, solo visible cuando la
        // fila de nombres está vacía — para no confundirlo con una descripción real ya escrita.
        var descripcionElementStyle = new Style(typeof(TextBlock));
        var placeholderTrigger = new MultiDataTrigger();
        placeholderTrigger.Conditions.Add(new Condition(new Binding(nameof(HojaCubicacionFilaVm.IsLabelRow)), true));
        placeholderTrigger.Conditions.Add(new Condition(new Binding(nameof(HojaCubicacionFilaVm.Descripcion)) { Converter = new QubiCostElectrical.Wpf.Infrastructure.StringNotEmptyToBoolConverter() }, false));
        placeholderTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
        placeholderTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, (System.Windows.Media.Brush)Application.Current.FindResource("Brush.Muted")));
        descripcionElementStyle.Triggers.Add(placeholderTrigger);

        grid.Columns.Add(new DisplayBindingTextColumn
        {
            Header = "Descripción",
            Binding = new Binding(nameof(HojaCubicacionFilaVm.Descripcion)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
            DisplayBinding = new Binding(nameof(HojaCubicacionFilaVm.DescripcionDisplay)),
            Width = 220,
            CellStyle = hitTestableCellStyle,
            ElementStyle = descripcionElementStyle
        });

        var sumaColumn = new DataGridTextColumn
        {
            Header = "Suma",
            Binding = new Binding(nameof(HojaCubicacionFilaVm.SumaDisplay)),
            Width = 90,
            IsReadOnly = true
        };
        var sumaCellStyle = new Style(typeof(DataGridCell), (Style)Application.Current.FindResource("DataGridCell.Excel"));
        sumaCellStyle.Setters.Add(new Setter(Control.BackgroundProperty, (System.Windows.Media.Brush)Application.Current.FindResource("Brush.GreenSoft")));
        sumaCellStyle.Setters.Add(new Setter(Control.ForegroundProperty, (System.Windows.Media.Brush)Application.Current.FindResource("Brush.Green")));
        sumaCellStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
        // La fila de nombres de columna no tiene suma (es texto libre, no cubica nada) — se
        // apaga el verde ahí para no insinuar que hay un total calculado en esa fila.
        var noSumaForLabelRow = new DataTrigger { Binding = new Binding(nameof(HojaCubicacionFilaVm.IsLabelRow)), Value = true };
        noSumaForLabelRow.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        noSumaForLabelRow.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Transparent));
        sumaCellStyle.Triggers.Add(noSumaForLabelRow);
        if (IsPickMode)
        {
            // Afordancia visual de "esto es clickeable" — cursor de mano y resalte al pasar el
            // mouse, solo en modo selección (en el uso normal la columna Suma es de solo lectura).
            sumaCellStyle.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
            sumaCellStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Clic para usar este valor"));
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Control.BackgroundProperty, (System.Windows.Media.Brush)Application.Current.FindResource("Brush.Green")));
            hover.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            sumaCellStyle.Triggers.Add(hover);
        }
        sumaColumn.CellStyle = sumaCellStyle;
        grid.Columns.Add(sumaColumn);

        // Igual que una hoja de Excel real: A = Descripción, B = Suma, C en adelante = datos.
        // Los encabezados usan letras reales de columna (C, D... Z, AA...). Nombrar cada columna
        // (ej. "Depto 1") se hace en la PRIMERA FILA de la grilla (fila de nombres, ver
        // HojaCubicacionFilaVm.IsLabelRow) — una fila real, no un control aparte en el
        // encabezado, para que tenga automáticamente las mismas funciones que cualquier otra
        // celda: selección, navegación con teclado, copiar/pegar, Ctrl+D/R para correlativos.
        // El texto en edición (Valores[i]) es el crudo escrito por el usuario — número o fórmula
        // — y el mostrado (ValoresDisplay[i]) es el resultado ya calculado, o vacío si la celda
        // no tiene datos (nunca "0" de relleno).
        int columnCount = item.ColumnCount;
        for (int i = 0; i < columnCount; i++)
        {
            grid.Columns.Add(new DisplayBindingTextColumn
            {
                Header = HojaCubicacionColumns.DataColumnLetter(i),
                Binding = new Binding($"Valores[{i}]") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                DisplayBinding = new Binding($"ValoresDisplay[{i}]"),
                Width = 40,
                CellStyle = hitTestableCellStyle
            });
        }

        var deleteColumn = new DataGridTemplateColumn { Header = "", Width = 36 };
        var factory = new System.Windows.FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "✕");
        factory.SetValue(Button.PaddingProperty, new Thickness(0));
        factory.SetValue(Button.ToolTipProperty, "Quitar fila");

        // La fila de nombres de columna no se puede quitar — se oculta el botón ahí.
        var deleteButtonStyle = new Style(typeof(Button), (Style)Application.Current.FindResource("Button.Secondary"));
        var hideForLabelRow = new DataTrigger { Binding = new Binding(nameof(HojaCubicacionFilaVm.IsLabelRow)), Value = true };
        hideForLabelRow.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
        deleteButtonStyle.Triggers.Add(hideForLabelRow);
        factory.SetValue(Button.StyleProperty, deleteButtonStyle);

        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnRemoveRowClick));
        deleteColumn.CellTemplate = new DataTemplate { VisualTree = factory };
        grid.Columns.Add(deleteColumn);
    }

    private void OnAddItemClick(object sender, RoutedEventArgs e)
    {
        var nuevo = new HojaCubicacionItemVm("", "Nuevo ítem", "ml", DefaultColumnCount);
        AttachItem(nuevo);
        items.Add(nuevo);
        RefreshGrandTotal();
    }

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not HojaCubicacionItemVm item) return;
        if (items.Count <= 1) return;

        DetachItem(item);
        items.Remove(item);
        RefreshGrandTotal();
    }

    private void OnToggleCollapseClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HojaCubicacionItemVm item)
            item.IsCollapsed = !item.IsCollapsed;
    }

    private void OnAddRowClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HojaCubicacionItemVm item)
            item.AddFila();
    }

    private void OnAddColumnClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is HojaCubicacionItemVm item)
            item.AddColumn();
    }

    private void OnRemoveRowClick(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not HojaCubicacionFilaVm fila) return;
        fila.Owner?.RemoveFila(fila);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
