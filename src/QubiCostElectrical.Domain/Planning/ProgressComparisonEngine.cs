using System;
using System.Collections.Generic;
using System.Linq;

namespace QubiCostElectrical;

public static class ProgressComparisonEngine
{
    public static ProgressComparisonResult Compare(
        string leftLabel,
        string rightLabel,
        DateTime? leftStatusDate,
        DateTime? rightStatusDate,
        IReadOnlyDictionary<string, ActivityProgressUpdate> left,
        IReadOnlyDictionary<string, ActivityProgressUpdate> right,
        IReadOnlyDictionary<string, Activity>? activities = null,
        IReadOnlySet<string>? leftCritical = null,
        IReadOnlySet<string>? rightCritical = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var ids = new HashSet<string>(left.Keys);
        foreach (var k in right.Keys) ids.Add(k);

        var diffs = new List<ProgressActivityDiff>();
        var started = new List<string>();
        var completed = new List<string>();
        var delayed = new List<string>();
        var ahead = new List<string>();
        var criticalChanged = new List<string>();

        foreach (var id in ids.OrderBy(x => x))
        {
            left.TryGetValue(id, out var l);
            right.TryGetValue(id, out var r);
            Activity? act = null;
            activities?.TryGetValue(id, out act);

            var leftCrit = leftCritical?.Contains(id) == true;
            var rightCrit = rightCritical?.Contains(id) == true;
            var critChanged = leftCrit != rightCrit;
            if (critChanged) criticalChanged.Add(id);

            if (l is null && r is not null && r.ActualStart.HasValue) started.Add(id);
            if (l is not null && !l.ActualStart.HasValue && r is not null && r.ActualStart.HasValue) started.Add(id);

            if ((l is null || l.ProgressStatus != ProgressTrackingStatus.Completed) &&
                r is not null && r.ProgressStatus == ProgressTrackingStatus.Completed)
                completed.Add(id);

            if ((l is null || l.ProgressStatus != ProgressTrackingStatus.Delayed) &&
                r is not null && r.ProgressStatus == ProgressTrackingStatus.Delayed)
                delayed.Add(id);

            if ((l is null || l.ProgressStatus != ProgressTrackingStatus.Ahead) &&
                r is not null && r.ProgressStatus == ProgressTrackingStatus.Ahead)
                ahead.Add(id);

            diffs.Add(new ProgressActivityDiff
            {
                ActivityId = r?.ActivityId ?? l!.ActivityId,
                ActivityCode = act?.Code ?? string.Empty,
                PercentCompleteDelta = (r?.PercentComplete ?? 0m) - (l?.PercentComplete ?? 0m),
                PhysicalProgressDelta = (r?.PhysicalProgress ?? 0m) - (l?.PhysicalProgress ?? 0m),
                FinancialProgressDelta = (r?.FinancialProgress ?? 0m) - (l?.FinancialProgress ?? 0m),
                QuantityDelta = (r?.ExecutedQuantity ?? 0m) - (l?.ExecutedQuantity ?? 0m),
                ActualCostDelta = (r?.ActualCost ?? 0m) - (l?.ActualCost ?? 0m),
                LeftStatus = l?.ProgressStatus,
                RightStatus = r?.ProgressStatus,
                CriticalChanged = critChanged
            });
        }

        return new ProgressComparisonResult
        {
            LeftLabel = leftLabel,
            RightLabel = rightLabel,
            LeftStatusDate = leftStatusDate,
            RightStatusDate = rightStatusDate,
            ActivityDiffs = diffs,
            NewlyStartedActivityIds = started,
            NewlyCompletedActivityIds = completed,
            NewlyDelayedActivityIds = delayed,
            NewlyAheadActivityIds = ahead,
            CriticalPathChangedActivityIds = criticalChanged
        };
    }
}
