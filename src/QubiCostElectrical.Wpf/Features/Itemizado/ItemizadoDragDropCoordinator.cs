using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.Itemizado;

/// <summary>
/// Coordina el arrastrar y soltar visual de filas dentro de la grilla de Itemizado
/// respetando las reglas de jerarquía (capítulos y partidas).
/// </summary>
public sealed class ItemizadoDragDropCoordinator
{
    private readonly DataGrid grid;
    private readonly Func<ItemizadoViewModel?> vmAccessor;
    private Point dragStartPoint;
    private bool isDragging;
    private ItemizadoRowVm? draggedItem;

    public ItemizadoDragDropCoordinator(DataGrid grid, Func<ItemizadoViewModel?> vmAccessor)
    {
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        this.vmAccessor = vmAccessor ?? throw new ArgumentNullException(nameof(vmAccessor));

        grid.AllowDrop = true;
        grid.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        grid.PreviewMouseMove += OnPreviewMouseMove;
        grid.DragOver += OnDragOver;
        grid.Drop += OnDrop;
    }

    private ItemizadoViewModel? Vm => vmAccessor();

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject dep)
        {
            // Ignorar clics sobre botones, scrollbars o cajas de texto en edición
            if (FindVisualParent<ButtonBase>(dep) != null ||
                FindVisualParent<ScrollBar>(dep) != null ||
                FindVisualParent<TextBox>(dep) != null)
            {
                draggedItem = null;
                return;
            }

            var row = FindVisualParent<DataGridRow>(dep);
            if (row?.Item is ItemizadoRowVm item)
            {
                dragStartPoint = e.GetPosition(grid);
                draggedItem = item;
            }
            else
            {
                draggedItem = null;
            }
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || draggedItem == null || isDragging)
            return;

        var vm = Vm;
        if (vm == null || vm.IsReadOnly)
            return;

        var currentPoint = e.GetPosition(grid);
        var diff = dragStartPoint - currentPoint;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            isDragging = true;
            try
            {
                DragDrop.DoDragDrop(grid, draggedItem, DragDropEffects.Move);
            }
            finally
            {
                isDragging = false;
                draggedItem = null;
            }
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(ItemizadoRowVm)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ItemizadoRowVm)))
            return;

        var sourceItem = e.Data.GetData(typeof(ItemizadoRowVm)) as ItemizadoRowVm;
        if (sourceItem == null)
            return;

        var vm = Vm;
        if (vm == null || vm.IsReadOnly)
            return;

        var pos = e.GetPosition(grid);
        var targetItem = FindRowItemUnderPosition(pos);
        if (targetItem == null || ReferenceEquals(sourceItem, targetItem))
            return;

        ReorderRows(vm, sourceItem, targetItem);
        e.Handled = true;
    }

    private ItemizadoRowVm? FindRowItemUnderPosition(Point position)
    {
        var hit = VisualTreeHelper.HitTest(grid, position);
        if (hit?.VisualHit == null)
            return null;

        var row = FindVisualParent<DataGridRow>(hit.VisualHit);
        return row?.Item as ItemizadoRowVm;
    }

    private static void ReorderRows(ItemizadoViewModel vm, ItemizadoRowVm source, ItemizadoRowVm target)
    {
        var allRows = vm.FilteredRows?.Cast<ItemizadoRowVm>().ToList();
        if (allRows == null)
            return;

        int sourceIndex = allRows.FindIndex(r => r.Id == source.Id);
        int targetIndex = allRows.FindIndex(r => r.Id == target.Id);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return;

        vm.SelectedRow = source;

        if (sourceIndex < targetIndex)
        {
            int steps = targetIndex - sourceIndex;
            for (int i = 0; i < steps; i++)
            {
                if (!vm.MoveDownCommand.CanExecute(null))
                    break;
                vm.MoveDownCommand.Execute(null);
            }
        }
        else
        {
            int steps = sourceIndex - targetIndex;
            for (int i = 0; i < steps; i++)
            {
                if (!vm.MoveUpCommand.CanExecute(null))
                    break;
                vm.MoveUpCommand.Execute(null);
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current != null && current != Application.Current?.MainWindow)
        {
            if (current is T parent)
                return parent;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
