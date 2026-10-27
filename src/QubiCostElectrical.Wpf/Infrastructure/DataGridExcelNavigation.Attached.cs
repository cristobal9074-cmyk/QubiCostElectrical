using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Infrastructure;

public static partial class DataGridExcelNavigation
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DataGrid grid, bool value) => grid.SetValue(EnableProperty, value);
    public static bool GetEnable(DataGrid grid) => (bool)grid.GetValue(EnableProperty);

    private static readonly DependencyProperty HookProperty = DependencyProperty.RegisterAttached(
        "Hook", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false));

    public static readonly DependencyProperty FormulaBarHandlesF2Property = DependencyProperty.RegisterAttached(
        "FormulaBarHandlesF2", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false));
    public static void SetFormulaBarHandlesF2(DataGrid grid, bool value) => grid.SetValue(FormulaBarHandlesF2Property, value);
    public static bool GetFormulaBarHandlesF2(DataGrid grid) => (bool)grid.GetValue(FormulaBarHandlesF2Property);

    /// <summary>
    /// Deja Ctrl+V al dueño de la grilla (código → material, fórmulas, sectores). Sin esto el
    /// pegado genérico marca PreviewKeyDown como atendido, suprime el KeyDown que burbujea y el
    /// pegado con lógica de negocio nunca corre.
    /// </summary>
    public static readonly DependencyProperty HandlePasteByHostProperty = DependencyProperty.RegisterAttached(
        "HandlePasteByHost", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false));

    public static void SetHandlePasteByHost(DataGrid grid, bool value) => grid.SetValue(HandlePasteByHostProperty, value);
    public static bool GetHandlePasteByHost(DataGrid grid) => (bool)grid.GetValue(HandlePasteByHostProperty);

    /// <summary>
    /// Deja Delete / Supr al dueño de la grilla (ej. eliminar fila de material manteniendo foco e indicador).
    /// </summary>
    public static readonly DependencyProperty HandleDeleteByHostProperty = DependencyProperty.RegisterAttached(
        "HandleDeleteByHost", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false));

    public static void SetHandleDeleteByHost(DataGrid grid, bool value) => grid.SetValue(HandleDeleteByHostProperty, value);
    public static bool GetHandleDeleteByHost(DataGrid grid) => (bool)grid.GetValue(HandleDeleteByHostProperty);

    public static readonly DependencyProperty HandleCellEditorByHostProperty = DependencyProperty.RegisterAttached(
        "HandleCellEditorByHost", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false));
    public static void SetHandleCellEditorByHost(DataGrid grid, bool value) => grid.SetValue(HandleCellEditorByHostProperty, value);
    public static bool GetHandleCellEditorByHost(DataGrid grid) => (bool)grid.GetValue(HandleCellEditorByHostProperty);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
            return;

        if (e.NewValue is true)
        {
            if (grid.GetValue(HookProperty) is true)
                return;

            Attach(grid);
            grid.PreviewKeyDown += OnAttachedPreviewKeyDown;
            grid.IsKeyboardFocusWithinChanged += OnGridKeyboardFocusWithinChanged;
            grid.SetValue(HookProperty, true);
            return;
        }

        if (grid.GetValue(HookProperty) is not true)
            return;

        grid.PreviewKeyDown -= OnAttachedPreviewKeyDown;
        grid.IsKeyboardFocusWithinChanged -= OnGridKeyboardFocusWithinChanged;
        grid.ClearValue(HookProperty);
    }

    private static void OnAttachedPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is DataGrid grid)
            HandlePreviewKeyDown(grid, e);
    }

    private static void OnGridKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is DataGrid g)
        {
            if (Keyboard.FocusedElement is not DataGridCell && Keyboard.FocusedElement is not TextBox && Keyboard.FocusedElement is not ComboBox)
            {
                g.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (g.IsKeyboardFocusWithin && Keyboard.FocusedElement is not DataGridCell && Keyboard.FocusedElement is not TextBox && Keyboard.FocusedElement is not ComboBox)
                    {
                        EnsureActiveCell(g);
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
    }
}
