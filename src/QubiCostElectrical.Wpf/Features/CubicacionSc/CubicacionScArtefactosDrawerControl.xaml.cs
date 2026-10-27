using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScArtefactosDrawerControl : UserControl
{
    public CubicacionScArtefactosDrawerControl()
    {
        InitializeComponent();

        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                Dispatcher.BeginInvoke(new Action(FocusSearch), System.Windows.Threading.DispatcherPriority.Input);
            }
        };

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CubicacionScArtefactosDrawerCoordinator oldCoord)
        {
            oldCoord.RequestFocusSearch -= OnRequestFocusSearch;
            oldCoord.RequestFocusQuantity -= OnRequestFocusQuantity;
        }

        if (e.NewValue is CubicacionScArtefactosDrawerCoordinator newCoord)
        {
            newCoord.RequestFocusSearch += OnRequestFocusSearch;
            newCoord.RequestFocusQuantity += OnRequestFocusQuantity;
        }
    }

    private void OnRequestFocusSearch()
    {
        Dispatcher.BeginInvoke(new Action(FocusSearch), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnRequestFocusQuantity()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            QuantityBox?.Focus();
            QuantityBox?.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    public void FocusSearch()
    {
        SearchBox?.Focus();
        SearchBox?.SelectAll();
    }

    private void OnLeftThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        double newWidth = ActualWidth - e.HorizontalChange;
        if (newWidth >= 360 && newWidth <= 950)
        {
            Width = newWidth;
        }
    }

    private void OnControlPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

        // Escape: cancela modo cantidad o cierra drawer
        if (e.Key == Key.Escape)
        {
            if (coordinator.IsQuantityMode)
            {
                coordinator.CancelQuantity();
                FocusSearch();
                e.Handled = true;
                return;
            }
            if (coordinator.IsSearchPopupOpen)
            {
                coordinator.IsSearchPopupOpen = false;
                e.Handled = true;
                return;
            }
            coordinator.IsOpen = false;
            e.Handled = true;
            return;
        }

        // Alt+A cierra el drawer si ya está abierto
        if (alt && !ctrl && (e.SystemKey == Key.A || e.Key == Key.A))
        {
            coordinator.IsOpen = false;
            e.Handled = true;
            return;
        }

        // Ctrl+Left / Ctrl+Right: cambiar de piso
        if (ctrl && !alt && e.Key == Key.Right)
        {
            coordinator.ShiftZona(1);
            e.Handled = true;
            return;
        }
        if (ctrl && !alt && e.Key == Key.Left)
        {
            coordinator.ShiftZona(-1);
            e.Handled = true;
            return;
        }

        // F2: Editar Tag rápido
        if (e.Key == Key.F2)
        {
            var target = PopupSugerenciasListBox?.SelectedItem as ArtefactoSugerencia
                         ?? SugerenciasListBox?.SelectedItem as ArtefactoSugerencia
                         ?? coordinator.Sugerencias.FirstOrDefault();
            if (target != null)
            {
                coordinator.EditTag(target);
                e.Handled = true;
                return;
            }
        }
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        if (e.Key == Key.Down)
        {
            if (!coordinator.IsSearchPopupOpen && coordinator.Sugerencias.Count > 0)
            {
                coordinator.IsSearchPopupOpen = true;
            }

            if (coordinator.Sugerencias.Count > 0)
            {
                int current = PopupSugerenciasListBox.SelectedIndex;
                if (current < coordinator.Sugerencias.Count - 1)
                {
                    PopupSugerenciasListBox.SelectedIndex = current + 1;
                    PopupSugerenciasListBox.ScrollIntoView(PopupSugerenciasListBox.SelectedItem);
                }
                else if (current < 0)
                {
                    PopupSugerenciasListBox.SelectedIndex = 0;
                    PopupSugerenciasListBox.ScrollIntoView(PopupSugerenciasListBox.SelectedItem);
                }
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Up)
        {
            if (coordinator.Sugerencias.Count > 0)
            {
                int current = PopupSugerenciasListBox.SelectedIndex;
                if (current > 0)
                {
                    PopupSugerenciasListBox.SelectedIndex = current - 1;
                    PopupSugerenciasListBox.ScrollIntoView(PopupSugerenciasListBox.SelectedItem);
                }
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Enter)
        {
            ArtefactoSugerencia? target = PopupSugerenciasListBox.SelectedItem as ArtefactoSugerencia
                                          ?? coordinator.Sugerencias.FirstOrDefault();

            if (target != null)
            {
                coordinator.OpenQuantityFromSugerencia(target);
                e.Handled = true;
                return;
            }

            // Si hay texto escrito y no coincidió con catálogo, crear nuevo al vuelo
            if (!string.IsNullOrWhiteSpace(coordinator.SearchText))
            {
                var nuevo = new ArtefactoSugerencia
                {
                    Nombre = coordinator.SearchText.Trim(),
                    Categoria = "Otro",
                    EsNuevo = true
                };
                coordinator.OpenQuantityFromSugerencia(nuevo);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            if (coordinator.IsSearchPopupOpen)
            {
                coordinator.IsSearchPopupOpen = false;
                e.Handled = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(coordinator.SearchText))
            {
                coordinator.SearchText = "";
                e.Handled = true;
                return;
            }

            coordinator.IsOpen = false;
            e.Handled = true;
            return;
        }
    }

    private void OnPopupSugerenciaClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        if (PopupSugerenciasListBox.SelectedItem is ArtefactoSugerencia s)
        {
            coordinator.OpenQuantityFromSugerencia(s);
        }
    }

    private void OnQuantityBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        if (e.Key == Key.Enter)
        {
            coordinator.ConfirmQuantity();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            coordinator.CancelQuantity();
            e.Handled = true;
            return;
        }
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CubicacionScArtefactosDrawerCoordinator coordinator)
        {
            coordinator.SearchText = "";
            coordinator.IsSearchPopupOpen = false;
        }
        FocusSearch();
    }

    private void OnSugerenciaClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        if (SugerenciasListBox.SelectedItem is ArtefactoSugerencia s)
        {
            coordinator.OpenQuantityFromSugerencia(s);
        }
    }

    private void OnAssignedQuantityClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        if (sender is FrameworkElement fe && fe.DataContext is ArtefactoZonaItem item)
        {
            coordinator.OpenQuantityFromAssigned(item);
            e.Handled = true;
        }
    }

    private void OnEditarTagContextMenuClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CubicacionScArtefactosDrawerCoordinator coordinator) return;

        var s = PopupSugerenciasListBox?.SelectedItem as ArtefactoSugerencia
                ?? SugerenciasListBox?.SelectedItem as ArtefactoSugerencia;
        if (s != null)
        {
            coordinator.EditTag(s);
        }
    }
}