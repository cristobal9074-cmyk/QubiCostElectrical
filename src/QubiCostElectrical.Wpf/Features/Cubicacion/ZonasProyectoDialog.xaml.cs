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

        // Insert o Ctrl+N: Nueva zona
        if (e.Key == Key.Insert || (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
        {
            if (!IsEditingCell())
            {
                OnAgregarClick(sender, e);
                e.Handled = true;
                return;
            }
        }

        // Reordenar con Alt+Up / Alt+Down o Ctrl+Up / Ctrl+Down
        if ((Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) != ModifierKeys.None)
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

        // Supr / Delete: Quitar zona si no se está editando celda
        if (e.Key == Key.Delete && !IsEditingCell() && SearchBox.IsFocused == false)
        {
            if (ViewModel.SelectedRow != null)
            {
                ViewModel.QuitarZona();
                e.Handled = true;
                return;
            }
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

    private void OnAgregarClick(object sender, RoutedEventArgs e)
    {
        var row = ViewModel.AgregarZona(string.Empty);
        ZonasGrid.SelectedItem = row;
        ZonasGrid.ScrollIntoView(row);
        Dispatcher.InvokeAsync(() =>
        {
            if (ZonasGrid.Columns.Count > 1)
            {
                ZonasGrid.CurrentCell = new DataGridCellInfo(row, ZonasGrid.Columns[1]);
                ZonasGrid.BeginEdit();
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
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
        if (ViewModel.SelectedRow != null)
            ViewModel.QuitarZona();
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
