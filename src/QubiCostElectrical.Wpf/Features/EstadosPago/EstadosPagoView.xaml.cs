using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.EstadosPago;

/// <summary>
/// Edición de la columna "Avance mes %" estilo Excel: Enter/↓/Tab avanzan a la fila
/// siguiente, ↑/Shift+Enter/Shift+Tab retroceden, PageUp/PageDown saltan 10 filas,
/// Ctrl+Home/Ctrl+End van a los extremos; al entrar a la celda se selecciona todo
/// (escribir reemplaza), la doble clic posiciona el cursor y el valor se confirma
/// al salir de la celda (binding LostFocus).
/// </summary>
public partial class EstadosPagoView : UserControl
{
    public EstadosPagoView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Mantiene el lateral (Resumen / Avance / Configuración) clavado en el tope
    /// del área visible mientras se hace scroll: sube hasta el tope y se queda
    /// ahí (sticky), y al llegar al final de su columna se libera y sigue el
    /// contenido. Si el lateral no cabe en el viewport se comporta como antes.
    /// </summary>
    private void OnPagoScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (PagoAside is null || PagoColumns is null || PagoScroll is null)
            return;

        PagoAside.RenderTransform = null; // posición natural para medir

        if (PagoScroll.Content is not FrameworkElement content
            || !PagoAside.IsVisible
            || PagoAside.ActualHeight <= 0
            || PagoColumns.ActualHeight <= 0)
            return;

        double viewport = PagoScroll.ViewportHeight;
        if (viewport <= 0 || PagoAside.ActualHeight >= viewport - 4)
            return; // no cabe en el viewport: sin fijado

        var asideOrigin = PagoAside.TranslatePoint(new Point(0, 0), content);
        var columnsOrigin = PagoColumns.TranslatePoint(new Point(0, 0), content);

        double naturalTop = asideOrigin.Y;
        double containerBottom = columnsOrigin.Y + PagoColumns.ActualHeight;
        double maxShift = Math.Max(0, containerBottom - naturalTop - PagoAside.ActualHeight);

        double shift = e.VerticalOffset - naturalTop;
        if (shift <= 0)
            return;
        if (shift > maxShift)
            shift = maxShift;

