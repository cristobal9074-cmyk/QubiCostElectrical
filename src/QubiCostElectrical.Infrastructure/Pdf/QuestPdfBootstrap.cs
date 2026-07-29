using System.Threading;
using QuestPDF.Infrastructure;

namespace QubiCostElectrical;

public static class QuestPdfBootstrap
{
    private static int configured;

    public static void EnsureLicense()
    {
        if (Interlocked.Exchange(ref configured, 1) == 1)
            return;

        var fromEnv = Environment.GetEnvironmentVariable("QUESTPDF_LICENSE")?.Trim();
        QuestPDF.Settings.License = fromEnv?.ToUpperInvariant() switch
        {
            "PROFESSIONAL" => LicenseType.Professional,
            "ENTERPRISE" => LicenseType.Enterprise,
            _ => LicenseType.Community
        };
    }

    public static void EnsureCommunityLicense() => EnsureLicense();
}
