using System;
using System.Threading.Tasks;
using System.Windows;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf;

public partial class SplashWindow : Window
{
    private readonly Action _onInitialized;

    public SplashWindow(Action onInitialized)
    {
        InitializeComponent();
        _onInitialized = onInitialized;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await AnimateProgress();
        _onInitialized();
    }

    private async Task AnimateProgress()
    {
        var container = (FrameworkElement)ProgressBar.Parent;
        var targetWidth = container.ActualWidth;
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

        ProgressBar.Width = targetWidth;
        StatusText.Text = "Listo.";
        await Task.Delay(200);
    }
}
