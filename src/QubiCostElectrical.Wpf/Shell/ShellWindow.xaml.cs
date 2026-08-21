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
    private bool _suppressNextProjectPickerToggle;

    // Cerrar sesión (ShellViewModel.Logout) reutiliza Window.Close() sobre ESTA MISMA clase para
    // destruir el ShellWindow viejo antes de mostrar el login — sin esta bandera, ese Close()
    // disparaba Closing y terminaba TODA la aplicación (ShutdownMode="OnExplicitShutdown" en
    // App.xaml significa que Shutdown() es la única forma de salir, y Logout la llamaba sin
    // querer). Solo Logout() debe poder pedir "ciérrate sin apagar la app".
    public bool SuppressShutdownOnClose { get; set; }
}
