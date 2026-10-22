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
        var solicitudIconPath = Path.Combine(exeDir, "Assets", "QubiCostSolicitud.ico");

        // Extraer iconos a disco si no existen en la carpeta Assets junto al ejecutable
        EnsureIconOnDisk("pack://application:,,,/Assets/QubiCostProject.ico", projectIconPath);
        EnsureIconOnDisk("pack://application:,,,/Assets/QubiCostSolicitud.ico", solicitudIconPath);

        // Fallback al ejecutable si por alguna razón el archivo de icono no estuviese
        var iconValue = File.Exists(projectIconPath)
            ? $"\"{projectIconPath}\",0"
            : $"\"{exePath}\",0";

        var solicitudIconValue = File.Exists(solicitudIconPath)
            ? $"\"{solicitudIconPath}\",0"
            : iconValue;

        using var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.qce");
        extKey.SetValue("", ProgId);

        using var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        progKey.SetValue("", "Proyecto de Qubi Cost Electrical");

        using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon");
        iconKey.SetValue("", iconValue);

        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command");
        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");

        RegisterSolicitudPacket(exePath, solicitudIconValue);
        // Notificar a Windows Shell para refrescar la caché de iconos inmediatamente
        NotifyShellIconChange();
    }

    private static void RegisterSolicitudPacket(string exePath, string iconValue)
    {
        try
        {
            const string reqProgId = "QubiCostElectrical.Solicitud.v1";
            using var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.qcreq");
            extKey.SetValue("", reqProgId);

            using var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{reqProgId}");
            progKey.SetValue("", "Paquete de Solicitud de Materiales Qubi Cost");

            using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{reqProgId}\DefaultIcon");
            iconKey.SetValue("", iconValue);

            using var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{reqProgId}\shell\open\command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Silencioso si no hay permisos de registro en entorno restringido
        }
    }

    private static void EnsureIconOnDisk(string packUri, string destinationPath)
    {
        try
        {
            if (File.Exists(destinationPath)) return;

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var uri = new Uri(packUri, UriKind.Absolute);
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

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private static void NotifyShellIconChange()
    {
        try
        {
            const int SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000;
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Omitir si el entorno no permite invocar Shell32
        }
    }
}
