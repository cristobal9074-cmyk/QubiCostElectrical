using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class ResumenScTomarDialog : Window
{
    private readonly ResumenScTomarViewModel vm;

    public ResumenScTomarDialog(
        IReadOnlyList<CuadroCargaSCRow> cuadros,
        CubicacionScConfig config,
        IReadOnlyList<ZonaProyecto> zonas)
    {
        InitializeComponent();

        vm = new ResumenScTomarViewModel(cuadros, config, zonas);
        vm.OnCerrar = () => DialogResult = false;
        DataContext = vm;

        Loaded += (_, _) =>
        {
            GenerarColumnas(zonas);
            FocusMaterialGrid();
        };
    }

    public MaterialConZonasVm? Result { get; private set; }

    private void GenerarColumnas(IReadOnlyList<ZonaProyecto> zonas)
    {
        MaterialGrid.Columns.Clear();

        var numStyle = new Style(typeof(TextBlock));
        numStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, FindResource("Font.Mono")));
        numStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0));
        numStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("Brush.Ink5")));
        numStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        numStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0)));

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "#",
            Width = 34,
            Binding = new Binding("CodigoDisplay"),
            ElementStyle = numStyle,
        });

        var codeCol = new DataGridTemplateColumn
        {
            Header = "Material",
            Width = 220,
            MinWidth = 160,
        };
        var codeFactory = new FrameworkElementFactory(typeof(TextBlock));
        codeFactory.SetValue(TextBlock.FontFamilyProperty, FindResource("Font.Mono"));
        codeFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        codeFactory.SetValue(TextBlock.ForegroundProperty, FindResource("Brush.Ink3"));
        codeFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        codeFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        codeFactory.SetBinding(TextBlock.TextProperty, new Binding("CodigoDisplay"));
        codeCol.CellTemplate = new DataTemplate { VisualTree = codeFactory };
        MaterialGrid.Columns.Add(codeCol);

        var udStyle = new Style(typeof(TextBlock));
        udStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 9.0));
        udStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
        udStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("Brush.Muted")));
        udStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        udStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "UD",
            Width = 34,
            Binding = new Binding("Unidad") { Mode = BindingMode.OneWay },
            ElementStyle = udStyle,
        });

        var zoneHeaderStyle = (Style)FindResource("ZoneColumnHeaderStyle");

        for (int i = 0; i < zonas.Count; i++)
        {
            var z = zonas[i];
            var col = new DataGridTemplateColumn
            {
                Header = z.Nombre,
                Width = 72,
                MinWidth = 56,
                HeaderStyle = zoneHeaderStyle,
            };

            var cellFactory = new FrameworkElementFactory(typeof(TextBlock));
            cellFactory.SetValue(TextBlock.FontFamilyProperty, FindResource("Font.Mono"));
            cellFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            cellFactory.SetValue(TextBlock.ForegroundProperty, FindResource("Brush.Ink2"));
            cellFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Right);
            cellFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            cellFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
            cellFactory.SetBinding(TextBlock.TextProperty, new Binding($"ZonaQties[{i}].Value")
            {
                Mode = BindingMode.OneWay,
                StringFormat = "{0:#,##0.##}",
                FallbackValue = "",
            });
            var cellTemplate = new DataTemplate { VisualTree = cellFactory };

            var editFactory = new FrameworkElementFactory(typeof(TextBox));
            editFactory.SetValue(TextBox.StyleProperty, FindResource("ZoneInputStyle"));
            editFactory.SetBinding(TextBox.TextProperty, new Binding($"ZonaQties[{i}].Text")
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                FallbackValue = "0",
            });
            editFactory.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(OnZoneTextChanged));
            editFactory.AddHandler(TextBox.PreviewKeyDownEvent, new KeyEventHandler(OnZoneKeyDown));
            var editTemplate = new DataTemplate { VisualTree = editFactory };

            col.CellTemplate = cellTemplate;
            col.CellEditingTemplate = editTemplate;
            MaterialGrid.Columns.Add(col);
        }

        var totalStyle = new Style(typeof(TextBlock));
        totalStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, FindResource("Font.Mono")));
        totalStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.0));
        totalStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        totalStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, FindResource("Brush.Ink")));
        totalStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        totalStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        totalStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0)));

        MaterialGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Total",
            Width = 68,
            Binding = new Binding("TotalDisplay") { Mode = BindingMode.OneWay },
            ElementStyle = totalStyle,
        });

        var actionCol = new DataGridTemplateColumn
        {
            Header = "",
            Width = 60,
            MinWidth = 60,
        };
        var actFactory = new FrameworkElementFactory(typeof(Button));
        actFactory.SetValue(Button.StyleProperty, FindResource("GridTomarButton"));
        actFactory.SetValue(Button.ContentProperty, "Tomar");
        actFactory.SetValue(Button.MarginProperty, new Thickness(2));
        actFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnTomarClick));
        actionCol.CellTemplate = new DataTemplate { VisualTree = actFactory };
        MaterialGrid.Columns.Add(actionCol);
    }

    private void FocusMaterialGrid()
    {
        if (MaterialGrid.Items.Count > 0)
        {
            MaterialGrid.Focus();
            var firstDataItem = MaterialGrid.Items.Cast<object>()
                .FirstOrDefault(i => i is MaterialConZonasVm);
            if (firstDataItem != null)
            {
                MaterialGrid.SelectedItem = firstDataItem;
                MaterialGrid.ScrollIntoView(firstDataItem);
            }
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OnCategoriaChipClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: CategoriaChipVm chip })
            vm.ActiveCategoria = chip.Nombre;
    }

    private void OnSidebarCategoriaClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoriaChipVm chip })
            vm.ActiveCategoria = chip.Nombre;
    }

    private void OnPreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
    }

    private void OnZoneTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: MaterialConZonasVm row })
            row.RecalcTotal();
    }

    private void OnZoneKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox tb)
        {
            MaterialGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            e.Handled = true;
        }
    }

    private void OnTomarClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MaterialConZonasVm row })
            TakeMaterial(row);
    }

    private void TakeMaterial(MaterialConZonasVm row)
    {
        Result = row;
        DialogResult = true;
    }

    private void OnFooterTomarClick(object sender, RoutedEventArgs e)
    {
        if (vm.SelectedRow is MaterialConZonasVm row)
            TakeMaterial(row);
    }
}
