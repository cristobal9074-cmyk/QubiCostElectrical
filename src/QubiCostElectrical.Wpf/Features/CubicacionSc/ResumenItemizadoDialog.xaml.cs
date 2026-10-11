using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FocusAndScrollToSelected();
    }

    private void FocusAndScrollToSelected()
    {
        var target = vm.SelectedRow ?? vm.FlatRows.FirstOrDefault(r => !r.EsSeccion) ?? vm.FlatRows.FirstOrDefault();
        if (target != null)
        {
            vm.SelectedRow = target;
            PartidaGrid.SelectedItem = target;
            PartidaGrid.SelectedIndex = vm.FlatRows.IndexOf(target);
            PartidaGrid.ScrollIntoView(target);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (vm.SelectedRow != null)
            {
                PartidaGrid.SelectedItem = vm.SelectedRow;
                PartidaGrid.SelectedIndex = vm.FlatRows.IndexOf(vm.SelectedRow);
                PartidaGrid.ScrollIntoView(vm.SelectedRow);
                if (PartidaGrid.Columns.Count > 0)
                {
                    PartidaGrid.CurrentCell = new DataGridCellInfo(vm.SelectedRow, PartidaGrid.Columns[0]);
                }
            }
            PartidaGrid.Focus();
            Keyboard.Focus(PartidaGrid);
            FocusManager.SetFocusedElement(this, PartidaGrid);
        }));
    }

    private void MoveSelection(int delta)
    {
        if (vm.FlatRows.Count == 0) return;

        int currentIndex = vm.SelectedRow != null ? vm.FlatRows.IndexOf(vm.SelectedRow) : -1;
        if (currentIndex < 0)
        {
            currentIndex = PartidaGrid.SelectedIndex >= 0 ? PartidaGrid.SelectedIndex : -1;
        }

        int nextIndex = currentIndex + delta;
        if (nextIndex < 0) nextIndex = 0;
        if (nextIndex >= vm.FlatRows.Count) nextIndex = vm.FlatRows.Count - 1;

        var target = vm.FlatRows[nextIndex];
        vm.SelectedRow = target;
        PartidaGrid.SelectedItem = target;
        PartidaGrid.SelectedIndex = nextIndex;
        PartidaGrid.ScrollIntoView(target);

        if (PartidaGrid.Columns.Count > 0)
        {
            PartidaGrid.CurrentCell = new DataGridCellInfo(target, PartidaGrid.Columns[0]);
        }
    }

    private void MoveToEdge(bool toTop)
    {
        if (vm.FlatRows.Count == 0) return;
        int nextIndex = toTop ? 0 : vm.FlatRows.Count - 1;
        var target = vm.FlatRows[nextIndex];
        vm.SelectedRow = target;
        PartidaGrid.SelectedItem = target;
        PartidaGrid.SelectedIndex = nextIndex;
        PartidaGrid.ScrollIntoView(target);

        if (PartidaGrid.Columns.Count > 0)
        {
            PartidaGrid.CurrentCell = new DataGridCellInfo(target, PartidaGrid.Columns[0]);
        }
    }

    private void ConfirmSelection()
    {
        var target = vm.SelectedRow ?? (PartidaGrid.SelectedItem as ItemizadoPartidaVm)
                     ?? vm.FlatRows.FirstOrDefault(r => !r.EsSeccion) ?? vm.FlatRows.FirstOrDefault();

        if (target != null)
        {
            if (target.EsSeccion)
            {
                var child = vm.FlatRows.SkipWhile(r => r != target).Skip(1).FirstOrDefault(r => !r.EsSeccion);
                if (child != null)
                    target = child;
            }

            if (!target.EsSeccion)
            {
                NavigateTo(target);
            }
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            if (SearchBox.IsKeyboardFocusWithin && !string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = string.Empty;
                FocusAndScrollToSelected();
                e.Handled = true;
                return;
            }

            DialogResult = false;
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && (key == Key.B || key == Key.F))
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (key == Key.Down && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Alt))
        {
            MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (key == Key.Up && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Alt))
        {
            MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (key == Key.PageDown)
        {
            MoveSelection(10);
            e.Handled = true;
            return;
        }

        if (key == Key.PageUp)
        {
            MoveSelection(-10);
            e.Handled = true;
            return;
        }

        if (key == Key.Home && Keyboard.FocusedElement is not TextBox)
        {
            MoveToEdge(true);
            e.Handled = true;
            return;
        }

        if (key == Key.End && Keyboard.FocusedElement is not TextBox)
        {
            MoveToEdge(false);
            e.Handled = true;
            return;
        }

        if (key == Key.Enter && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Alt))
        {
            ConfirmSelection();
            e.Handled = true;
            return;
        }

        if (e.IsRepeat) return;

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
            && vm.SelectedRow is { EsSeccion: false })
        {
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

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Down)
        {
            MoveSelection(1);
            e.Handled = true;
        }
        else if (key == Key.Up)
        {
            MoveSelection(-1);
            e.Handled = true;
        }
        else if (key == Key.Enter)
        {
            ConfirmSelection();
            e.Handled = true;
        }
    }

    private void OnFilterChipClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FilterChipVm chip })
        {
            vm.ActiveFilter = chip.Nombre;
            FocusAndScrollToSelected();
        }
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void NavigateTo(ItemizadoPartidaVm row)
    {
        Result = row;
        DialogResult = true;
    }
}
