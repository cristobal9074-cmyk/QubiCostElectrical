using System.IO;
using System.Linq;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Shell;

namespace VisualCapture;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var outputPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "qubi-startup.png"));
        var projectPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory, "qubi-sample-project.qce");

        try
        {
            var host = WpfAppHost.Initialize();
            CreateSampleProject(projectPath);

            var app = new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/QubiCostElectrical;component/DesignSystem/Theme.xaml", UriKind.Absolute)
            });
            var captured = false;
            app.Startup += (_, _) =>
            {
                var window = new ShellWindow(host)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = 0,
                    Top = 0,
                    Width = 1720,
                    Height = 980
                };

                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(200)
                };

                timer.Tick += (_, _) =>
                {
                    if (captured)
                    {
                        timer.Stop();
                        return;
                    }

                    if (!window.IsLoaded)
                        return;

                    if (window.DataContext is not ShellViewModel vm || !vm.HasOpenProject)
                        return;

                    captured = true;
                    timer.Stop();
                    window.Dispatcher.BeginInvoke(() => Capture(window, outputPath), DispatcherPriority.ApplicationIdle);
                };

                timer.Start();
                window.Show();
            };

            return app.Run();
        }
        catch (Exception ex)
        {
            var errorPath = Path.ChangeExtension(outputPath, ".error.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
            File.WriteAllText(errorPath, ex.ToString());
            return 1;
        }
    }

    private static void CreateSampleProject(string projectPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);

        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);

        session.NewProject("visual-capture", "Inspector");
        session.ProjectInfo.Nombre = "Proyecto de captura";
        session.ProjectInfo.Cliente = "Cliente visual";
        session.ProjectInfo.Ubicacion = "Santiago";
        session.ProjectInfo.EstadoPresupuesto = "En progreso";
        session.ProjectInfo.MetrosCuadrados = 840m;
        session.ProjectInfo.ValorUfClp = 38000m;

        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P1",
            Item = "1.1",
            Descripcion = "Canalización principal",
            Unidad = "ML",
            Cantidad = 120m
        });

        session.Materiales.Add(new MaterialRow
        {
            Codigo = "MAT001",
            Descripcion = "Cable THHN 2,5 mm2",
            Unidad = "ML",
            MatUf = 0.35m
        });

        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C1",
            ItemId = "P1",
            Tipo = "MAT",
            Codigo = "MAT001",
            Descripcion = "Cable THHN 2,5 mm2",
            Unidad = "ML",
            Cantidad = 120m,
            MatUf = 0.35m
        });

        session.Apus.Add(new ApuRow
        {
            Id = "A1",
            ItemId = "P1",
            Descripcion = "APU Canalización principal",
            CondicionesLaborales = new ApuCondicionesLaboralesRow
            {
                FactorAlturaPct = 10m,
                EdificioEnOperacion = false,
                RecargoOperacionPct = 0m,
                NivelDificultad = ApuNivelDificultad.Normal,
                Ubicacion = ApuUbicacionLabor.Interior
            }
        });

        workspace.SaveAsAsync(projectPath).GetAwaiter().GetResult();
    }

    private static void Capture(Window window, string outputPath)
    {
        try
        {
            window.UpdateLayout();

            var width = (int)Math.Ceiling(window.ActualWidth);
            var height = (int)Math.Ceiling(window.ActualHeight);
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"La ventana no tiene tamaño válido: {width}x{height}.");

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(outputPath);
            encoder.Save(stream);
        }
        catch (Exception ex)
        {
            var errorPath = Path.ChangeExtension(outputPath, ".error.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(errorPath)!);
            File.WriteAllText(errorPath, ex.ToString());
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }
}
