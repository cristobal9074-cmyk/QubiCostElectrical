using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class CubicacionScView : UserControl
{
    public CubicacionScView(CubicacionScViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CircuitoAdded += OnCircuitoAdded;
        viewModel.CircuitoRemoving += OnCircuitoRemoving;
        viewModel.RequestRestoreActiveCell += EnsureActiveCellSelected;
        viewModel.RequestClearGridSelection += () =>
        {
            CircuitosGrid?.SelectedCells.Clear();
            if (CircuitosGrid != null)
                CircuitosGrid.CurrentCell = default;
        };
        // Confirma la celda activa del DataGrid ANTES de que LoadCircuitos() limpie Circuitos.
        // Sin esto, si el usuario tenía un TextBox en edición (ej. LargoTuberia) y hace clic en
        // otro tablero, el binding UpdateSourceTrigger=LostFocus no alcanza a propagar el valor
        // al row model porque la fila es destruida antes de que LostFocus dispare — el dato
        // ingresado se pierde silenciosamente y el circuito queda en 0.
        viewModel.RequestCommitGridEdit += () =>
        {
            if (CircuitosGrid == null || !IsCircuitosGridEditing()) return;
            if (Keyboard.FocusedElement is FrameworkElement focusedEditor)
            {
                var tb = focusedEditor as TextBox ?? FindDescendant<TextBox>(focusedEditor);
                var cb = FindAncestor<ComboBox>(focusedEditor) ?? focusedEditor as ComboBox;
                var editingCell = FindAncestor<DataGridCell>(focusedEditor);
                if (tb != null && editingCell?.Column != null && editingCell.DataContext is CircuitoScRowVm editingRow)
                    CommitCircuitoCellEditor(tb, cb, editingCell.Column, editingRow);
            }
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
        };
        // Al empezar a editar, dejar solo esa celda seleccionada — evita el “fila entera azul”
        // si antes había un rango o un circuito completo marcado vía N°.
        CircuitosGrid.BeginningEdit += OnCircuitosGridBeginningEdit;
        CircuitosGrid.PreparingCellForEdit += (_, e) =>
        {
            CircuitosGrid.SelectedCells.Clear();
            if (e.Row.Item != null && e.Column != null)
                CircuitosGrid.SelectedCells.Add(new DataGridCellInfo(e.Row.Item, e.Column));
        };
        CircuitosGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnCircuitosGridScrollChanged));
        CircuitosGrid.LayoutUpdated += OnCircuitosGridLayoutUpdated;
        CircuitosGrid.SizeChanged += (_, _) =>
        {
            UpdateColumnsVisibility();
            Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
        };
        CircuitosGrid.Loaded += (_, _) =>
        {
            UpdateColumnsVisibility();
            Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
            EnsureActiveCellSelected();
        };

        DataContextChanged += (_, _) =>
        {
            UpdateColumnsVisibility();
            Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
        };

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(CubicacionScViewModel.SelectedCuadro) or nameof(CubicacionScViewModel.CircuitosFiltrados)
                or nameof(CubicacionScViewModel.IsPerspectiveGenerales) or nameof(CubicacionScViewModel.IsPerspectiveDeptos)
                or nameof(CubicacionScViewModel.ShowColUbicacion) or nameof(CubicacionScViewModel.ShowColBandeja))
            {
                UpdateColumnsVisibility();
                Dispatcher.BeginInvoke(new Action(EnsureActiveCellSelected), System.Windows.Threading.DispatcherPriority.Loaded);
                Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            if (e.PropertyName == nameof(CubicacionScViewModel.IsCatalogoDrawerOpen))
            {
                if (viewModel.IsCatalogoDrawerOpen)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CatalogoDrawerPanel?.FocusNavigation();
                        if (CatalogoDrawerPanel != null)
                        {
                            CatalogoDrawerPanel.ScrollSelectedCategoryIntoView();

                            CatalogoDrawerPanel.ScrollSelectedItemIntoView();
                        }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(EnsureActiveCellSelected), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            if (e.PropertyName == nameof(CubicacionScViewModel.IsArtefactosDrawerOpen))
            {
                if (viewModel.IsArtefactosDrawerOpen)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ArtefactosDrawerPanel?.FocusSearch();
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Dispatcher.BeginInvoke(new Action(EnsureActiveCellSelected), System.Windows.Threading.DispatcherPriority.Input);
                }
            }
            if (e.PropertyName is nameof(CubicacionScViewModel.ShowSegundoDucto) or nameof(CubicacionScViewModel.ShowColSegundoDucto))
            {
                UpdateColumnsVisibility();
                Dispatcher.BeginInvoke(new Action(SyncBandColumnWidths), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            if (e.PropertyName is nameof(CubicacionScViewModel.IsAdvancedMode))
            {
                UpdateColumnsVisibility();
                Dispatcher.BeginInvoke(new Action(UpdateColumnsVisibility), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        // Ctrl+Shift+H: Cambio masivo de familia / línea
        if (ctrl && shift && e.Key == Key.H)
        {
            if (DataContext is CubicacionScViewModel scVmH && scVmH.OpenFamilyBatchReplaceCommand.CanExecute(null))
            {
                scVmH.OpenFamilyBatchReplaceCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Alt+C: alternar o abrir Catálogo lateral
        if (alt && !ctrl && (e.SystemKey == Key.C || e.Key == Key.C))
        {
            if (DataContext is CubicacionScViewModel scVmAltC && scVmAltC.OpenCatalogoCommand.CanExecute(null))
            {
                scVmAltC.OpenCatalogoCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Alt+A: alternar o abrir Artefactos lateral
        if (alt && !ctrl && (e.SystemKey == Key.A || e.Key == Key.A))
        {
            if (DataContext is CubicacionScViewModel scVmAltA)
            {
                var target = scVmAltA.SelectedCircuito ?? ResolveCurrentOrSelectedCircuito() ?? scVmAltA.Circuitos.FirstOrDefault();
                if (scVmAltA.OpenArtefactosCommand.CanExecute(target))
                {
                    scVmAltA.OpenArtefactosCommand.Execute(target);
                    Dispatcher.BeginInvoke(new Action(FocusArtefactosSearch), System.Windows.Threading.DispatcherPriority.Input);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Alt+D: alternar circuito Disponible / Activo en cualquier momento
        if (alt && !ctrl && (e.SystemKey == Key.D || e.Key == Key.D))
        {
            if (TryToggleCircuitoDisponible())
            {
                e.Handled = true;
                return;
            }
        }

        // Escape con Catálogo C.SC abierto: cerrar siempre el catálogo y volver a la grilla
        if (e.Key == Key.Escape && DataContext is CubicacionScViewModel scVmCatalogo && scVmCatalogo.IsCatalogoDrawerOpen)
        {
            scVmCatalogo.CatalogoDrawer.IsOpen = false;
            EnsureActiveCellSelected();
            e.Handled = true;
            return;
        }
        // Escape con Artefactos C.SC abierto: cancelar cantidad o cerrar drawer
        if (e.Key == Key.Escape && DataContext is CubicacionScViewModel scVmArtefactos && scVmArtefactos.IsArtefactosDrawerOpen)
        {
            if (scVmArtefactos.ArtefactosDrawer.IsQuantityMode)
            {
                scVmArtefactos.ArtefactosDrawer.CancelQuantity();
            }
            else
            {
                scVmArtefactos.ArtefactosDrawer.IsOpen = false;
                EnsureActiveCellSelected();
            }
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && DataContext is CubicacionScViewModel scVmEsc && scVmEsc.HasSearchText)
        {
            if (scVmEsc.ClearSearchCommand.CanExecute(null)) scVmEsc.ClearSearchCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (ctrl && !alt && DataContext is CubicacionScViewModel scVm)
        {
            bool isPlus = e.Key is Key.Add or Key.OemPlus || (shift && e.Key is Key.OemPlus or Key.Oem1 or Key.OemQuestion);
            bool isMinus = !shift && e.Key is Key.Subtract or Key.OemMinus;

            if (isPlus && scVm.IsCatalogoDrawerOpen)
            {
                CatalogoDrawerPanel?.FocusNewItem();
                e.Handled = true;
                return;
            }

            if (isPlus && scVm.AddCircuitoCommand.CanExecute(null))
            {
                if (IsCircuitosGridEditing())
                {
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                    CircuitosGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                scVm.AddCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }

            if (isMinus && scVm.RemoveCircuitoCommand.CanExecute(null))
            {
                if (IsCircuitosGridEditing())
                {
                    CircuitosGrid.CancelEdit();
                }
                scVm.RemoveCircuitoCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }
    }

    private void OnCircuitoAdded(string circuitoId)
    {
        Dispatcher.BeginInvoke(new Action(() => BeginEditNewCircuito(circuitoId)), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void OnCircuitoRemoving(CircuitoScRowVm row)
    {
        CircuitosGrid.CancelEdit();

        if (CircuitosGrid.SelectedCells.Any(c => ReferenceEquals(c.Item, row)))
        {
            CircuitosGrid.SelectedCells.Clear();
        }

        if (ReferenceEquals(CircuitosGrid.CurrentCell.Item, row))
        {
            CircuitosGrid.CurrentCell = default;
        }
    }

    private void BeginEditNewCircuito(string circuitoId)
    {
        var row = CircuitosGrid.Items.OfType<CircuitoScRowVm>().FirstOrDefault(r => r.Id == circuitoId);
        if (row == null)
            return;

        var column = CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible && !c.IsReadOnly)
                     ?? CircuitosGrid.Columns.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        if (column == null)
            return;

        CircuitosGrid.ScrollIntoView(row, column);
        CircuitosGrid.UpdateLayout();
        if (DataContext is CubicacionScViewModel scVm)
            scVm.SelectedCircuito = row;
        CircuitosGrid.CurrentCell = new DataGridCellInfo(row, column);
        CircuitosGrid.SelectedCells.Clear();
        CircuitosGrid.SelectedCells.Add(CircuitosGrid.CurrentCell);

        CircuitosGrid.Focus();
        Keyboard.Focus(CircuitosGrid);
        CircuitosGrid.BeginEdit();
    }

    private void OnCircuitosGridBeginningEdit(object? sender, DataGridBeginningEditEventArgs e)
    {
        if (Vm.IsGridReadOnly || !Vm.CanEdit)
            e.Cancel = true;
    }

    private void OnMoreActionsClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }
    }

    // ── Colapsar / Expandir panel de tableros (Mueca) ─────────────────────────
    private bool _isSidebarCollapsed;
    private GridLength _savedSidebarWidth = new(170);

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _isSidebarCollapsed = !_isSidebarCollapsed;
        ApplySidebarCollapseState();
    }

    private void TablerosListCollapsed_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _isSidebarCollapsed = false;
        ApplySidebarCollapseState();
    }

    private void ApplySidebarCollapseState()
    {
        if (_isSidebarCollapsed)
        {
            if (TablerosColumn.Width.Value > 60)
                _savedSidebarWidth = TablerosColumn.Width;

            TablerosColumn.MinWidth = 48;
            TablerosColumn.MaxWidth = 48;
            TablerosColumn.Width = new GridLength(48);

            SidebarSplitter.Visibility = Visibility.Collapsed;
            SidebarMuecaIcon.Text = "▸";
            SidebarMuecaButton.ToolTip = "Expandir panel de tableros";

            TablerosHeaderExpanded.Visibility = Visibility.Collapsed;
            TablerosHeaderCollapsed.Visibility = Visibility.Visible;
            TablerosListExpanded.Visibility = Visibility.Collapsed;
            TablerosListCollapsed.Visibility = Visibility.Visible;
        }
        else
        {
            TablerosColumn.MinWidth = 130;
            TablerosColumn.MaxWidth = 320;
            TablerosColumn.Width = _savedSidebarWidth.Value >= 130 ? _savedSidebarWidth : new GridLength(185);

            SidebarSplitter.Visibility = Visibility.Visible;
            SidebarMuecaIcon.Text = "◂";
            SidebarMuecaButton.ToolTip = "Colapsar panel de tableros";

            TablerosHeaderExpanded.Visibility = Visibility.Visible;
            TablerosHeaderCollapsed.Visibility = Visibility.Collapsed;
            TablerosListExpanded.Visibility = Visibility.Visible;
            TablerosListCollapsed.Visibility = Visibility.Collapsed;
        }
    }

    // ── Renombrar tablero (sidebar) ──────────────────────────────────────────
    private void TableroItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!Vm.CanEdit) return;
        if (sender is ListBoxItem { DataContext: CuadroScRowVm vm })
            vm.IsEditing = true;
    }

    private void TableroEdit_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.CanEdit) return;
        if (sender is FrameworkElement { DataContext: CuadroScRowVm vm })
            vm.IsEditing = true;
    }

    private void TableroNombre_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.IsVisible)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void TableroNombre_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: CuadroScRowVm vm })
            vm.IsEditing = false; // el binding LostFocus ya confirmó el nombre
    }

    private void TableroNombre_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not CuadroScRowVm vm) return;
        if (e.Key == Key.Enter)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            vm.IsEditing = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget(); // descarta lo tecleado
            vm.IsEditing = false;
            e.Handled = true;
        }
    }

    internal static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        return QubiCostElectrical.Wpf.Infrastructure.VisualTreeHelperSafe.FindAncestor<T>(start);
    }

    // Busca el TextBox interno (PART_EditableTextBox) de un ComboBox editable ya realizado en
    // el árbol visual. Solo funciona después de que la plantilla se aplicó (ej. en el evento
    // Loaded o en un handler posterior, nunca en el constructor de la celda).
    internal static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                return match;
            var nested = FindDescendant<T>(child);
            if (nested != null)
                return nested;
        }
        return null;
    }

    // Junta todos los descendientes de tipo T en orden de árbol visual (usado por el flyout de
    // pisos para navegar ↑↓ entre los TextBox de L.Band/L.PVC de cada zona).
    internal static void CollectDescendants<T>(DependencyObject root, List<T> results) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                results.Add(match);
            CollectDescendants(child, results);
        }
    }

    private void OnCircuitosGridScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0 && BandScrollViewer != null)
        {
            BandScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
        if (e.ViewportWidthChange != 0 || e.ExtentWidthChange != 0)
        {
            SyncBandColumnWidths();
        }
    }

    private void SyncBandColumnWidths()
    {
        if (CircuitosGrid == null || BandGrid == null || BandColNumero == null)
            return;

        double W(DataGridColumn? col) => (col != null && col.Visibility == Visibility.Visible) ? col.ActualWidth : 0;

        BandColNumero.Width = new GridLength(W(NumeroColumn), GridUnitType.Pixel);
        BandColSist.Width = new GridLength(W(ColSist), GridUnitType.Pixel);
        BandColAlim.Width = new GridLength(W(ColAlim), GridUnitType.Pixel);

        BandColUbicacion.Width = new GridLength(W(ColUbicacion), GridUnitType.Pixel);

        double wMedidas = W(ColLBand) + W(ColLPvc) + W(ColLPvc2);
        BandColMedidasEntrada.Width = new GridLength(wMedidas, GridUnitType.Pixel);

        double wBandeja = W(ColCondBand) + W(ColSeccBand) + W(ColTotBand);
        BandColTramoBandeja.Width = new GridLength(wBandeja, GridUnitType.Pixel);

        double wPvc = W(ColTipoPvc) + W(ColDuctoPvc) + W(ColTotalDucto)
            + W(ColTipoPvc2) + W(ColDuctoPvc2) + W(ColTotalDucto2)
            + W(ColCondPvc) + W(ColSeccPvc) + W(ColTotPvc);
        BandColTramoPvc.Width = new GridLength(wPvc, GridUnitType.Pixel);

        BandColMontaje.Width = new GridLength(0, GridUnitType.Pixel);
        BandColCajas.Width = new GridLength(0, GridUnitType.Pixel);

        double wTmf = W(ColTmfCant) + W(ColTmfMed);
        BandColTmf.Width = new GridLength(wTmf, GridUnitType.Pixel);

        BandColArtefactos.Width = new GridLength(W(ColArtefactos), GridUnitType.Pixel);
    }

    private double _lastTotalGridWidth = -1;
    private void OnCircuitosGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (CircuitosGrid == null || BandGrid == null) return;
        double currentTotal = 0;
        int i = 1;
        foreach (var col in CircuitosGrid.Columns)
        {
            if (col.Visibility == Visibility.Visible)
            {
                currentTotal += col.ActualWidth * i;
                i++;
            }
        }

        if (Math.Abs(currentTotal - _lastTotalGridWidth) > 0.5)
        {
            _lastTotalGridWidth = currentTotal;
            SyncBandColumnWidths();
        }
    }

    private void UpdateColumnsVisibility()
    {
        if (CircuitosGrid == null) return;

        var vm = Vm;
        bool isGenerales = vm == null || vm.IsPerspectiveGenerales;
        bool isAdv = vm != null && vm.IsAdvancedMode;

        // Columnas exclusivas de Servicios Comunes / Generales (no aplican a Departamentos)
        var generalesOnlyVis = isGenerales ? Visibility.Visible : Visibility.Collapsed;

        // Ubicación / Pisos (solo en Generales)
        if (ColUbicacion != null) ColUbicacion.Visibility = generalesOnlyVis;

        // Tramo Bandeja (solo en Generales)
        if (ColLBand != null) ColLBand.Visibility = generalesOnlyVis;
        if (ColCondBand != null) ColCondBand.Visibility = generalesOnlyVis;
        if (ColSeccBand != null) ColSeccBand.Visibility = generalesOnlyVis;
        if (ColTotBand != null) ColTotBand.Visibility = generalesOnlyVis;

        // Terminal Flexible (solo en Generales)
        if (ColTmfCant != null) ColTmfCant.Visibility = generalesOnlyVis;
        if (ColTmfMed != null) ColTmfMed.Visibility = generalesOnlyVis;

        // Tramo PVC / Conduit (visible SIEMPRE: en Generales y Deptos)
        if (ColTipoPvc != null) ColTipoPvc.Visibility = Visibility.Visible;
        if (ColDuctoPvc != null) ColDuctoPvc.Visibility = Visibility.Visible;
        if (ColTotalDucto != null) ColTotalDucto.Visibility = Visibility.Visible;
        if (ColCondPvc != null) ColCondPvc.Visibility = Visibility.Visible;
        if (ColSeccPvc != null) ColSeccPvc.Visibility = Visibility.Visible;
        if (ColTotPvc != null) ColTotPvc.Visibility = Visibility.Visible;

        // Elementos y accesorios (visible SIEMPRE: en Generales y Deptos)
        if (ColArtefactos != null) ColArtefactos.Visibility = Visibility.Visible;

        // Segundo Ducto (opcional con el botón +)
        var segundoDuctoVis = (vm != null && vm.ShowSegundoDucto) ? Visibility.Visible : Visibility.Collapsed;
        if (ColLPvc2 != null) ColLPvc2.Visibility = segundoDuctoVis;
        if (ColTipoPvc2 != null) ColTipoPvc2.Visibility = segundoDuctoVis;
        if (ColDuctoPvc2 != null) ColDuctoPvc2.Visibility = segundoDuctoVis;
        if (ColTotalDucto2 != null) ColTotalDucto2.Visibility = segundoDuctoVis;

        // Orden funcional solicitado para la planilla SC. Los elementos y accesorios
        // permanecen como una acción auxiliar al final, fuera de las 17 columnas.
        var orderedColumns = new DataGridColumn?[]
        {
            NumeroColumn, ColSist, ColAlim, ColUbicacion,
            ColLBand, ColLPvc, ColLPvc2,
            ColCondBand, ColSeccBand, ColTotBand,
            ColTipoPvc, ColDuctoPvc, ColTotalDucto,
            ColTipoPvc2, ColDuctoPvc2, ColTotalDucto2,
            ColCondPvc, ColSeccPvc, ColTotPvc,
            ColTmfCant, ColTmfMed, ColArtefactos
        };
        for (int i = 0; i < orderedColumns.Length; i++)
        {
            if (orderedColumns[i] != null)
                orderedColumns[i]!.DisplayIndex = i;
        }

        if (BandBorderUbicacion != null) BandBorderUbicacion.Visibility = generalesOnlyVis;
        if (BandBorderTramoBandeja != null) BandBorderTramoBandeja.Visibility = generalesOnlyVis;
        if (BandBorderTmf != null) BandBorderTmf.Visibility = generalesOnlyVis;
        if (BandBorderTramoPvc != null) BandBorderTramoPvc.Visibility = Visibility.Visible;

        if (BandLabelMedidasEntrada != null)
        {
            BandLabelMedidasEntrada.Text = isGenerales ? "MEDIDAS ENTRADA" : "L. PVC";
        }

        SyncBandColumnWidths();
    }

    public void FocusArtefactosSearch()
    {
        ArtefactosDrawerPanel?.FocusSearch();
    }
}
