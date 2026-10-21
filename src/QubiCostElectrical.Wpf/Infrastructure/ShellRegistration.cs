using System.IO;
using Microsoft.Win32;

namespace QubiCostElectrical.Wpf.Infrastructure;

internal static class ShellRegistration
{
    private const string ProgId = "QubiCostElectrical.Project";

    internal static void Register()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        var exeDir = Path.GetDirectoryName(exePath) ?? string.Empty;
        var projectIconPath = Path.Combine(exeDir, "Assets", "QubiCostProject.ico");

        // Extraer icono a disco si no existe en la carpeta Assets junto al ejecutable
        EnsureProjectIconOnDisk(projectIconPath);

        // Fallback al ejecutable si por alguna razón el archivo de icono no estuviese
        var iconValue = File.Exists(projectIconPath)
            ? $"\"{projectIconPath}\",0"
            : $"\"{exePath}\",0";

        using var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.qce");
        extKey.SetValue("", ProgId);

        using var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        progKey.SetValue("", "Proyecto de Qubi Cost Electrical");

        using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon");
        iconKey.SetValue("", iconValue);

        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command");
        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
    }

    private static void EnsureProjectIconOnDisk(string destinationPath)
    {
        try
        {
            if (File.Exists(destinationPath)) return;

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var uri = new Uri("pack://application:,,,/Assets/QubiCostProject.ico", UriKind.Absolute);
            var streamResourceInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamResourceInfo != null)
            {
                using var input = streamResourceInfo.Stream;
                using var output = File.Create(destinationPath);
                input.CopyTo(output);
            }
        }
        catch
        {
            // Silencioso: si falla la extracción, el fallback usa el ejecutable principal
        }
    }
}
