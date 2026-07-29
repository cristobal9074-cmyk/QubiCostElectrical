using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ChangeRequestStatusTransitions
{
    private static readonly Dictionary<ChangeRequestStatus, HashSet<ChangeRequestStatus>> ValidTransitions = new()
    {
        [ChangeRequestStatus.Draft] = new() { ChangeRequestStatus.Submitted, ChangeRequestStatus.Cancelled },
        [ChangeRequestStatus.Submitted] = new() { ChangeRequestStatus.UnderAnalysis, ChangeRequestStatus.Cancelled },
        [ChangeRequestStatus.UnderAnalysis] = new() { ChangeRequestStatus.QuotationRequired, ChangeRequestStatus.Negotiation, ChangeRequestStatus.Approved, ChangeRequestStatus.Rejected, ChangeRequestStatus.Cancelled },
        [ChangeRequestStatus.QuotationRequired] = new() { ChangeRequestStatus.Negotiation, ChangeRequestStatus.Approved, ChangeRequestStatus.Rejected, ChangeRequestStatus.Cancelled },
        [ChangeRequestStatus.Negotiation] = new() { ChangeRequestStatus.Approved, ChangeRequestStatus.Rejected, ChangeRequestStatus.Cancelled },
        [ChangeRequestStatus.Approved] = new() { ChangeRequestStatus.ConvertedToChangeOrder, ChangeRequestStatus.Closed },
        [ChangeRequestStatus.Rejected] = new() { ChangeRequestStatus.Closed, ChangeRequestStatus.Cancelled },
        [ChangeRequestStatus.ConvertedToChangeOrder] = new() { ChangeRequestStatus.Closed },
        [ChangeRequestStatus.Cancelled] = new() { ChangeRequestStatus.Closed },
        [ChangeRequestStatus.Closed] = new()
    };

    public static bool CanTransition(ChangeRequestStatus from, ChangeRequestStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(ChangeRequestStatus from, ChangeRequestStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<ChangeRequestStatus> GetAllowedNext(ChangeRequestStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<ChangeRequestStatus>();
}
