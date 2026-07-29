using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QubiCostElectrical;

public static class DashboardSnapshotHasher
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Compute(ProjectExecutiveDashboard dashboard, string engineVersions, string sourceHashes)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        var payload = JsonSerializer.Serialize(new
        {
            dashboard.ScheduleId.Value,
            StatusDate = dashboard.StatusDate?.ToString("O", CultureInfo.InvariantCulture),
            dashboard.DataQuality,
            dashboard.CurrencyCode,
            Kpis = dashboard.Kpis,
            Series = dashboard.Series,
            Alerts = dashboard.Alerts,
            Deviations = dashboard.Deviations,
            Wbs = dashboard.WbsNodes,
            engineVersions,
            sourceHashes
        }, Options);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
