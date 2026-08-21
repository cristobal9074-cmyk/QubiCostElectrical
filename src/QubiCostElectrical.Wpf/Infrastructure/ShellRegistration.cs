using Microsoft.Win32;

namespace QubiCostElectrical.Wpf.Infrastructure;

internal static class ShellRegistration
{
    private const string ProgId = "QubiCostElectrical.Project";

    internal static void Register()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return;

        using var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.qce");
        extKey.SetValue("", ProgId);

        using var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        progKey.SetValue("", "Proyecto de Qubi Cost Electrical");

        using var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\DefaultIcon");
        iconKey.SetValue("", $"\"{exePath}\",0");

        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}\shell\open\command");
        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
    }

    internal static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\.qce", throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);
    }
}
