using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;
using QubiCostElectrical.Wpf.Auth;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace QubiCostElectrical.Tools.UiSnapshotter;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.WriteLine("Iniciando UiSnapshotter...");
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

        string outputDir = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : @"C:\Users\cristobal1\.gemini\antigravity\brain\30550f8a-6408-4e33-bb34-fbb562dafeda\screenshots";
        Directory.CreateDirectory(outputDir);

        string originalProject = @"C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL037A2026.qce";
        if (!File.Exists(originalProject))
        {
            originalProject = @"C:\Users\cristobal1\Documents\Presupuestos Electricos\PEL039A2026 ABIERTO.qce";
        }
        string tempProject = Path.Combine(Path.GetTempPath(), $"snapshotter_{Guid.NewGuid():N}.qce");
        File.Copy(originalProject, tempProject, overwrite: true);

        try
        {
            var testLogsFolder = Path.Combine(Path.GetTempPath(), "QubiCostElectrical-Snapshots", "Logs");
            Directory.CreateDirectory(testLogsFolder);
            var testDataFolder = Path.Combine(Path.GetTempPath(), "QubiCostElectrical-Snapshots");
            Directory.CreateDirectory(testDataFolder);
            var testPriceBankPath = Path.Combine(testDataFolder, "banco_precios.db");
            var testAuth = new AuthService();

            var host = WpfAppHost.Initialize(testLogsFolder, testPriceBankPath, testAuth);
            SessionManager.SetSession(new UserSession
            {
                Account = new UserAccount
                {
                    DisplayName = "Cristóbal (Audit UI)",
                    Email = "cristobal@qubicost.local",
                    Role = UserRole.Admin,
                    IsActive = true
                }
            });

            var app = new System.Windows.Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/QubiCostElectrical;component/DesignSystem/Theme.xaml", UriKind.RelativeOrAbsolute)
            });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/QubiCostElectrical;component/DesignSystem/ItemizadoResources.xaml", UriKind.RelativeOrAbsolute)
            });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/QubiCostElectrical;component/DesignSystem/DataGrid.xaml", UriKind.RelativeOrAbsolute)
            });

            Console.WriteLine($"Abriendo proyecto temporal {tempProject}...");
            host.ProjectWorkspace.OpenAsync(tempProject).GetAwaiter().GetResult();
            host.Session.CurrentProjectPath = tempProject;
            host.Session.SetLockReadOnly(false, null);

            var shell = new ShellWindow(host);
            var vm = (ShellViewModel)shell.DataContext;
            shell.Width = 1600;
            shell.Height = 900;
            shell.Show();
            vm.RefreshChrome();
            PumpDispatcher();

            (ShellRoute route, string filename, string label)[] tabs =
            [
                (ShellRoute.Dashboard, "01_Dashboard.png", "Dashboard"),
                (ShellRoute.Costo, "07_Costo.png", "Costo"),
                (ShellRoute.Purchases, "12_ListaCompra.png", "Lista de Compra"),
                (ShellRoute.Itemized, "03_Itemizado.png", "Itemizado"),
                (ShellRoute.Takeoff, "04_Cubicacion.png", "Cubicación"),
                (ShellRoute.CubicacionSc, "05_CubicacionSc.png", "Cubicación SC"),
                (ShellRoute.Quotes, "11_Cotizaciones.png", "Cotizaciones"),
                (ShellRoute.Versions, "18_Versiones.png", "Versiones"),
                (ShellRoute.OfferDocuments, "19_OfertaDocumentos.png", "Oferta y Documentos"),
                (ShellRoute.Review, "17_Revision.png", "Revisión"),
                (ShellRoute.Solicitudes, "13_Solicitudes.png", "Solicitudes"),
                (ShellRoute.MaterialControl, "14_MaterialControl.png", "Control de Materiales"),
                (ShellRoute.EstadosPagos, "15_EstadosPagos.png", "Estados de Pago"),
                (ShellRoute.ChangeOrders, "16_OrdenesCambio.png", "Órdenes de Cambio"),
                (ShellRoute.ManoDeObra, "08_ManoDeObra.png", "Mano de Obra"),
                (ShellRoute.Epp, "09_Epp.png", "EPP"),
                (ShellRoute.Prices, "10_BancoPrecios.png", "Banco de Precios"),
                (ShellRoute.FloorSummary, "20_FloorSummary.png", "Resumen por Piso"),
                (ShellRoute.Templates, "21_Plantillas.png", "Plantillas"),
                (ShellRoute.Preferences, "22_Preferencias.png", "Preferencias")
            ];

            foreach (var tab in tabs)
            {
                Console.WriteLine($"Capturando [{tab.label}] -> {tab.filename}...");
                vm.RequestNavigate(tab.route, null, force: true);

                for (int i = 0; i < 10; i++)
                {
                    PumpDispatcher();
                    Thread.Sleep(40);
                }

                if (tab.route == ShellRoute.Purchases)
                {
                    var dataGrids = FindVisualChildren<DataGrid>(shell);
                    foreach (var dg in dataGrids)
                    {
                        Console.WriteLine($"DataGrid: ActualWidth={dg.ActualWidth}, Columns={dg.Columns.Count}");
                        for (int c = 0; c < dg.Columns.Count; c++)
                        {
                            var col = dg.Columns[c];
                            Console.WriteLine($"  Col[{c}] '{col.Header}': ActualWidth={col.ActualWidth}, Width={col.Width}");
                        }
                    }
                }

                int width = (int)Math.Max(1600, shell.ActualWidth);
                int height = (int)Math.Max(900, shell.ActualHeight);

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                
                // Si la ventana tiene contenido pero el Window renderiza negro, renderizamos el contenido
                if (VisualTreeHelper.GetChildrenCount(shell) > 0)
                {
                    var rootVisual = VisualTreeHelper.GetChild(shell, 0);
                    rtb.Render(rootVisual as Visual ?? shell);
                }
                else
                {
                    rtb.Render(shell);
                }

                string outPath = Path.Combine(outputDir, tab.filename);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.OpenWrite(outPath))
                {
                    encoder.Save(fs);
                }
                Console.WriteLine($"Guardado: {outPath}");
            }

            shell.Close();
            Console.WriteLine("Todas las capturas se generaron con éxito.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error fatal en UiSnapshotter: {ex}");
            return 2;
        }
        finally
        {
            try
            {
                if (File.Exists(tempProject))
                    File.Delete(tempProject);
                string lockFile = tempProject + ".lock";
                if (File.Exists(lockFile))
                    File.Delete(lockFile);
            }
            catch { }
        }
    }

    private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t) yield return t;
            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
