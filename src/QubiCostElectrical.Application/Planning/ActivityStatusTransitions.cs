using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ActivityStatusTransitions
{
    private static readonly Dictionary<ActivityStatus, HashSet<ActivityStatus>> ValidTransitions = new()
    {
        [ActivityStatus.NotStarted] = new() { ActivityStatus.Ready, ActivityStatus.Blocked, ActivityStatus.InPlanning, ActivityStatus.Cancelled },
        [ActivityStatus.Ready] = new() { ActivityStatus.Scheduled, ActivityStatus.Blocked, ActivityStatus.NotStarted, ActivityStatus.Cancelled },
        [ActivityStatus.Blocked] = new() { ActivityStatus.NotStarted, ActivityStatus.Ready, ActivityStatus.Suspended, ActivityStatus.Cancelled },
        [ActivityStatus.InPlanning] = new() { ActivityStatus.Scheduled, ActivityStatus.NotStarted, ActivityStatus.Cancelled },
        [ActivityStatus.Scheduled] = new() { ActivityStatus.InReview, ActivityStatus.InPlanning, ActivityStatus.Blocked, ActivityStatus.Suspended, ActivityStatus.Cancelled },
        [ActivityStatus.InReview] = new() { ActivityStatus.Approved, ActivityStatus.Scheduled, ActivityStatus.Cancelled },
        [ActivityStatus.Approved] = new() { ActivityStatus.Suspended, ActivityStatus.Completed, ActivityStatus.InReview },
        [ActivityStatus.Suspended] = new() { ActivityStatus.NotStarted, ActivityStatus.Ready, ActivityStatus.Cancelled },
        [ActivityStatus.Cancelled] = new() { ActivityStatus.NotStarted },
        [ActivityStatus.Completed] = new() { }
    };

    public static bool IsValid(ActivityStatus from, ActivityStatus to)
    {
        if (from == to) return true;
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static IReadOnlySet<ActivityStatus> GetAllowedTargets(ActivityStatus from)
        => ValidTransitions.TryGetValue(from, out var allowed) ? allowed : new HashSet<ActivityStatus>();
}
