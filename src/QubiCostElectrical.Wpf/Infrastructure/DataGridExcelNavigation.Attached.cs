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

    /// <summary>
    /// Deja Ctrl+V al dueño de la grilla (código → material, fórmulas, sectores). Sin esto el
    /// pegado genérico marca PreviewKeyDown como atendido, suprime el KeyDown que burbujea y el
    /// pegado con lógica de negocio nunca corre.
    /// </summary>
    public static readonly DependencyProperty HandlePasteByHostProperty = DependencyProperty.RegisterAttached(
        "HandlePasteByHost", typeof(bool), typeof(DataGridExcelNavigation), new PropertyMetadata(false));

    public static void SetHandlePasteByHost(DataGrid grid, bool value) => grid.SetValue(HandlePasteByHostProperty, value);
    public static bool GetHandlePasteByHost(DataGrid grid) => (bool)grid.GetValue(HandlePasteByHostProperty);

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
            grid.SetValue(HookProperty, true);
            return;
        }

        if (grid.GetValue(HookProperty) is not true)
            return;

        grid.PreviewKeyDown -= OnAttachedPreviewKeyDown;
        grid.ClearValue(HookProperty);
    }

    private static void OnAttachedPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is DataGrid grid)
            HandlePreviewKeyDown(grid, e);
    }
}
