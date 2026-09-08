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

        // Registrar asociación .qce en instalación Velopack o publish portable
        // (publish\win-x64), para que doble clic y acceso directo usen el mismo exe.
        if (IsVelopackInstall() || IsPublishLayout())
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

    private static bool IsPublishLayout()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath);
        if (dir is null) return false;
        // ...\publish\win-x64  o  ...\publish\win-x86
        var parent = Path.GetFileName(dir);
        var grand = Path.GetFileName(Path.GetDirectoryName(dir));
        return string.Equals(grand, "publish", StringComparison.OrdinalIgnoreCase)
               && parent.StartsWith("win-", StringComparison.OrdinalIgnoreCase);
    }
}
