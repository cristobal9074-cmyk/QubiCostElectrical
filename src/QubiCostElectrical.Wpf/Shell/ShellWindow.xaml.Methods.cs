using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public partial class ShellWindow : Window
{
    public ShellWindow(WpfAppHost host)
    {
        InitializeComponent();
        SourceInitialized += ShellWindow_SourceInitialized;
        StateChanged += OnShellStateChanged;
        Closing += OnShellClosing;
        _viewModel = new ShellViewModel(host);
        _dropHandler = new ShellFileDropHandler(_viewModel);
        _requestUndoAction = _viewModel.Undo;
        ShellNotificationService.RequestUndo = _requestUndoAction;
        Closed += (_, _) =>
        {
            if (ReferenceEquals(ShellNotificationService.RequestUndo, _requestUndoAction))
                ShellNotificationService.RequestUndo = null;
            _viewModel.Dispose();
        };
        DataContext = _viewModel;
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShellViewModel.CurrentRoute))
                UpdateNavVisuals();
            if (e.PropertyName is nameof(ShellViewModel.IsSearchPaletteOpen))
                QuickSearchPopup.IsOpen = _viewModel.IsSearchPaletteOpen;
            if (e.PropertyName is nameof(ShellViewModel.IsProjectPickerOpen))
                ProjectPickerPopup.IsOpen = _viewModel.IsProjectPickerOpen;
            if (e.PropertyName is nameof(ShellViewModel.IsUserMenuOpen))
                UserMenuPopup.IsOpen = _viewModel.IsUserMenuOpen;
        };
        QuickSearchPopup.IsOpen = _viewModel.IsSearchPaletteOpen;
        ProjectPickerPopup.IsOpen = _viewModel.IsProjectPickerOpen;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ShellNotificationService.RequestUndo = _requestUndoAction;
        NavProjectGroup.Items =
        [
            new NavDropdownItem { Label = "Datos del proyecto", Route = ShellRoute.Project },
            new NavDropdownItem { Label = "Antecedentes", Route = ShellRoute.Documents },
            new NavDropdownItem { Label = "Monedas e indicadores", Route = ShellRoute.Currency }
        ];
        NavCubicacionGroup.Items =
        [
            new NavDropdownItem { Label = "Cubicación", Route = ShellRoute.Takeoff },
            new NavDropdownItem { Label = "Cubicación SC", Route = ShellRoute.CubicacionSc }
        ];
        NavPricesGroup.Items =
        [
            new NavDropdownItem { Label = "Banco de precios", Route = ShellRoute.Prices },
            new NavDropdownItem { Label = "Cotizaciones", Route = ShellRoute.Quotes },
            new NavDropdownItem { Label = "Lista de compra", Route = ShellRoute.Purchases },
            new NavDropdownItem { Label = "Solicitudes", Route = ShellRoute.Solicitudes }
        ];
        NavCostosGroup.Items =
        [
            new NavDropdownItem { Label = "Desglose de costos", Route = ShellRoute.Costo },
            new NavDropdownItem { Label = "Mano de obra", Route = ShellRoute.ManoDeObra },
            new NavDropdownItem { Label = "EPP", Route = ShellRoute.Epp }
        ];
        NavCloseGroup.Items =
        [
            new NavDropdownItem { Label = "Revisión final", Route = ShellRoute.Review },
            new NavDropdownItem { Label = "Versiones", Route = ShellRoute.Versions },
            new NavDropdownItem { Label = "Órdenes de cambio", Route = ShellRoute.ChangeOrders },
            new NavDropdownItem { Label = "Oferta y exportación", Route = ShellRoute.OfferDocuments },
            new NavDropdownItem { Label = "Contratos", Route = ShellRoute.ContractWorkspace }
        ];
        NavMoreGroup.Items =
        [
            // Herramientas complementarias
            new NavDropdownItem { Label = "APU (Análisis unitario)", Route = ShellRoute.Apu },
            new NavDropdownItem { Label = "Plantillas", Route = ShellRoute.Templates },
            new NavDropdownItem { Label = "Preferencias", Route = ShellRoute.Preferences },
            NavDropdownItem.Separator(),
            // Herramientas avanzadas / análisis
            new NavDropdownItem { Label = "Editor económico", Route = ShellRoute.EconomicEditor },
            new NavDropdownItem { Label = "Resumen por piso", Route = ShellRoute.FloorSummary },
            new NavDropdownItem { Label = "APU · Catálogo", Route = ShellRoute.ApuCatalog },
            new NavDropdownItem { Label = "APU · Fusión de versiones", Route = ShellRoute.ApuMerge }
        ];
        _viewModel.Navigate(ShellRoute.Dashboard);
        UpdateNavVisuals();
    }

    private void UpdateNavVisuals()
    {
        var route = _viewModel.CurrentRoute;
        NavigationVisuals.SetIsActive(DashboardButton, route == ShellRoute.Dashboard);
        NavProjectGroup.SetActiveState(ShellRouteNavigation.IsProjectRoute(route));
        NavigationVisuals.SetIsActive(NavItemizadoTab, route == ShellRoute.Itemized);
        NavCubicacionGroup.SetActiveState(ShellRouteNavigation.IsCubicacionRoute(route));
        NavPricesGroup.SetActiveState(ShellRouteNavigation.IsPricesRoute(route));
        NavCostosGroup.SetActiveState(ShellRouteNavigation.IsCostosRoute(route));
        NavCloseGroup.SetActiveState(ShellRouteNavigation.IsCloseRoute(route));
        NavMoreGroup.SetActiveState(ShellRouteNavigation.IsMoreRoute(route));
    }

    private void OnBudgetTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ShellRoute route })
            _viewModel.Navigate(route);
    }

    // ─── Window controls ──────────────────────────────────────────────────

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnMaxRestoreClick(object sender, RoutedEventArgs e) =>
        ToggleMaximizeRestore();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaxRestoreIcons();
    }

    private void OnShellStateChanged(object? sender, EventArgs e) =>
        UpdateMaxRestoreIcons();

    private void UpdateMaxRestoreIcons()
    {
        var icon = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        if (MaxRestoreIcon != null)
            MaxRestoreIcon.Text = icon;
        if (LandingMaxRestoreIcon != null)
            LandingMaxRestoreIcon.Text = icon;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<ButtonBase>(dep) != null ||
                    FindVisualParent<TextBox>(dep) != null ||
                    FindVisualParent<ListBoxItem>(dep) != null)
                {
                    return;
                }
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // DragMove puede fallar si el botón del mouse fue soltado antes de la llamada
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private async void OnShellClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (SuppressShutdownOnClose)
            return;

        if (_exitFlushCompleted)
        {
            IsClosing = true;
            if (Application.Current.Dispatcher is { HasShutdownStarted: false })
                Application.Current.Shutdown();
            return;
        }

        // Cancelar el cierre hasta completar flush; luego reintentar con bandera.
        e.Cancel = true;
        IsClosing = true;
        bool ok;
        try
        {
            ok = await _viewModel.PrepareForAppExitAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Shell", "Falló el flush al cerrar la aplicación", ex);
            ok = false;
            WpfDialogService.Error("Salir", OperationFailureMessages.ToUserMessage(ex));
        }

        if (!ok)
        {
            IsClosing = false;
            return;
        }

        _exitFlushCompleted = true;
        if (Application.Current.Dispatcher is { HasShutdownStarted: false })
            Application.Current.Shutdown();
    }

    // ─── Navigation ───────────────────────────────────────────────────────

    private void OnDashboardClick(object sender, RoutedEventArgs e) => _viewModel.Navigate(ShellRoute.Dashboard);
    private void OnReviewClick(object sender, RoutedEventArgs e) => _viewModel.Navigate(ShellRoute.Review);
    private void OnCurrencyChipClick(object sender, RoutedEventArgs e) => _viewModel.Navigate(ShellRoute.Currency);
    private void OnSaveClick(object sender, RoutedEventArgs e) => _ = _viewModel.SaveProjectAsync();
    private void OnSaveAsClick(object sender, RoutedEventArgs e) => _ = _viewModel.SaveProjectAsAsync();
    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        CancelActiveDataGridEdit();
        _viewModel.Undo();
        RestoreDataGridFocus();
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        CancelActiveDataGridEdit();
        _viewModel.Redo();
        RestoreDataGridFocus();
    }

    // ─── Project Picker ────────────────────────────────────────────────────

    private void OnProjectPickerClick(object sender, RoutedEventArgs e)
    {
        if (_suppressNextProjectPickerToggle)
        {
            _suppressNextProjectPickerToggle = false;
            return;
        }
        _viewModel.IsProjectPickerOpen = !_viewModel.IsProjectPickerOpen;
    }

    private void OnProjectPickerPopupClosed(object? sender, EventArgs e)
    {
        if (_viewModel.IsProjectPickerOpen)
        {
            _viewModel.IsProjectPickerOpen = false;
            // Suppress reopening if the click on the button itself triggered the close
            _suppressNextProjectPickerToggle = true;
            Dispatcher.BeginInvoke(
                () => _suppressNextProjectPickerToggle = false,
                System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void OnProjectPickerPopupOpened(object? sender, EventArgs e)
    {
        OnPopupSlideInOpened(sender, e);
        // Synchronous focus on border prevents Tab from escaping before BeginInvoke fires
        ProjectPickerRoot.Focus();
        Keyboard.Focus(ProjectPickerRoot);
        Dispatcher.BeginInvoke(() =>
        {
            if (NewProjectBtn.IsVisible && NewProjectBtn.IsEnabled)
            {
                NewProjectBtn.Focus();
                Keyboard.Focus(NewProjectBtn);
                return;
            }

            if (OpenProjectBtn.IsVisible && OpenProjectBtn.IsEnabled)
            {
                OpenProjectBtn.Focus();
                Keyboard.Focus(OpenProjectBtn);
                return;
            }
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void OnProjectPickerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.IsProjectPickerOpen = false;
            Dispatcher.BeginInvoke(
                () => ProjectPickerButton.Focus(),
                System.Windows.Threading.DispatcherPriority.Input);
            e.Handled = true;
        }
    }

    private void OnNewProjectClick(object sender, RoutedEventArgs e)
        => _ = _viewModel.NewProjectAsync();

    private void OnOpenProjectFromPickerClick(object sender, RoutedEventArgs e)
        => _ = _viewModel.OpenProjectAsync();

    private void OnCloseProjectFromPickerClick(object sender, RoutedEventArgs e)
        => _ = _viewModel.CloseProjectAsync();

    private void OnOpenRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string path)
            return;
        _ = _viewModel.OpenRecentProjectAsync(path);
    }

    private void OnRemoveRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement el || el.Tag is not string path)
            return;
        _viewModel.RemoveRecentProject(path);
    }

    private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
            if (child is T match)
                return match;
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }

    private void CancelActiveDataGridEdit()
    {
        try
        {
            var grid = FindVisualChild<System.Windows.Controls.DataGrid>(this);
            if (grid != null)
            {
                grid.CancelEdit(System.Windows.Controls.DataGridEditingUnit.Cell);
                grid.CancelEdit(System.Windows.Controls.DataGridEditingUnit.Row);
            }
        }
        catch
        {
            // Evitar que fallos al cancelar edición impidan la ejecución del Undo/Redo
        }
    }

    private void RestoreDataGridFocus()
    {
        try
        {
            var grid = FindVisualChild<System.Windows.Controls.DataGrid>(this);
            if (grid != null && grid.Items.Count > 0)
            {
                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    grid.Focus();
                    if (grid.SelectedItem != null &&
                        grid.ItemContainerGenerator.ContainerFromItem(grid.SelectedItem) is System.Windows.Controls.DataGridRow row)
                    {
                        row.Focus();

                        if (grid.CurrentCell.IsValid)
                        {
                            var cellInfo = grid.CurrentCell;
                            grid.ScrollIntoView(cellInfo.Item, cellInfo.Column);
                            var presenter = FindVisualChild<System.Windows.Controls.Primitives.DataGridCellsPresenter>(row);
                            if (presenter != null)
                            {
                                int colIndex = grid.Columns.IndexOf(cellInfo.Column);
                                if (colIndex >= 0)
                                {
                                    var cell = presenter.ItemContainerGenerator.ContainerFromIndex(colIndex) as System.Windows.Controls.DataGridCell;
                                    cell?.Focus();
                                }
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
        catch
        {
            // Evitar que fallos al enfocar detengan el flujo
        }
    }

    private static bool IsElementInsideDataGrid(DependencyObject element)
    {
        DependencyObject parent = element;
        while (parent != null)
        {
            if (parent is System.Windows.Controls.DataGrid)
                return true;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return false;
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        bool textBoxHasFocus = Keyboard.FocusedElement is System.Windows.Controls.TextBox or System.Windows.Controls.RichTextBox;

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            bool handledLocally = false;
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && !IsElementInsideDataGrid(tb))
            {
                if (tb.CanUndo)
                {
                    tb.Undo();
                    handledLocally = true;
                }
            }
            else if (Keyboard.FocusedElement is System.Windows.Controls.RichTextBox rtb && !IsElementInsideDataGrid(rtb))
            {
                if (rtb.CanUndo)
                {
                    rtb.Undo();
                    handledLocally = true;
                }
            }

            if (!handledLocally)
            {
                CancelActiveDataGridEdit();
                _viewModel.Undo();
                RestoreDataGridFocus();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
        {
            bool handledLocally = false;
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox tb && !IsElementInsideDataGrid(tb))
            {
                if (tb.CanRedo)
                {
                    tb.Redo();
                    handledLocally = true;
                }
            }
            else if (Keyboard.FocusedElement is System.Windows.Controls.RichTextBox rtb && !IsElementInsideDataGrid(rtb))
            {
                if (rtb.CanRedo)
                {
                    rtb.Redo();
                    handledLocally = true;
                }
            }

            if (!handledLocally)
            {
                CancelActiveDataGridEdit();
                _viewModel.Redo();
                RestoreDataGridFocus();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CancelActiveDataGridEdit();
            _ = _viewModel.SaveProjectAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CancelActiveDataGridEdit();
            _ = _viewModel.NewProjectAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CancelActiveDataGridEdit();
            _ = _viewModel.OpenProjectAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CancelActiveDataGridEdit();
            _ = _viewModel.CloseProjectAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _viewModel.OpenQuickSearch();
            TopSearchBox.Focus();
            TopSearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F8 && Keyboard.Modifiers == ModifierKeys.None)
        {
            _viewModel.Navigate(ShellRoute.Review);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F1)
        {
            KeyboardShortcutsDialog.Show(this);
            e.Handled = true;
            return;
        }

        if (!textBoxHasFocus && !_viewModel.IsSearchPaletteOpen
            && _viewModel.CurrentRoute == ShellRoute.Takeoff
            && !(Keyboard.FocusedElement is DependencyObject focusedShortcut && IsElementInsideDataGrid(focusedShortcut))
            && PageContent.Content is QubiCostElectrical.Wpf.Features.Cubicacion.CubicacionView cubicacionShortcutView
            && cubicacionShortcutView.TryHandleConfiguredShortcut(e))
        {
            e.Handled = true;
            return;
        }

        if (!textBoxHasFocus && !_viewModel.IsSearchPaletteOpen && TryNavigateAltShortcut(e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (_viewModel.IsProjectPickerOpen)
            {
                _viewModel.IsProjectPickerOpen = false;
                e.Handled = true;
                return;
            }

            if (_viewModel.IsSearchPaletteOpen)
            {
                _viewModel.CloseQuickSearch();
                e.Handled = true;
                return;
            }

            CloseAllDropdowns();
        }
    }

    private void OnSearchBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => _viewModel.OpenQuickSearch();

    private void OnSearchBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (!_viewModel.IsSearchPaletteOpen)
                _viewModel.OpenQuickSearch();

            _viewModel.SelectQuickSearchIndex(1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            if (!_viewModel.IsSearchPaletteOpen)
                _viewModel.OpenQuickSearch();

            _viewModel.SelectQuickSearchIndex(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            _ = _viewModel.ExecuteQuickSearchAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _viewModel.CloseQuickSearch();
            e.Handled = true;
        }
    }

    private void OnQuickSearchItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not ShellViewModel.QuickSearchItemVm item)
            return;

        _viewModel.SelectedQuickSearchItem = item;
        _ = _viewModel.ExecuteQuickSearchAsync();
    }

    private void OnQuickSearchSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        _viewModel.SelectedQuickSearchItem = listBox.SelectedItem as ShellViewModel.QuickSearchItemVm;
    }

    private void OnQuickSearchPopupClosed(object? sender, EventArgs e)
    {
        if (_viewModel.IsSearchPaletteOpen)
            _viewModel.CloseQuickSearch();
    }

    private void OnUserAvatarClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsUserMenuOpen = !_viewModel.IsUserMenuOpen;
        UserMenuPopup.IsOpen = _viewModel.IsUserMenuOpen;
    }

    private void OnUserMenuPopupClosed(object? sender, EventArgs e)
    {
        if (_viewModel.IsUserMenuOpen)
            _viewModel.IsUserMenuOpen = false;
    }

    private void OnPopupSlideInOpened(object? sender, EventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.Popup { Child: FrameworkElement root })
            return;

        root.Opacity = 0;
        root.Margin = new Thickness(0, -12, 0, 12);
        if (FindResource("Anim.SlideDownIn") is System.Windows.Media.Animation.Storyboard story)
            story.Begin(root);
    }

    private void CloseAllDropdowns()
    {
        NavProjectGroup.ClosePopup();
        NavCubicacionGroup.ClosePopup();
        NavPricesGroup.ClosePopup();
        NavCostosGroup.ClosePopup();
        NavCloseGroup.ClosePopup();
        NavMoreGroup.ClosePopup();
    }

    private bool TryNavigateAltShortcut(KeyEventArgs e)
    {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            return false;

        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        ShellRoute? route = e.Key switch
        {
            Key.D1 or Key.NumPad1 => ShellRoute.Dashboard,
            Key.D2 or Key.NumPad2 => ShellRoute.Itemized,
            Key.D3 or Key.NumPad3 when shift => ShellRoute.CubicacionSc,
            Key.D3 or Key.NumPad3 => ShellRoute.Takeoff,
            Key.D4 or Key.NumPad4 => ShellRoute.Apu,
            Key.D5 or Key.NumPad5 => ShellRoute.Templates,
            Key.D6 or Key.NumPad6 => ShellRoute.Prices,
            Key.D7 or Key.NumPad7 => ShellRoute.Quotes,
            Key.D8 or Key.NumPad8 => ShellRoute.Purchases,
            Key.D9 or Key.NumPad9 => ShellRoute.Review,
            Key.D0 or Key.NumPad0 => ShellRoute.Versions,
            Key.P => ShellRoute.Project,
            Key.O => ShellRoute.OfferDocuments,
            Key.M => ShellRoute.Currency,
            Key.C => ShellRoute.ChangeOrders,
            _ => null
        };

        if (route == null)
            return false;

        _viewModel.Navigate(route.Value);
        return true;
    }
    // ─── Maximizar sin cubrir la barra de tareas ──────────────────────────

    private void ShellWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            var monitor = MonitorFromWindow(hwnd, 0x00000002 /* MONITOR_DEFAULTTONEAREST */);
            if (monitor != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                GetMonitorInfo(monitor, ref mi);

                var workArea = mi.rcWork;
                var monitorArea = mi.rcMonitor;

                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMaxPosition.X = workArea.Left - monitorArea.Left;
                mmi.ptMaxPosition.Y = workArea.Top - monitorArea.Top;
                mmi.ptMaxSize.X = workArea.Right - workArea.Left;
                mmi.ptMaxSize.Y = workArea.Bottom - workArea.Top;

                // Sin esto, Windows no aplica ningún piso al arrastrar el borde: al tomar
                // el mensaje como manejado (handled = true) más abajo, se pisa el
                // ptMinTrackSize que WPF habría fijado a partir de MinWidth/MinHeight,
                // permitiendo reducir la ventana hasta un tamaño donde pantallas como
                // Revisión final (Features/Review/ReviewView.xaml) recortan contenido.
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                mmi.ptMinTrackSize.X = (int)Math.Ceiling(MinWidth * dpi.DpiScaleX);
                mmi.ptMinTrackSize.Y = (int)Math.Ceiling(MinHeight * dpi.DpiScaleY);

                Marshal.StructureToPtr(mmi, lParam, true);
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnWindowDragEnter(object sender, DragEventArgs e)
    {
        if (ShellFileDropHandler.HasValidDroppedFiles(e))
        {
            DragDropOverlay.Visibility = Visibility.Visible;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (ShellFileDropHandler.HasValidDroppedFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        DragDropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        DragDropOverlay.Visibility = Visibility.Collapsed;
        try
        {
            await _dropHandler.HandleDropAsync(e);
        }
        catch (Exception ex)
        {
            AppLog.Error("ShellWindow", "Error al procesar archivo soltado", ex);
        }
    }
}
