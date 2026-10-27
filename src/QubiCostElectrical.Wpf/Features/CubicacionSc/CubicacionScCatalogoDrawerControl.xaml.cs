using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScCatalogoDrawerControl : UserControl
{
    public CubicacionScCatalogoDrawerControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ScrollSelectedCategoryIntoView();
            ScrollSelectedItemIntoView();
        };
    }

    public void FocusNavigation()
    {
        Focus();
        Keyboard.Focus(this);
    }

    public void FocusSearchOrList()
    {
        FocusNavigation();
    }

    public void FocusNewItem()
    {
        if (NewItemTagBox.Visibility == Visibility.Visible)
        {
            NewItemTagBox.Focus();
            NewItemTagBox.SelectAll();
        }
        else
        {
            NewItemBox.Focus();
            NewItemBox.SelectAll();
        }
    }

    public void ScrollSelectedCategoryIntoView()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var child in CategoriesStackPanel.Children)
            {
                if (child is RadioButton rb && rb.IsChecked == true)
                {
                    rb.BringIntoView();
                    break;
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ScrollSelectedItemIntoView()
    {
        if (DataContext is not CubicacionScCatalogoDrawerCoordinator coordinator || coordinator.SelectedItem == null)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            var container = CardsItemsControl.ItemContainerGenerator.ContainerFromItem(coordinator.SelectedItem) as FrameworkElement;
            container?.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnLeftThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        double newWidth = ActualWidth - e.HorizontalChange;
        if (newWidth >= 360 && newWidth <= 950)
        {
            Width = newWidth;
        }
    }

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CubicacionScCatalogoDrawerCoordinator coordinator) return;

        if (e.Key == Key.Escape)
        {
            coordinator.IsOpen = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left && (SearchBox.CaretIndex == 0 || string.IsNullOrEmpty(SearchBox.Text)))
        {
            coordinator.PreviousCategory();
            ScrollSelectedCategoryIntoView();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && (SearchBox.CaretIndex >= SearchBox.Text.Length || string.IsNullOrEmpty(SearchBox.Text)))
        {
            coordinator.NextCategory();
            ScrollSelectedCategoryIntoView();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            coordinator.SelectNextItem();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            coordinator.SelectPreviousItem();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (coordinator.SelectedItem != null)
            {
                if (coordinator.SelectedItem.CanEditTag)
                {
                    coordinator.EditItemTag(coordinator.SelectedItem);
                }
                else
                {
                    coordinator.EditItemName(coordinator.SelectedItem);
                }
                e.Handled = true;
            }
        }
    }

    private void OnControlPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CubicacionScCatalogoDrawerCoordinator coordinator) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        bool isPlusKey = e.Key is Key.Add or Key.OemPlus || (shift && e.Key is Key.OemPlus or Key.Oem1 or Key.OemQuestion);

        // Escape desde cualquier parte del panel cierra el catálogo
        if (e.Key == Key.Escape)
        {
            coordinator.IsOpen = false;
            e.Handled = true;
            return;
        }

        // Alt+C cierra el catálogo si ya está abierto
        if (alt && !ctrl && (e.SystemKey == Key.C || e.Key == Key.C))
        {
            coordinator.IsOpen = false;
            e.Handled = true;
            return;
        }

        // Si el foco está dentro de los inputs de nuevo elemento
        if (NewItemBox.IsFocused || NewItemTagBox.IsFocused)
        {
            if (e.Key == Key.Enter)
            {
                if (coordinator.AddItemCommand.CanExecute(null))
                {
                    coordinator.AddItemCommand.Execute(null);
                    ScrollSelectedItemIntoView();
                    e.Handled = true;
                }
                return;
            }
            return;
        }

        // Atajo Ctrl + + (o + numérico) para enfocar formulario de creación
        if ((ctrl && isPlusKey) || (!ctrl && e.Key == Key.Add))
        {
            FocusNewItem();
            e.Handled = true;
            return;
        }

        // Si el foco está en el SearchBox, OnSearchBoxKeyDown maneja flechas verticales
        if (SearchBox.IsFocused)
        {
            return;
        }

        // Flechas Izquierda y Derecha: Moverse entre pestañas de categorías
        if (e.Key == Key.Left)
        {
            coordinator.PreviousCategory();
            ScrollSelectedCategoryIntoView();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right)
        {
            coordinator.NextCategory();
            ScrollSelectedCategoryIntoView();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        // Flechas Arriba y Abajo: Navegar entre elementos del catálogo
        if (e.Key == Key.Up)
        {
            coordinator.SelectPreviousItem();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            coordinator.SelectNextItem();
            ScrollSelectedItemIntoView();
            e.Handled = true;
            return;
        }

        // Si el usuario escribe directamente letras o números, transferir foco al SearchBox
        if (!ctrl && !alt && e.Key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9) or (>= Key.NumPad0 and <= Key.NumPad9) or Key.OemQuestion)
        {
            SearchBox.Focus();
            return;
        }

        // Supr / Delete: Eliminar elemento seleccionado
        if (e.Key == Key.Delete)
        {
            if (coordinator.SelectedItem != null && coordinator.DeleteItemCommand.CanExecute(coordinator.SelectedItem))
            {
                coordinator.DeleteItemCommand.Execute(coordinator.SelectedItem);
                e.Handled = true;
                return;
            }
        }

        // Enter: Editar elemento seleccionado (Tag o Nombre)
        if (e.Key == Key.Enter)
        {
            if (coordinator.SelectedItem != null)
            {
                if (coordinator.SelectedItem.CanEditTag)
                {
                    coordinator.EditItemTag(coordinator.SelectedItem);
                }
                else
                {
                    coordinator.EditItemName(coordinator.SelectedItem);
                }
                e.Handled = true;
                return;
            }
        }
    }

    private void OnNewItemKeyDown(object sender, KeyEventArgs e)
    {
        // Manejado en OnControlPreviewKeyDown
    }

    private void OnCardMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CatalogoDrawerItem item)
        {
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.SelectedItem = item;
                if (e.ClickCount >= 2)
                {
                    if (item.CanEditTag)
                    {
                        coordinator.EditItemTag(item);
                    }
                    else
                    {
                        coordinator.EditItemName(item);
                    }
                }
                e.Handled = true;
            }
        }
    }

    private void OnNameMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CatalogoDrawerItem item)
        {
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.SelectedItem = item;
                coordinator.EditItemName(item);
                e.Handled = true;
            }
        }
    }

    private void OnMermaBadgeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CatalogoDrawerItem item)
        {
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.SelectedItem = item;
                coordinator.EditItemMerma(item);
                e.Handled = true;
            }
        }
    }

    private void OnAlturaBadgeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CatalogoDrawerItem item)
        {
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.SelectedItem = item;
                coordinator.EditItemAltura(item);
                e.Handled = true;
            }
        }
    }

    private void OnTagBadgeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CatalogoDrawerItem item)
        {
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.SelectedItem = item;
                coordinator.EditItemTag(item);
                e.Handled = true;
            }
        }
    }
    private void OnBulkAlturaInterruptorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.ApplyBulkAlturaInterruptores();
            }
        }
    }

    private void OnBulkAlturaEnchufeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (DataContext is CubicacionScCatalogoDrawerCoordinator coordinator)
            {
                coordinator.ApplyBulkAlturaEnchufes();
            }
        }
    }
}