        PagoAside.RenderTransform = new TranslateTransform(0, shift);
    }

    private void OnAvanceCellGotKeyboardFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            box.SelectAll();
    }

    private void OnAvanceCellLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox box || !box.IsLoaded || !box.IsVisible)
            return;
        if (PresentationSource.FromVisual(box) == null)
            return;
        if (box.DataContext is not EstadoPagoPartidaVm partida || partida.IsDisposed)
            return;
        if (DataContext is EstadosPagoViewModel vm && (vm.IsLoading || !vm.HasProject || !vm.CanEdit || vm.UndoService?.IsUndoingOrRedoing == true))
            return;

        bool hasDigit = false;
        foreach (char c in box.Text ?? "")
        {
            if (char.IsDigit(c))
            {
                hasDigit = true;
                break;
            }
        }
        if (!hasDigit)
            box.Text = "0"; // vacío o solo separador: comprometer 0

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void OnAvanceCellPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled)
            return;

        if (e.ClickCount >= 2)
        {
            box.Focus();
            box.SelectionLength = 0;
            box.CaretIndex = box.Text?.Length ?? 0;
            e.Handled = true;
            return;
        }

        if (!box.IsKeyboardFocused)
            box.Focus();
        box.SelectAll();
        e.Handled = true;
    }

    private void OnAvanceCellPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled)
            return;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            return;

        var boxes = CollectAvanceBoxes();
        int idx = boxes.FindIndex(b => ReferenceEquals(b, box));
        if (idx < 0)
            return;

        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        // Escape: cancela edición no confirmada y restaura valor del modelo (estilo Excel)
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            box.SelectAll();
            return;
        }

        // Ctrl+Z: si hay edición pendiente en la celda la descarta en 1 pulsación; si ya está confirmada delega al Undo global
        if (ctrl && e.Key == Key.Z && !shift)
        {
            e.Handled = true;
            var expr = box.GetBindingExpression(TextBox.TextProperty);
            string originalText = box.DataContext is EstadoPagoPartidaVm p
                ? (Grid.GetColumn(box) == 6 ? p.CantidadMes.ToString(CultureInfo.CurrentCulture) : p.AvanceMes.ToString(CultureInfo.CurrentCulture))
                : "";

            bool isUncommitted = !string.Equals(box.Text?.Trim(), originalText.Trim(), StringComparison.OrdinalIgnoreCase);
            if (isUncommitted)
            {
                expr?.UpdateTarget();
                box.SelectAll();
                return;
            }

            Keyboard.ClearFocus();
            ShellNotificationService.RequestUndo?.Invoke();
            return;
        }

        // Ctrl+Y o Ctrl+Shift+Z: rehace en 1 pulsación
        if ((ctrl && e.Key == Key.Y && !shift) || (ctrl && shift && e.Key == Key.Z))
        {
            e.Handled = true;
            Keyboard.ClearFocus();
            ShellNotificationService.RequestRedo?.Invoke();
            return;
        }

        // Tab / Shift+Tab: navegación horizontal continua entre celdas (Cant. -> % -> sig. fila)
        if (e.Key == Key.Tab)
        {
            int targetTab = shift ? idx - 1 : idx + 1;
            if (targetTab >= 0 && targetTab < boxes.Count)
            {
                e.Handled = true;
                MoveAvanceCell(boxes[targetTab]);
            }
            return;
        }

        if (ctrl && e.Key != Key.Home && e.Key != Key.End)
            return; // Ctrl+C/V/etc.: comportamiento nativo de edición

        // Navegación vertical: SIEMPRE se mantiene en la misma columna (Cant. Mes o % Mes)
        int currentCol = Grid.GetColumn(box);
        var colBoxes = boxes.Where(b => Grid.GetColumn(b) == currentCol).ToList();
        int colIdx = colBoxes.FindIndex(b => ReferenceEquals(b, box));
        if (colIdx < 0)
            return;

        int target;
        if (ctrl && e.Key == Key.Home)
            target = 0;
        else if (ctrl && e.Key == Key.End)
            target = colBoxes.Count - 1;
        else if (e.Key == Key.Up)
            target = colIdx - 1;
        else if (e.Key == Key.Down)
            target = colIdx + 1;
        else if (e.Key == Key.Enter)
            target = shift ? colIdx - 1 : colIdx + 1;
        else if (e.Key == Key.PageUp)
            target = Math.Max(0, colIdx - 10);
        else if (e.Key == Key.PageDown)
            target = Math.Min(colBoxes.Count - 1, colIdx + 10);
        else
            return;

        e.Handled = true;
        if (target >= 0 && target < colBoxes.Count)
        {
            MoveAvanceCell(colBoxes[target]);
        }
    }

    private void OnAvanceCellPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box || !box.IsEnabled || string.IsNullOrEmpty(e.Text))
            return;

        foreach (char ch in e.Text)
        {
            if (!char.IsDigit(ch) && ch is not (',' or '.'))
            {
                e.Handled = true; // solo dígitos y separador decimal
                return;
            }
        }

        if (e.Text is not ("," or "."))
            return; // dígitos: el TextBox inserta solo (reemplazando la selección)

        // Normaliza al separador decimal de la cultura (el teclado chileno escribe
        // "," y la cultura de la app usa "." u otro símbolo: evita parses erróneos).
        string decSep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        char dec = decSep.Length > 0 ? decSep[0] : '.';

        e.Handled = true;
        int start = box.SelectionStart;
        string rest = box.Text.Remove(start, box.SelectionLength);
        if (rest.IndexOf(dec) >= 0)
            return; // ya tiene separador decimal: ignorar (estilo Excel)

        box.Text = rest.Insert(start, dec.ToString());
        box.CaretIndex = start + 1;
        box.SelectionLength = 0;
    }

    private static void MoveAvanceCell(TextBox box)
    {
        box.Focus();
        box.SelectAll();
        box.BringIntoView();
    }

    private List<TextBox> CollectAvanceBoxes()
    {
        var boxes = new List<TextBox>();
        CollectAvanceBoxes(this, boxes);
        return boxes;
    }

    private static void CollectAvanceBoxes(DependencyObject node, List<TextBox> boxes)
    {
        int count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is TextBox box && box.DataContext is EstadoPagoPartidaVm && box.IsEnabled)
                boxes.Add(box);
            CollectAvanceBoxes(child, boxes);
        }
    }
}
