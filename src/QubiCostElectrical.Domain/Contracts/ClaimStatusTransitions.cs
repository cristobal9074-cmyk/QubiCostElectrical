using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ClaimStatusTransitions
{
    private static readonly Dictionary<ClaimStatus, HashSet<ClaimStatus>> ValidTransitions = new()
    {
        [ClaimStatus.Draft] = new() { ClaimStatus.Notified, ClaimStatus.Submitted, ClaimStatus.Withdrawn },
        [ClaimStatus.Notified] = new() { ClaimStatus.Submitted, ClaimStatus.Withdrawn },
        [ClaimStatus.Submitted] = new() { ClaimStatus.UnderReview, ClaimStatus.InformationRequested, ClaimStatus.Withdrawn },
        [ClaimStatus.UnderReview] = new() { ClaimStatus.InformationRequested, ClaimStatus.Negotiation, ClaimStatus.Accepted, ClaimStatus.PartiallyAccepted, ClaimStatus.Rejected, ClaimStatus.Withdrawn },
        [ClaimStatus.InformationRequested] = new() { ClaimStatus.UnderReview, ClaimStatus.Negotiation, ClaimStatus.Withdrawn },
        [ClaimStatus.Negotiation] = new() { ClaimStatus.Accepted, ClaimStatus.PartiallyAccepted, ClaimStatus.Rejected, ClaimStatus.Withdrawn },
        [ClaimStatus.Accepted] = new() { ClaimStatus.Closed },
        [ClaimStatus.PartiallyAccepted] = new() { ClaimStatus.Closed },
        [ClaimStatus.Rejected] = new() { ClaimStatus.Closed, ClaimStatus.Withdrawn },
        [ClaimStatus.Withdrawn] = new() { ClaimStatus.Closed },
        [ClaimStatus.Closed] = new()
    };

    public static bool CanTransition(ClaimStatus from, ClaimStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(ClaimStatus from, ClaimStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<ClaimStatus> GetAllowedNext(ClaimStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<ClaimStatus>();
}
