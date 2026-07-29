using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ChangeOrderStatusTransitions
{
    private static readonly Dictionary<ChangeOrderStatus, HashSet<ChangeOrderStatus>> ValidTransitions = new()
    {
        [ChangeOrderStatus.Draft] = new() { ChangeOrderStatus.PendingReview, ChangeOrderStatus.Cancelled },
        [ChangeOrderStatus.PendingReview] = new() { ChangeOrderStatus.UnderNegotiation, ChangeOrderStatus.PendingApproval, ChangeOrderStatus.Rejected, ChangeOrderStatus.Cancelled },
        [ChangeOrderStatus.UnderNegotiation] = new() { ChangeOrderStatus.PendingApproval, ChangeOrderStatus.Rejected, ChangeOrderStatus.Cancelled },
        [ChangeOrderStatus.PendingApproval] = new() { ChangeOrderStatus.Approved, ChangeOrderStatus.Rejected, ChangeOrderStatus.Cancelled },
        [ChangeOrderStatus.Approved] = new() { ChangeOrderStatus.Implemented, ChangeOrderStatus.Superseded },
        [ChangeOrderStatus.Rejected] = new() { ChangeOrderStatus.Closed, ChangeOrderStatus.Cancelled },
        [ChangeOrderStatus.Implemented] = new() { ChangeOrderStatus.Closed },
        [ChangeOrderStatus.Closed] = new(),
        [ChangeOrderStatus.Cancelled] = new() { ChangeOrderStatus.Closed },
        [ChangeOrderStatus.Superseded] = new() { ChangeOrderStatus.Closed }
    };

    public static bool CanTransition(ChangeOrderStatus from, ChangeOrderStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(ChangeOrderStatus from, ChangeOrderStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<ChangeOrderStatus> GetAllowedNext(ChangeOrderStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<ChangeOrderStatus>();
}
