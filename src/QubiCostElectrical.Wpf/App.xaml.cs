using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Infrastructure.Diagnostics;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Wpf;

public partial class App : Application
{
    private const string MutexName = "QubiCostElectrical_SingleInstance";
    private Mutex? mutex;
    private IAuthService? _authService;
    private string[] _startupArgs = [];
    private readonly Stopwatch startupStopwatch = Stopwatch.StartNew();

    protected override void OnStartup(StartupEventArgs e)
    {
        _startupArgs = e.Args;

        mutex = new Mutex(initiallyOwned: false, name: MutexName);
        if (!mutex.WaitOne(0))
        {
            if (SingleInstanceHandoffService.TryExtractOpenProjectPath(e.Args, out var projectPath))
                SingleInstanceHandoffService.TrySendOpenProjectRequest(projectPath);
            BringExistingToFront();
            mutex.Dispose();
            mutex = null;
            Shutdown();
            return;
        }

        QuestPdfBootstrap.EnsureCommunityLicense();

        SplashWindow splash = null!;
        var host = Infrastructure.WpfAppHost.Initialize();
        WpfAppHost.Logger?.Info("Inicio", $"Servicios inicializados en {startupStopwatch.ElapsedMilliseconds} ms.");
        QubiCostElectrical.Wpf.DesignSystem.ThemeManager.Initialize();
        splash = new SplashWindow(() =>
        {
            splash.Dispatcher.Invoke(() =>
            {
                splash.Close();
                ShowAuthFlow(host);
            });
        });
        splash.Show();

        base.OnStartup(e);
    }

    // async void es el patrón estándar para un handler de nivel superior que no tiene un
    // caller que espere su finalización (se invoca desde el callback de cierre de SplashWindow).
    private async void ShowAuthFlow(WpfAppHost host)
    {
        GlobalErrorHandler.Initialize(host);
        _authService = host.Auth;

        // Antes bloqueaba el hilo de UI con GetAwaiter().GetResult() durante el I/O de
        // sesión/credenciales al arrancar — la app parecía congelada un instante antes de
        // mostrar login o el shell.
        var session = await _authService.TryRestoreSessionAsync();
        WpfAppHost.Logger?.Info("Inicio", $"Sesión restaurada en {startupStopwatch.ElapsedMilliseconds} ms.");

        if (session != null)
        {
            SessionManager.SetSession(session);
            ShowShellWindow(host);
            return;
        }

        var login = new LoginWindow(_authService);
        login.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        if (login.ShowDialog() == true)
        {
            ShowShellWindow(host);
        }
        else
        {
            Shutdown();
        }
    }

    private void ShowShellWindow(WpfAppHost host)
    {
        var shell = new ShellWindow(host);
        MainWindow = shell;

        SingleInstanceHandoffService.Start(HandleOpenProjectRequestAsync);

        if (SingleInstanceHandoffService.TryExtractOpenProjectPath(_startupArgs, out var path))
            Dispatcher.BeginInvoke(new Action(() => _ = HandleOpenProjectRequestAsync(path)));

        shell.Show();
        WpfAppHost.Logger?.Info("Inicio", $"Shell mostrado en {startupStopwatch.ElapsedMilliseconds} ms.");
        _ = SeedApuTemplatesAsync();
    }

    private static async Task SeedApuTemplatesAsync()
    {
        try
        {
            await Task.Run(() => ApuTemplateSeeder.SeedIfNeeded(new ApuTemplateStore()));
            WpfAppHost.Logger?.Info("Inicio", "Plantillas APU verificadas en segundo plano.");
        }
        catch (Exception ex)
        {
            WpfAppHost.Logger?.Warning("Inicio", "No se pudieron verificar las plantillas APU en segundo plano.", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SingleInstanceHandoffService.Stop();
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        GlobalErrorHandler.Shutdown();
        Infrastructure.WpfAppHost.Logger?.Dispose();
        base.OnExit(e);
    }

    private static void BringExistingToFront()
    {
        var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            if (process.Id == current.Id) continue;
            var hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero) continue;
            if (NativeMethods.IsIconic(hwnd))
                NativeMethods.ShowWindow(hwnd, 9);
            NativeMethods.SetForegroundWindow(hwnd);
            return;
        }
    }

    private async Task HandleOpenProjectRequestAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (Current?.MainWindow?.DataContext is not IShellNavigationHost shell)
            return;

        await shell.OpenRecentProjectAsync(path);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
