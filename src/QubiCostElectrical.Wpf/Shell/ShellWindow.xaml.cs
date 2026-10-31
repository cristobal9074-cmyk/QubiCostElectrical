using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;

namespace QubiCostElectrical.Wpf.Shell;

public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly Action _requestUndoAction;
    private readonly Action _requestRedoAction;
    private readonly ShellFileDropHandler _dropHandler;
    private bool _suppressNextProjectPickerToggle;
    private bool _exitFlushCompleted;

    // Cerrar sesión (ShellViewModel.Logout) reutiliza Window.Close() sobre ESTA MISMA clase para
    // destruir el ShellWindow viejo antes de mostrar el login — sin esta bandera, ese Close()
    // disparaba Closing y terminaba TODA la aplicación (ShutdownMode="OnExplicitShutdown" en
    // App.xaml significa que Shutdown() es la única forma de salir, y Logout la llamaba sin
    // querer). Solo Logout() debe poder pedir "ciérrate sin apagar la app".
    public bool SuppressShutdownOnClose { get; set; }

    /// <summary>Verdadero mientras la ventana principal se cierra por salida de la aplicación.</summary>
    public bool IsClosing { get; private set; }

    public static bool CanShowModalDialog
    {
        get
        {
            var app = Application.Current;
            if (app?.Dispatcher is not { HasShutdownStarted: false })
                return false;
            if (app.MainWindow is ShellWindow shell && shell.IsClosing)
                return false;
            return true;
        }
    }

    public static void RestoreWorkspaceGridFocus()
    {
        if (Application.Current?.MainWindow is ShellWindow shell)
            shell.RestoreDataGridFocus();
    }
}
