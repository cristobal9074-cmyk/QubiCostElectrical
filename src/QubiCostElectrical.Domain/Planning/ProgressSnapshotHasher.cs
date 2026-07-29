using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace QubiCostElectrical;

public static class ProgressSnapshotHasher
{
    public static string ComputeHash(ProgressSnapshot snapshot, System.Collections.Generic.IReadOnlyList<ProgressSnapshotItem> items)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(items);

        var sb = new StringBuilder();
        sb.Append($"SNAP:{snapshot.ScheduleId.Value}|{snapshot.Version}|{snapshot.StatusDate:yyyyMMddHHmmss}|");
        sb.Append($"{snapshot.ForecastProjectFinish?.ToString("yyyyMMddHHmmss")};");

        if (snapshot.Summary is not null)
        {
            var s = snapshot.Summary;
            sb.Append($"SUM:{s.TotalActivities}|{s.Completed}|{s.InProgress}|{s.Delayed}|");
            sb.Append($"{s.GlobalPhysicalProgress}|{s.GlobalFinancialProgress}|{s.AccumulatedActualCost};");
        }

        foreach (var id in snapshot.CriticalPathActivityIds.OrderBy(x => x))
            sb.Append($"CP:{id};");

        foreach (var item in items.OrderBy(i => i.ActivityId.Value))
        {
            var p = item.Progress;
            sb.Append($"IT:{item.ActivityId.Value}|{p.PercentComplete}|{p.PhysicalProgress}|{p.FinancialProgress}|");
            sb.Append($"{p.ActualStart?.ToString("yyyyMMddHHmmss")}|{p.ActualFinish?.ToString("yyyyMMddHHmmss")}|");
            sb.Append($"{p.ExecutedQuantity}|{p.ActualCost}|{p.CommittedCost}|{p.ProgressStatus}|");
            sb.Append($"{item.ForecastStart?.ToString("yyyyMMddHHmmss")}|{item.ForecastFinish?.ToString("yyyyMMddHHmmss")}|{item.IsCritical};");
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) hex.Append(b.ToString("x2"));
        return hex.ToString();
    }

    public static bool Verify(ProgressSnapshot snapshot, System.Collections.Generic.IReadOnlyList<ProgressSnapshotItem> items) =>
        string.Equals(snapshot.SHA256, ComputeHash(snapshot, items), StringComparison.OrdinalIgnoreCase);
}
