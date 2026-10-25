using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.Cubicacion;

public partial class ZonasProyectoDialog : Window
{
    public ZonasProyectoViewModel ViewModel { get; }
    private string? _pendingInitialText;

    public ZonasProyectoDialog(IEnumerable<ZonaProyecto> zonas)
    {
        InitializeComponent();

        ViewModel = new ZonasProyectoViewModel();
        ViewModel.CargarZonas(zonas);
        DataContext = ViewModel;

        Loaded += OnWindowLoaded;
    }

    /// <summary>Zonas resultantes; solo válido cuando DialogResult es true.</summary>
    public List<ZonaProyecto> Zonas { get; } = new();

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Rows.Count > 0)
        {
            ZonasGrid.SelectedIndex = 0;
            ZonasGrid.Focus();
        }
        else
        {
            SearchBox.Focus();
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+B o Ctrl+F: Enfocar buscador Spotlight
        if ((e.Key == Key.B || e.Key == Key.F) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        // Alt+P: Catálogo de Presets de Industria
        if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            OnPresetsIndustriaClick(sender, e);
            e.Handled = true;
            return;
        }

        // Ctrl+S: Guardar como Preset
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && !IsEditingCell())
        {
            OnGuardarComoPresetClick(sender, e);
            e.Handled = true;
            return;
        }

        // Ctrl + o NumPad + o Insert o Ctrl+N: Nueva zona correlativa
        bool isCtrlPlus = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                          (e.Key == Key.OemPlus || e.Key == Key.Add);
        bool isInsertOrCtrlN = (e.Key == Key.Insert && !SearchBox.IsFocused) ||
                               (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && !SearchBox.IsFocused);

        if (isCtrlPlus || isInsertOrCtrlN)
        {
            if (IsEditingCell())
            {
                ZonasGrid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            OnAgregarClick(sender, e);
            e.Handled = true;
            return;
        }

        // Ctrl - o NumPad - o Supr/Delete: Quitar zona seleccionada
        bool isCtrlMinus = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                           (e.Key == Key.OemMinus || e.Key == Key.Subtract);
        bool isDelete = e.Key == Key.Delete && !IsEditingCell() && SearchBox.IsFocused == false;

        if (isCtrlMinus || isDelete)
        {
            if (IsEditingCell())
            {
                ZonasGrid.CancelEdit(DataGridEditingUnit.Row);
            }
            EjecutarQuitarZona();
            e.Handled = true;
            return;
        }

        // Reordenar con Alt+Up / Alt+Down
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            if (e.Key == Key.Up && ViewModel.CanMoveUp)
            {
                ViewModel.MoveSelected(-1);
                ZonasGrid.ScrollIntoView(ViewModel.SelectedRow);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down && ViewModel.CanMoveDown)
            {
                ViewModel.MoveSelected(1);
                ZonasGrid.ScrollIntoView(ViewModel.SelectedRow);
                e.Handled = true;
                return;
            }
        }

        // Escape: Si el popup de rangos está abierto, cerrarlo
        if (e.Key == Key.Escape && RangoPisosPopup.IsOpen)
        {
            RangoPisosPopup.IsOpen = false;
            ZonasGrid.Focus();
            e.Handled = true;
            return;
        }

        // Escape: Si hay búsqueda, limpiar búsqueda y enfocar grid; si no, cerrar diálogo
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(ViewModel.SearchText))
            {
                ViewModel.SearchText = string.Empty;
                ZonasGrid.Focus();
                e.Handled = true;
                return;
            }
        }
    }

    private bool IsEditingCell()
    {
        var cell = ZonasGrid.CurrentCell;
        if (!cell.IsValid) return false;
        var row = ZonasGrid.ItemContainerGenerator.ContainerFromItem(cell.Item) as DataGridRow;
        return row?.IsEditing == true;
    }

    private List<ZonaEditRowVm> GetVisibleRows()
    {
        return ZonasGrid.Items.OfType<ZonaEditRowVm>().ToList();
    }

    private void EjecutarQuitarZona()
    {
        var current = ViewModel.SelectedRow;
        if (current == null) return;

        var visible = GetVisibleRows();
        int currentIndex = visible.IndexOf(current);
        ViewModel.QuitarZona();

        var updatedVisible = GetVisibleRows();
        if (updatedVisible.Count > 0)
        {
            int nextIndex = Math.Clamp(currentIndex, 0, updatedVisible.Count - 1);
            var nextRow = updatedVisible[nextIndex];
            ViewModel.SelectedRow = nextRow;
            ZonasGrid.SelectedItem = nextRow;
            ZonasGrid.ScrollIntoView(nextRow);
        }
        ZonasGrid.Focus();
    }

    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        var row = ViewModel.AgregarZona(string.Empty);
        ViewModel.SelectedRow = row;
        ZonasGrid.SelectedItem = row;
        ZonasGrid.ScrollIntoView(row);

        Dispatcher.InvokeAsync(() =>
        {
            BeginEditNombre();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Type-to-edit: si el usuario escribe una letra o número mientras navega por la grilla,
    /// entra inmediatamente en modo de edición con ese carácter pre-cargado.
    /// </summary>
    private void OnZonasGridPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (IsEditingCell() || SearchBox.IsFocused) return;
        if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;

        var visible = GetVisibleRows();
        if (ViewModel.SelectedRow == null)
        {
            if (visible.Count > 0)
            {
                ViewModel.SelectedRow = visible[0];
            }
            else
            {
                var newRow = ViewModel.AgregarZona(e.Text);
                ViewModel.SelectedRow = newRow;
                ZonasGrid.SelectedItem = newRow;
                BeginEditNombre(e.Text);
                e.Handled = true;
                return;
            }
        }

        BeginEditNombre(e.Text);
        e.Handled = true;
    }

    /// <summary>
    /// Navegación rápida con teclado dentro de la grilla:
    /// Enter comitea y baja de fila (o crea siguiente piso si está en la última fila).
    /// Flechas Arriba/Abajo comitean y cambian de fila sin trabas.
    /// </summary>
    private void OnZonasGridPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (IsEditingCell())
            {
                ZonasGrid.CommitEdit(DataGridEditingUnit.Row, true);
                e.Handled = true;

                var current = ViewModel.SelectedRow;
                if (current == null) return;

                var visible = GetVisibleRows();
                int currentIndex = visible.IndexOf(current);
                if (currentIndex >= 0 && currentIndex < visible.Count - 1)
                {
                    var nextRow = visible[currentIndex + 1];
                    ViewModel.SelectedRow = nextRow;
                    ZonasGrid.SelectedItem = nextRow;
                    ZonasGrid.ScrollIntoView(nextRow);
                    Dispatcher.InvokeAsync(() => BeginEditNombre(), System.Windows.Threading.DispatcherPriority.Background);
                }
                else if (currentIndex == visible.Count - 1)
                {
                    // Al final de la grilla, Enter crea el siguiente piso correlativo inmediatamente
                    OnAgregarClick(sender, e);
                }
            }
            else
            {
                BeginEditNombre();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.F2 && !IsEditingCell())
        {
            BeginEditNombre();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && IsEditingCell())
        {
            ZonasGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var current = ViewModel.SelectedRow;
            if (current != null)
            {
                var visible = GetVisibleRows();
                int currentIndex = visible.IndexOf(current);
                if (currentIndex > 0)
                {
                    var prevRow = visible[currentIndex - 1];
                    ViewModel.SelectedRow = prevRow;
                    ZonasGrid.SelectedItem = prevRow;
                    ZonasGrid.ScrollIntoView(prevRow);
                    Dispatcher.InvokeAsync(() => BeginEditNombre(), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && IsEditingCell())
        {
            ZonasGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var current = ViewModel.SelectedRow;
            if (current != null)
            {
                var visible = GetVisibleRows();
                int currentIndex = visible.IndexOf(current);
                if (currentIndex >= 0 && currentIndex < visible.Count - 1)
                {
                    var nextRow = visible[currentIndex + 1];
                    ViewModel.SelectedRow = nextRow;
                    ZonasGrid.SelectedItem = nextRow;
                    ZonasGrid.ScrollIntoView(nextRow);
                    Dispatcher.InvokeAsync(() => BeginEditNombre(), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            e.Handled = true;
            return;
        }
    }

    private void BeginEditNombre(string? initialText = null)
    {
        var visible = GetVisibleRows();
        if (ViewModel.SelectedRow == null && visible.Count > 0)
        {
            ViewModel.SelectedRow = visible[0];
        }

        if (ViewModel.SelectedRow == null) return;

        _pendingInitialText = initialText;
        var row = ViewModel.SelectedRow;
        ZonasGrid.SelectedItem = row;
        ZonasGrid.ScrollIntoView(row);

        if (ZonasGrid.Columns.Count > 1)
        {
            ZonasGrid.CurrentCell = new DataGridCellInfo(row, ZonasGrid.Columns[1]);
            ZonasGrid.BeginEdit();
        }
    }

    private void OnZonaNombreInputLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            if (_pendingInitialText != null)
            {
                tb.Text = _pendingInitialText;
                tb.CaretIndex = tb.Text.Length;
                _pendingInitialText = null;
            }
            else
            {
                tb.SelectAll();
            }
            tb.Focus();
        }
    }

    private void OnRangoPisosClick(object sender, RoutedEventArgs e)
    {
        RangoPisosPopup.IsOpen = true;
        TxtRangoHasta.Focus();
        TxtRangoHasta.SelectAll();
    }

    private void OnRangoPisosCancelarClick(object sender, RoutedEventArgs e)
    {
        RangoPisosPopup.IsOpen = false;
        ZonasGrid.Focus();
    }

    private void OnGenerarRangoConfirmClick(object sender, RoutedEventArgs e)
    {
        string prefijo = string.IsNullOrWhiteSpace(CmbRangoPrefijo.Text) ? "Piso " : CmbRangoPrefijo.Text;
        if (!int.TryParse(TxtRangoDesde.Text.Trim(), out int desde))
            desde = 1;
        if (!int.TryParse(TxtRangoHasta.Text.Trim(), out int hasta))
            hasta = 10;

        bool reemplazar = ChkRangoReemplazar.IsChecked == true;
        ViewModel.GenerarRangoPisos(prefijo, desde, hasta, reemplazar);

        RangoPisosPopup.IsOpen = false;

        if (ViewModel.Rows.Count > 0)
        {
            ZonasGrid.SelectedIndex = 0;
            ZonasGrid.Focus();
        }
    }

    private void OnPresetsIndustriaClick(object sender, RoutedEventArgs e)
    {
        ZonasGrid.CancelEdit(DataGridEditingUnit.Cell);
        ZonasGrid.CancelEdit(DataGridEditingUnit.Row);

        var dialog = new PresetsZonasDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.ZonasSeleccionadas.Count > 0)
        {
            if (dialog.ModoReemplazar)
            {
                ViewModel.Rows.Clear();
            }

            foreach (var nombreZona in dialog.ZonasSeleccionadas)
            {
                ViewModel.AgregarZona(nombreZona);
            }

            if (ViewModel.Rows.Count > 0)
            {
                ZonasGrid.SelectedIndex = 0;
                ZonasGrid.Focus();
            }
        }
    }

    private void OnGuardarComoPresetClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Rows.Count == 0)
        {
            WpfDialogService.Info("Sin zonas", "No hay zonas en la lista para guardar como preset.");
            return;
        }

        string? nombre = WpfDialogService.PromptText("Guardar Estructura como Preset",
            "Ingrese el nombre identificador para esta plantilla de zonas:\n(Ej: Estándar Faena Cordillera, Galpón Frío 5000m2)", "");
        if (string.IsNullOrWhiteSpace(nombre))
            return;

        string? categoria = WpfDialogService.PromptText("Categoría de Industria",
            "Ingrese la categoría de la plantilla:\n(Minería, Solar, Industrial, Edificación, Hospitalario, Personalizado)", "Personalizado");
        if (string.IsNullOrWhiteSpace(categoria))
            categoria = "Personalizado";

        var zonasNombres = ViewModel.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Nombre))
            .Select(r => r.Nombre.Trim())
            .ToList();

        if (zonasNombres.Count == 0)
        {
            WpfDialogService.Info("Sin zonas válidas", "Complete el nombre de al menos una zona antes de guardar.");
            return;
        }

        var store = new ZonaStructurePresetStore();
        var preset = new ZonaStructurePreset
        {
            Name = nombre.Trim(),
            Category = categoria.Trim(),
            Icon = "⭐",
            Description = $"Preset personalizado con {zonasNombres.Count} zonas guardado desde el proyecto.",
            Zonas = zonasNombres,
            IsFactory = false
        };

        store.SavePreset(preset);

        WpfDialogService.Info("Preset Guardado",
            $"La estructura \"{nombre}\" ({zonasNombres.Count} zonas) ha sido guardada en la memoria local de la aplicación y estará disponible en todos sus proyectos.");
    }

    private void OnQuitarClick(object sender, RoutedEventArgs e)
    {
        EjecutarQuitarZona();
    }

    private void OnSubirClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanMoveUp)
        {
            ViewModel.MoveSelected(-1);
            if (ViewModel.SelectedRow != null)
                ZonasGrid.ScrollIntoView(ViewModel.SelectedRow);
        }
    }

    private void OnBajarClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanMoveDown)
        {
            ViewModel.MoveSelected(1);
            if (ViewModel.SelectedRow != null)
                ZonasGrid.ScrollIntoView(ViewModel.SelectedRow);
        }
    }

    private void OnToggleActivaClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ZonaEditRowVm row)
        {
            row.ToggleActiva();
        }
    }

    private void OnRowSubirClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ZonaEditRowVm row)
        {
            ViewModel.MoveZona(row, -1);
            ZonasGrid.ScrollIntoView(row);
        }
    }

    private void OnRowBajarClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ZonaEditRowVm row)
        {
            ViewModel.MoveZona(row, 1);
            ZonasGrid.ScrollIntoView(row);
        }
    }

    private void OnRowQuitarClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ZonaEditRowVm row)
        {
            ViewModel.QuitarZona(row);
        }
    }

    private void OnActivarTodasClick(object sender, RoutedEventArgs e) => ViewModel.SetActivasTodas(true);

    private void OnDesactivarTodasClick(object sender, RoutedEventArgs e) => ViewModel.SetActivasTodas(false);

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        ZonasGrid.CommitEdit(DataGridEditingUnit.Row, true);

        var vacias = ViewModel.Rows.Any(r => string.IsNullOrWhiteSpace(r.Nombre));
        if (vacias)
        {
            WpfDialogService.Info("Zonas sin nombre",
                "Hay zonas sin nombre. Complete el nombre o quítelas antes de guardar.");
            return;
        }

        var duplicada = ViewModel.Rows
            .GroupBy(r => r.Nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicada != null)
        {
            WpfDialogService.Info("Zona duplicada",
                $"La zona \"{duplicada.Key}\" está repetida. Cada zona debe tener un nombre único.");
            return;
        }

        Zonas.Clear();
        Zonas.AddRange(ViewModel.ObtenerZonasResultantes());
        DialogResult = true;
    }
}
