using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class ResumenItemizadoDialog : Window
{
    private readonly ResumenItemizadoViewModel vm;

    public ItemizadoPartidaVm? Result { get; private set; }
    public bool ShouldAddResource { get; private set; }
    public bool ShouldDeleteRows { get; private set; }

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

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && vm.SelectedRow is { EsSeccion: false })
        {
            NavigateTo(vm.SelectedRow);
            e.Handled = true;
            return;
        }

        if (e.IsRepeat) return;

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && vm.SelectedRow is { EsSeccion: false })
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.N)
            {
                ShouldAddResource = true;
                NavigateTo(vm.SelectedRow);
                e.Handled = true;
            }
            else if (key == Key.Delete)
            {
                ShouldDeleteRows = true;
                NavigateTo(vm.SelectedRow);
                e.Handled = true;
            }
        }
    }

    private void OnFilterChipClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FilterChipVm chip })
            vm.ActiveFilter = chip.Nombre;
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PartidaGrid.SelectedItem is ItemizadoPartidaVm { EsSeccion: false } row)
            NavigateTo(row);
    }

    private void NavigateTo(ItemizadoPartidaVm row)
    {
        Result = row;
        DialogResult = true;
    }
}
