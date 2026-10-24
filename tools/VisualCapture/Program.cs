using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Infrastructure;
using QubiCostElectrical.Wpf.Navigation;
using QubiCostElectrical.Wpf.Shell;

namespace VisualCapture;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var outputDir = Path.Combine(baseDir, "docs", "manual", "images");
        Directory.CreateDirectory(outputDir);

        var projectPath = Path.Combine(outputDir, "Edificio_Vista_Cordillera.qce");

        try
        {
            var host = WpfAppHost.Initialize();
            CreateSampleProject(projectPath);

            var app = new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/QubiCostElectrical;component/DesignSystem/Theme.xaml", UriKind.Absolute)
            });

            app.Startup += async (_, _) =>
            {
                try
                {
                    var window = new ShellWindow(host)
                    {
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = 50,
                        Top = 50,
                        Width = 1440,
                        Height = 900
                    };

                    window.Show();
                    await Task.Delay(1200);

                    // 00 - Pantalla de inicio
                    Capture(window, Path.Combine(outputDir, "00_inicio.png"));

                    var vm = (ShellViewModel)window.DataContext;
                    var openMethod = typeof(ShellViewModel).GetMethod("OpenProjectFromPathAsync", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (openMethod == null)
                        throw new InvalidOperationException("No se encontró OpenProjectFromPathAsync");

                    await (Task)openMethod.Invoke(vm, new object[] { projectPath })!;
                    await Task.Delay(1500);

                    var targets = new (ShellRoute Route, string FileName)[]
                    {
                        (ShellRoute.Dashboard, "01_dashboard.png"),
                        (ShellRoute.Project, "02_datos_proyecto.png"),
                        (ShellRoute.Itemized, "03_itemizado.png"),
                        (ShellRoute.Takeoff, "04_cubicacion.png"),
                        (ShellRoute.CubicacionSc, "05_cubicacion_sc.png"),
                        (ShellRoute.Apu, "06_apu.png"),
                        (ShellRoute.Prices, "07_banco_precios.png"),
                        (ShellRoute.Review, "08_revision_final.png"),
                        (ShellRoute.OfferDocuments, "09_oferta_exportacion.png")
                    };

                    foreach (var (route, fileName) in targets)
                    {
                        vm.RequestNavigate(route, force: true);
                        await Task.Delay(1000);
                        Capture(window, Path.Combine(outputDir, fileName));
                    }

                    Application.Current.Shutdown(0);
                }
                catch (Exception ex)
                {
                    var errPath = Path.Combine(outputDir, "error.txt");
                    File.WriteAllText(errPath, ex.ToString());
                    Application.Current.Shutdown(1);
                }
            };

            return app.Run();
        }
        catch (Exception ex)
        {
            var errPath = Path.Combine(outputDir, "error.txt");
            File.WriteAllText(errPath, ex.ToString());
            return 1;
        }
    }

    private static void CreateSampleProject(string projectPath)
    {
        if (File.Exists(projectPath))
            File.Delete(projectPath);

        var services = AppServices.CreateDefault();
        var session = new BudgetProjectSession();
        var workspace = new ProjectWorkspaceService(session, services.ProjectRepository, services.Validation);

        session.NewProject("manual-demo", "Cristóbal (Estimador Senior)");
        session.ProjectInfo.Nombre = "Edificio Vista Cordillera";
        session.ProjectInfo.Cliente = "Inmobiliaria y Constructora Alto Sur SpA";
        session.ProjectInfo.Ubicacion = "Av. Las Condes 10240, Las Condes";
        session.ProjectInfo.EstadoPresupuesto = "En progreso";
        session.ProjectInfo.MetrosCuadrados = 4500m;
        session.ProjectInfo.ValorUfClp = 38500m;
        session.ProjectInfo.MonedaPresupuesto = "UF";
        session.ProjectInfo.GastosGeneralesPct = 12m;
        session.ProjectInfo.UtilidadPct = 15m;
        session.ProjectInfo.UsuarioNombre = "Cristóbal (Estimador Senior)";
        session.ProjectInfo.UsuarioRol = "Estimador";

        // Itemizado
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "CAP1",
            Item = "1",
            Descripcion = "OBRAS PRELIMINARES Y FAENAS",
            EsTitulo = true
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P101",
            Item = "1.1",
            Descripcion = "Empalme provisorio y tablero de faenas",
            Unidad = "GL",
            CantidadOriginal = 1m,
            Cantidad = 1m
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P102",
            Item = "1.2",
            Descripcion = "Instalación de faena y red provisoria",
            Unidad = "GL",
            CantidadOriginal = 1m,
            Cantidad = 1m
        });

        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "CAP2",
            Item = "2",
            Descripcion = "ALIMENTADORES Y FUERZA",
            EsTitulo = true
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P201",
            Item = "2.1",
            Descripcion = "Alimentador principal T-Gral a T-Fuerza (EVA 3x50mm2 + T)",
            Unidad = "ML",
            CantidadOriginal = 160m,
            Cantidad = 160m
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P202",
            Item = "2.2",
            Descripcion = "Subalimentador Sala de Bombas e Hidroneumático",
            Unidad = "ML",
            CantidadOriginal = 85m,
            Cantidad = 85m
        });

        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "CAP3",
            Item = "3",
            Descripcion = "TABLEROS ELÉCTRICOS",
            EsTitulo = true
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P301",
            Item = "3.1",
            Descripcion = "Tablero General de Fuerza y Control TDF-01",
            Unidad = "UN",
            CantidadOriginal = 1m,
            Cantidad = 1m
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P302",
            Item = "3.2",
            Descripcion = "Tablero de Alumbrado y Servicios Comunes TDA-SC",
            Unidad = "UN",
            CantidadOriginal = 1m,
            Cantidad = 1m
        });

        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "CAP4",
            Item = "4",
            Descripcion = "ALUMBRADO Y ENCHUFES",
            EsTitulo = true
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P401",
            Item = "4.1",
            Descripcion = "Centro de alumbrado LED embutido 40W c/interruptor 9/12",
            Unidad = "UN",
            CantidadOriginal = 64m,
            Cantidad = 64m
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P402",
            Item = "4.2",
            Descripcion = "Enchufe doble 10/16A con tierra de protección",
            Unidad = "UN",
            CantidadOriginal = 92m,
            Cantidad = 92m
        });

        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "CAP5",
            Item = "5",
            Descripcion = "CORRIENTES DÉBILES Y COMUNICACIONES",
            EsTitulo = true
        });
        session.Itemizado.Add(new ItemizadoRow
        {
            Id = "P501",
            Item = "5.1",
            Descripcion = "Punto de datos red UTP Cat6 y canalización CCTV",
            Unidad = "PTO",
            CantidadOriginal = 36m,
            Cantidad = 36m
        });

        // Banco de materiales
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-CAB-01", Descripcion = "Cable multiconductor EVA 3x50 mm2 + T", Unidad = "ML", MatClp = 43120m, MatUf = 1.12m, Categoria = "Conductores" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-CAB-02", Descripcion = "Conductor Cu H07Z1-K 2.5 mm2 Verde", Unidad = "ML", MatClp = 577m, MatUf = 0.015m, Categoria = "Conductores" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-CAB-03", Descripcion = "Conductor Cu H07Z1-K 2.5 mm2 Rojo", Unidad = "ML", MatClp = 577m, MatUf = 0.015m, Categoria = "Conductores" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-CAB-04", Descripcion = "Conductor Cu H07Z1-K 2.5 mm2 Blanco", Unidad = "ML", MatClp = 577m, MatUf = 0.015m, Categoria = "Conductores" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-TUB-01", Descripcion = "Tubería Conduit Galvanizada C-40 2\"", Unidad = "ML", MatClp = 16170m, MatUf = 0.42m, Categoria = "Canalizaciones" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-TUB-02", Descripcion = "Tubería PVC Conduit rígido 20mm", Unidad = "ML", MatClp = 1732m, MatUf = 0.045m, Categoria = "Canalizaciones" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-CAJ-01", Descripcion = "Caja Chuqui galvanizada 5/8", Unidad = "UN", MatClp = 2502m, MatUf = 0.065m, Categoria = "Accesorios" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-TAB-01", Descripcion = "Gabinete metálico sobrepuesto IP65 800x600", Unidad = "UN", MatClp = 184800m, MatUf = 4.80m, Categoria = "Tableros" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-DIS-01", Descripcion = "Interruptor Automático 3x63A 10kA Legrand", Unidad = "UN", MatClp = 55825m, MatUf = 1.45m, Categoria = "Protecciones" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-DIS-02", Descripcion = "Interruptor Automático 1x16A 6kA Schneider", Unidad = "UN", MatClp = 6930m, MatUf = 0.18m, Categoria = "Protecciones" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-DIS-03", Descripcion = "Interruptor Diferencial 2x25A 30mA Legrand", Unidad = "UN", MatClp = 18480m, MatUf = 0.48m, Categoria = "Protecciones" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-APA-01", Descripcion = "Placa Armada 1 Interruptor 9/12 Bticino", Unidad = "UN", MatClp = 4620m, MatUf = 0.12m, Categoria = "Artefactos" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-APA-02", Descripcion = "Placa Armada Doble Enchufe 10/16A Bticino", Unidad = "UN", MatClp = 5390m, MatUf = 0.14m, Categoria = "Artefactos" });
        session.Materiales.Add(new MaterialRow { Codigo = "MAT-LUM-01", Descripcion = "Luminaria LED Panel 60x60 40W 4000K", Unidad = "UN", MatClp = 32725m, MatUf = 0.85m, Categoria = "Iluminación" });

        // Cubicación
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C201_1",
            ItemId = "P201",
            Tipo = "MAT",
            Codigo = "MAT-CAB-01",
            Descripcion = "Cable multiconductor EVA 3x50 mm2 + T",
            Unidad = "ML",
            Cantidad = 160m,
            QtyExpressions = new[] { "120+40" },
            MatClp = 43120m,
            MatUf = 1.12m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C201_2",
            ItemId = "P201",
            Tipo = "MAT",
            Codigo = "MAT-TUB-01",
            Descripcion = "Tubería Conduit Galvanizada C-40 2\"",
            Unidad = "ML",
            Cantidad = 160m,
            MatClp = 16170m,
            MatUf = 0.42m
        });

        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C401_1",
            ItemId = "P401",
            Tipo = "MAT",
            Codigo = "MAT-LUM-01",
            Descripcion = "Luminaria LED Panel 60x60 40W 4000K",
            Unidad = "UN",
            Cantidad = 64m,
            QtyExpressions = new[] { "32*2" },
            MatClp = 32725m,
            MatUf = 0.85m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C401_2",
            ItemId = "P401",
            Tipo = "MAT",
            Codigo = "MAT-APA-01",
            Descripcion = "Placa Armada 1 Interruptor 9/12 Bticino",
            Unidad = "UN",
            Cantidad = 28m,
            MatClp = 4620m,
            MatUf = 0.12m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C401_3",
            ItemId = "P401",
            Tipo = "MAT",
            Codigo = "MAT-TUB-02",
            Descripcion = "Tubería PVC Conduit rígido 20mm",
            Unidad = "ML",
            Cantidad = 180m,
            MatClp = 1732m,
            MatUf = 0.045m
        });
        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C401_4",
            ItemId = "P401",
            Tipo = "MAT",
            Codigo = "MAT-CAB-02",
            Descripcion = "Conductor Cu H07Z1-K 2.5 mm2 Verde",
            Unidad = "ML",
            Cantidad = 220m,
            MatClp = 577m,
            MatUf = 0.015m
        });

        session.Cubicacion.Add(new CubicacionRow
        {
            Id = "C402_1",
            ItemId = "P402",
            Tipo = "MAT",
            Codigo = "MAT-APA-02",
            Descripcion = "Placa Armada Doble Enchufe 10/16A Bticino",
            Unidad = "UN",
            Cantidad = 92m,
            QtyExpressions = new[] { "46*2" },
            MatClp = 5390m,
            MatUf = 0.14m
        });

        // APUs
        var apu201 = new ApuRow
        {
            Id = "A201",
            ItemId = "P201",
            Descripcion = "APU Alimentador principal EVA 3x50mm2",
            Unidad = "ML",
            CantidadPartida = 160m,
            HerramientasPct = 5m,
            ImprevistosAlzaPct = 2m,
            CondicionesLaborales = new ApuCondicionesLaboralesRow
            {
                FactorAlturaPct = 10m,
                NivelDificultad = ApuNivelDificultad.Normal,
                Ubicacion = ApuUbicacionLabor.Interior
            }
        };
        apu201.Recursos.Add(new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            CodigoMaterial = "MAT-CAB-01",
            Descripcion = "Cable multiconductor EVA 3x50 mm2 + T",
            Unidad = "ML",
            Rendimiento = 1.05m,
            PrecioUf = 1.12m
        });
        apu201.Recursos.Add(new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MAT,
            CodigoMaterial = "MAT-TUB-01",
            Descripcion = "Tubería Conduit Galvanizada C-40 2\"",
            Unidad = "ML",
            Rendimiento = 1.02m,
            PrecioUf = 0.42m
        });
        apu201.Recursos.Add(new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MO,
            Descripcion = "Maestro Electricista SEC Clase B",
            Unidad = "HH",
            Rendimiento = 0.45m,
            ValorHHUf = 0.42m
        });
        apu201.Recursos.Add(new ApuRecursoRow
        {
            Tipo = ApuTipoRecurso.MO,
            Descripcion = "Ayudante Eléctrico",
            Unidad = "HH",
            Rendimiento = 0.45m,
            ValorHHUf = 0.28m
        });
        session.Apus.Add(apu201);

        // Cubicación SC
        var cuadro = new CuadroCargaSCRow
        {
            Nombre = "TDA-SC",
            Descripcion = "Tablero Alumbrado Servicios Comunes",
            CircuitosProyectados = 8
        };
        cuadro.Circuitos.Add(new CircuitoSCRow
        {
            Numero = 1,
            Tipo = "A",
            Descripcion = "Iluminación Pasillos Pisos 1 al 5",
            MetrosTuberia = 85m,
            ConductorDerivacion = "H07Z1-K 2.5mm2",
            ConductorDistribucion = "H07Z1-K 1.5mm2"
        });
        cuadro.Circuitos.Add(new CircuitoSCRow
        {
            Numero = 2,
            Tipo = "A",
            Descripcion = "Iluminación Escalera Presurizada y Emergencia",
            MetrosTuberia = 120m,
            ConductorDerivacion = "H07Z1-K 2.5mm2",
            ConductorDistribucion = "H07Z1-K 1.5mm2"
        });
        cuadro.Circuitos.Add(new CircuitoSCRow
        {
            Numero = 3,
            Tipo = "E",
            Descripcion = "Enchufes Servicios Comunes Hall y Pasillos",
            MetrosTuberia = 95m,
            ConductorDerivacion = "H07Z1-K 2.5mm2"
        });
        cuadro.Circuitos.Add(new CircuitoSCRow
        {
            Numero = 4,
            Tipo = "F",
            Descripcion = "Fuerza Extractores de Aire Subterráneo",
            MetrosTuberia = 45m,
            ConductorDerivacion = "H07Z1-K 4mm2"
        });
        session.CubicacionSC.Add(cuadro);

        workspace.SaveAsAsync(projectPath).GetAwaiter().GetResult();
    }

    private static void Capture(Window window, string outputPath)
    {
        window.UpdateLayout();

        var width = (int)Math.Ceiling(window.ActualWidth);
        var height = (int)Math.Ceiling(window.ActualHeight);
        if (width <= 0 || height <= 0)
            return;

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
