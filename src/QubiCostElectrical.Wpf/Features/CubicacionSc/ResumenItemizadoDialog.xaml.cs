using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class ResumenItemizadoDialog : Window
{
    private readonly ResumenItemizadoViewModel vm;

    public ResumenItemizadoDialog(IReadOnlyList<ItemizadoPartidaVm> items, string? initialId = null)
    {
        InitializeComponent();

        vm = new ResumenItemizadoViewModel(items, initialId);
        vm.OnCerrar = () => DialogResult = false;
        DataContext = vm;

        Loaded += (_, _) =>
        {
            PartidaGrid.Focus();
            if (PartidaGrid.Items.Count > 0 && vm.SelectedRow == null)
                PartidaGrid.SelectedIndex = 0;
        };
    }

    public ItemizadoPartidaVm? Result { get; private set; }
    public bool ShouldAddResource { get; private set; }
    public bool ShouldDeleteRows { get; private set; }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && vm.SelectedRow != null && !vm.SelectedRow.EsSeccion)
        {
            NavigateTo(vm.SelectedRow);
            e.Handled = true;
            return;
        }

        if (e.IsRepeat) return;

        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (!ctrl || !shift) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (vm.SelectedRow == null || vm.SelectedRow.EsSeccion) return;

        if (key == Key.A)
        {
            ShouldAddResource = true;
            NavigateTo(vm.SelectedRow);
            e.Handled = true;
            return;
        }

        if (key == Key.D)
        {
            ShouldDeleteRows = true;
            NavigateTo(vm.SelectedRow);
            e.Handled = true;
        }
    }

    private void OnFilterChipClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { DataContext: FilterChipVm chip })
            vm.ActiveFilter = chip.Nombre;
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PartidaGrid.SelectedItem is ItemizadoPartidaVm row && !row.EsSeccion)
            NavigateTo(row);
    }

    private void NavigateTo(ItemizadoPartidaVm row)
    {
        Result = row;
        DialogResult = true;
    }
}
