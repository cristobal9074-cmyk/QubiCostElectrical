using System.IO;
using Velopack;
using QubiCostElectrical.Wpf.Infrastructure;

namespace QubiCostElectrical.Wpf;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // Registrar asociación .qce cuando se ejecuta desde instalación Velopack.
        // Update.exe en el directorio padre indica contexto Velopack.
        if (IsVelopackInstall())
            ShellRegistration.Register();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static bool IsVelopackInstall()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath);
        return dir != null && File.Exists(Path.Combine(dir, "..", "Update.exe"));
    }
}
