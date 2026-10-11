using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class ResumenScTomarDialog : Window
{
    private readonly ResumenScTomarViewModel vm;
    private readonly IReadOnlyList<ZonaProyecto> zonas;

    public MaterialConZonasVm? Result { get; private set; }

    public ResumenScTomarDialog(IReadOnlyList<CuadroCargaSCRow> cuadros, CubicacionScConfig config, IReadOnlyList<ZonaProyecto> zonas)
    {
        InitializeComponent();
        this.zonas = zonas;
        vm = new ResumenScTomarViewModel(cuadros, config, zonas);
        vm.OnCerrar = () => DialogResult = false;
        DataContext = vm;
        Loaded += (_, _) =>
        {
            GenerarColumnas(zonas);
            FocusMaterialGrid();
        };
    }

    private void GenerarColumnas(IReadOnlyList<ZonaProyecto> zonasProyecto)
    {
        MaterialGrid.Columns.Clear();

        // 1. Columna Material
        var materialStyle = new Style(typeof(TextBlock));
        materialStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        materialStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
        materialStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        materialStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(10, 0, 6, 0)));

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "MATERIAL",
            Width = 280,
            MinWidth = 200,
            Binding = new Binding(nameof(MaterialConZonasVm.CodigoDisplay)) { Mode = BindingMode.OneWay },
            IsReadOnly = true,
            ElementStyle = materialStyle
        });

        // 2. Columna Unidad (UD)
        var udStyle = new Style(typeof(TextBlock));
        udStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
        udStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        udStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
        udStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        udStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("Brush.Muted")));

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "UD",
            Width = 50,
            Binding = new Binding(nameof(MaterialConZonasVm.Unidad)) { Mode = BindingMode.OneWay },
            IsReadOnly = true,
            ElementStyle = udStyle
        });

        // 3. Columnas Dinámicas por Zona
        for (var i = 0; i < zonasProyecto.Count; i++)
        {
            var index = i;
            var zona = zonasProyecto[i];
            var column = new DataGridTemplateColumn
            {
                Header = zona.Nombre.ToUpperInvariant(),
                Width = 80,
                MinWidth = 65
            };

            var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
            displayFactory.SetBinding(TextBlock.TextProperty, new Binding($"ZonaQties[{index}].Value")
            {
                Mode = BindingMode.OneWay,
                StringFormat = "{0:#,##0.##}",
                FallbackValue = ""
            });
            displayFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            displayFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            displayFactory.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas, Segoe UI Mono, monospace"));
            displayFactory.SetValue(TextBlock.FontSizeProperty, 11.5);
            displayFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            column.CellTemplate = new DataTemplate { VisualTree = displayFactory };

            var editFactory = new FrameworkElementFactory(typeof(TextBox));
            editFactory.SetBinding(TextBox.TextProperty, new Binding($"ZonaQties[{index}].Text")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                FallbackValue = "0"
            });
            editFactory.SetValue(TextBox.TextAlignmentProperty, TextAlignment.Right);
            editFactory.SetValue(TextBox.FontFamilyProperty, new FontFamily("Consolas, Segoe UI Mono, monospace"));
            editFactory.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnZoneTextChanged));
            editFactory.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnZoneKeyDown));
            column.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };

            MaterialGrid.Columns.Add(column);
        }

        // 4. Columna Total
        var totalStyle = new Style(typeof(TextBlock));
        totalStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
        totalStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        totalStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas, Segoe UI Mono, monospace")));
        totalStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
        totalStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.5));
        totalStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("Brush.Ink")));
        totalStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0)));

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "TOTAL",
            Width = 85,
            Binding = new Binding(nameof(MaterialConZonasVm.TotalDisplay)) { Mode = BindingMode.OneWay },
            IsReadOnly = true,
            ElementStyle = totalStyle
        });

        // 5. Columna Acción (Tomar)
        if (Resources["TomarButtonTemplate"] is DataTemplate tomarTemplate)
        {
            MaterialGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "",
                Width = 85,
                MinWidth = 75,
                CellTemplate = tomarTemplate
            });
        }

        MaterialGrid.PreparingCellForEdit += (_, e) =>
        {
            if (e.EditingElement is TextBox textBox)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        };
    }

    private void FocusMaterialGrid()
    {
        if (MaterialGrid.Items.Count == 0) return;
        MaterialGrid.Focus();
        var first = MaterialGrid.Items.Cast<object>().FirstOrDefault(i => i is MaterialConZonasVm);
        if (first != null)
        {
            MaterialGrid.SelectedItem = first;
            MaterialGrid.ScrollIntoView(first);
        }
    }

    private void ConfirmSelection()
    {
        if (vm.SelectedRow is MaterialConZonasVm material)
        {
            TakeMaterial(material);
            return;
        }

        if (vm.SelectedRow is GroupHeaderRowVm groupHeader)
        {
            var child = vm.FlatRows.SkipWhile(r => r != groupHeader).Skip(1).OfType<MaterialConZonasVm>().FirstOrDefault();
            if (child != null)
            {
                vm.SelectedRow = child;
                MaterialGrid.SelectedItem = child;
                MaterialGrid.ScrollIntoView(child);
            }
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape: limpiar búsqueda o cerrar diálogo
        if (key == Key.Escape)
        {
            if (SearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                FocusMaterialGrid();
                e.Handled = true;
                return;
            }

            DialogResult = false;
            e.Handled = true;
            return;
        }

        // Ctrl+B o Ctrl+F: enfocar barra de búsqueda
        if (Keyboard.Modifiers == ModifierKeys.Control && (key == Key.B || key == Key.F))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        // Enter: confirmar selección si no se está editando una celda
        if (key == Key.Enter)
        {
            if (SearchBox.IsKeyboardFocusWithin)
            {
                FocusMaterialGrid();
                e.Handled = true;
                return;
            }

            // Si está enfocado en una caja de texto dentro de una celda, dejar que OnZoneKeyDown actúe
            if (Keyboard.FocusedElement is TextBox)
            {
                return;
            }

            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Down || key == Key.Enter)
        {
            FocusMaterialGrid();
            e.Handled = true;
        }
    }

    private void OnSidebarCategoriaClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoriaChipVm chip })
        {
            vm.ActiveCategoria = chip.Nombre;
            FocusMaterialGrid();
        }
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void OnZoneTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: MaterialConZonasVm row })
            row.RecalcTotal();
    }

    private void OnZoneKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox)
        {
            MaterialGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            e.Handled = true;
        }
    }

    private void MaterialGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is DataGrid grid
            && DataGridExcelNavigation.TryBeginEditFromChar(grid, e.Text))
        {
            e.Handled = true;
        }
    }

    private void OnTomarClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MaterialConZonasVm row })
            TakeMaterial(row);
    }

    private void OnFooterTomarClick(object sender, RoutedEventArgs e)
    {
        if (vm.SelectedRow is MaterialConZonasVm row)
            TakeMaterial(row);
    }

    private void TakeMaterial(MaterialConZonasVm row)
    {
        Result = row;
        DialogResult = true;
    }
}
