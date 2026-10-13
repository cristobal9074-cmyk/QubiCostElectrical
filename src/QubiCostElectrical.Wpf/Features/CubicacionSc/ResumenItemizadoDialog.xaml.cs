using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf.Features.CubicacionSc;

public partial class ResumenItemizadoDialog : Window
{
    private readonly ResumenItemizadoViewModel vm;

    public ItemizadoPartidaVm? Result { get; private set; }
    public bool ShouldAddResource { get; private set; }
    public bool ShouldDeleteRows { get; private set; }
    public bool HasMutations => vm.HasMutations;
    public ItemizadoPartidaVm? SelectedRow => vm.SelectedRow;

    public ResumenItemizadoDialog(
        IReadOnlyList<ItemizadoPartidaVm> items,
        string? initialId = null,
        BudgetProjectSession? session = null,
        ItemizadoPresenter? presenter = null,
        WpfUndoService? undo = null)
    {
        InitializeComponent();
        vm = new ResumenItemizadoViewModel(items, initialId, session, presenter, undo);
        vm.OnCerrar = () => DialogResult = false;
        vm.RequestFocusQuickEdit += FocusQuickEdit;
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
            int idx = vm.FlatRows.IndexOf(target);
            if (idx >= 0)
            {
                PartidaGrid.SelectedIndex = idx;
                PartidaGrid.ScrollIntoView(target);
            }
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            PartidaGrid.Focus();
            Keyboard.Focus(PartidaGrid);
        }));
    }

    private void FocusQuickEdit()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (vm.SelectedRow is { EsSeccion: false })
            {
                EditCodeBox.Focus();
                EditCodeBox.SelectAll();
            }
        }));
    }

    private void MoveSelection(int delta)
    {
        int count = vm.FlatRows.Count;
        if (count == 0) return;

        int currentIndex = PartidaGrid.SelectedIndex;
        if (currentIndex < 0 && vm.SelectedRow != null)
        {
            currentIndex = vm.FlatRows.IndexOf(vm.SelectedRow);
        }

        int nextIndex = Math.Clamp(currentIndex + delta, 0, count - 1);
        if (nextIndex == currentIndex && PartidaGrid.SelectedItem != null) return;

        var target = vm.FlatRows[nextIndex];
        vm.SelectedRow = target;
        PartidaGrid.SelectedIndex = nextIndex;
        PartidaGrid.ScrollIntoView(target);
    }

    private void MoveToEdge(bool toTop)
    {
        int count = vm.FlatRows.Count;
        if (count == 0) return;

        int nextIndex = toTop ? 0 : count - 1;
        var target = vm.FlatRows[nextIndex];
        vm.SelectedRow = target;
        PartidaGrid.SelectedIndex = nextIndex;
        PartidaGrid.ScrollIntoView(target);
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

        // Ctrl++ : Agregar partida
        if (Keyboard.Modifiers == ModifierKeys.Control && (key == Key.OemPlus || key == Key.Add))
        {
            if (vm.AddPartidaCommand.CanExecute(null))
            {
                vm.AddPartidaCommand.Execute(null);
                FocusAndScrollToSelected();
                FocusQuickEdit();
            }
            e.Handled = true;
            return;
        }

        // Ctrl+- : Eliminar partida
        if (Keyboard.Modifiers == ModifierKeys.Control && (key == Key.OemMinus || key == Key.Subtract))
        {
            if (vm.DeletePartidaCommand.CanExecute(null))
            {
                vm.DeletePartidaCommand.Execute(null);
                FocusAndScrollToSelected();
            }
            e.Handled = true;
            return;
        }

        // F2 : Enfocar edición rápida
        if (key == Key.F2)
        {
            FocusQuickEdit();
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

        if (key == Key.Down && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Alt)
            && Keyboard.FocusedElement is not TextBox)
        {
            MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (key == Key.Up && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Alt)
            && Keyboard.FocusedElement is not TextBox)
        {
            MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (key == Key.PageDown && Keyboard.FocusedElement is not TextBox)
        {
            MoveSelection(10);
            e.Handled = true;
            return;
        }

        if (key == Key.PageUp && Keyboard.FocusedElement is not TextBox)
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

        // Ctrl+Enter en cualquier contexto confirma navegación
        if (key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ConfirmSelection();
            e.Handled = true;
            return;
        }

        // Enter normal: flujo inteligente según control enfocado
        if (key == Key.Enter && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Alt))
        {
            if (Keyboard.FocusedElement is TextBox tb)
            {
                if (tb == EditCodeBox)
                {
                    EditDescBox.Focus();
                    EditDescBox.SelectAll();
                    e.Handled = true;
                    return;
                }
                if (tb == EditDescBox)
                {
                    EditUnidadBox.Focus();
                    EditUnidadBox.SelectAll();
                    e.Handled = true;
                    return;
                }
                if (tb == EditUnidadBox)
                {
                    EditCantBox.Focus();
                    EditCantBox.SelectAll();
                    e.Handled = true;
                    return;
                }
                if (tb == EditCantBox)
                {
                    ConfirmSelection();
                    e.Handled = true;
                    return;
                }
            }

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

    private void OnQuickEditPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            PartidaGrid.Focus();
            Keyboard.Focus(PartidaGrid);
            e.Handled = true;
        }
        else if (key == Key.Down && Keyboard.Modifiers == ModifierKeys.None)
        {
            MoveSelection(1);
            e.Handled = true;
        }
        else if (key == Key.Up && Keyboard.Modifiers == ModifierKeys.None)
        {
            MoveSelection(-1);
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

    private void OnIrAPartidaClick(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void NavigateTo(ItemizadoPartidaVm row)
    {
        Result = row;
        DialogResult = true;
    }
}

