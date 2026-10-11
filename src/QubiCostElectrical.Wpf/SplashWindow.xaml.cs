using System;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf;

public partial class SplashWindow : Window
{
    private readonly Action _onInitialized;
    private bool _started;

    public SplashWindow(Action onInitialized)
    {
        InitializeComponent();
        _onInitialized = onInitialized;
        Loaded += (_, _) => Start();
        ContentRendered += (_, _) => Start();
    }

    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            await AnimateProgress();
        }
        catch (Exception ex)
        {
            WpfAppHost.Logger?.Warning("Splash", "Error en animación de splash.", ex);
        }
        finally
        {
            _onInitialized();
        }
    }

    private async Task AnimateProgress()
    {
        var container = (FrameworkElement)ProgressBar.Parent;
        var targetWidth = container.ActualWidth > 0 ? container.ActualWidth : 440.0;
        var statusMessages = new[]
        {
            (0.0, "Inicializando aplicación..."),
            (0.3, "Cargando servicios..."),
            (0.6, "Verificando sesión..."),
            (0.9, "Listo.")
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        const double totalDuration = 1800.0;

        while (sw.ElapsedMilliseconds < totalDuration)
        {
            var t = sw.ElapsedMilliseconds / totalDuration;
            if (targetWidth <= 0 && container.ActualWidth > 0)
                targetWidth = container.ActualWidth;

            ProgressBar.Width = targetWidth * t;

            for (int i = statusMessages.Length - 1; i >= 0; i--)
            {
                if (t >= statusMessages[i].Item1)
                {
                    StatusText.Text = statusMessages[i].Item2;
                    break;
                }
            }

            await Task.Delay(16);
        }

        ProgressBar.Width = targetWidth > 0 ? targetWidth : 440;
        StatusText.Text = "Listo.";
        await Task.Delay(150);
    }
}
