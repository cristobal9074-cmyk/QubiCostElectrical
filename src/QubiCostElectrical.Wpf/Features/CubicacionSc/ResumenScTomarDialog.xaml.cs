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

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Material",
            Width = 260,
            MinWidth = 180,
            Binding = new Binding(nameof(MaterialConZonasVm.CodigoDisplay)) { Mode = BindingMode.OneWay },
            IsReadOnly = true
        });

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "UD",
            Width = 40,
            Binding = new Binding(nameof(MaterialConZonasVm.Unidad)) { Mode = BindingMode.OneWay },
            IsReadOnly = true
        });

        for (var i = 0; i < zonasProyecto.Count; i++)
        {
            var index = i;
            var zona = zonasProyecto[i];
            var column = new DataGridTemplateColumn
            {
                Header = zona.Nombre,
                Width = 72,
                MinWidth = 56
            };

            var displayFactory = new FrameworkElementFactory(typeof(TextBlock));
            displayFactory.SetBinding(TextBlock.TextProperty, new Binding($"ZonaQties[{index}].Value")
            {
                Mode = BindingMode.OneWay,
                StringFormat = "{0:#,##0.##}",
                FallbackValue = ""
            });
            displayFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            displayFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            column.CellTemplate = new DataTemplate { VisualTree = displayFactory };

            var editFactory = new FrameworkElementFactory(typeof(TextBox));
            editFactory.SetBinding(TextBox.TextProperty, new Binding($"ZonaQties[{index}].Text")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                FallbackValue = "0"
            });
            editFactory.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnZoneTextChanged));
            editFactory.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnZoneKeyDown));
            column.CellEditingTemplate = new DataTemplate { VisualTree = editFactory };

            MaterialGrid.Columns.Add(column);
        }

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Total",
            Width = 72,
            Binding = new Binding(nameof(MaterialConZonasVm.TotalDisplay)) { Mode = BindingMode.OneWay },
            IsReadOnly = true
        });

        var tomarColumn = new DataGridTemplateColumn { Header = "", Width = 68, MinWidth = 68 };
        var btnFactory = new FrameworkElementFactory(typeof(Button));
        btnFactory.SetValue(ContentControl.ContentProperty, "Tomar");
        btnFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        btnFactory.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnTomarClick));
        tomarColumn.CellTemplate = new DataTemplate { VisualTree = btnFactory };
        MaterialGrid.Columns.Add(tomarColumn);

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
            MaterialGrid.SelectedItem = first;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OnSidebarCategoriaClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoriaChipVm chip })
            vm.ActiveCategoria = chip.Nombre;
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
        if (sender is Button { DataContext: MaterialConZonasVm row })
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
