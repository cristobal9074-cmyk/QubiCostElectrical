using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class AdjustmentStatusTransitions
{
    private static readonly Dictionary<AdjustmentStatus, HashSet<AdjustmentStatus>> ValidTransitions = new()
    {
        [AdjustmentStatus.Draft] = new() { AdjustmentStatus.Calculated, AdjustmentStatus.Closed },
        [AdjustmentStatus.Calculated] = new() { AdjustmentStatus.Applied, AdjustmentStatus.Rejected, AdjustmentStatus.Closed },
        [AdjustmentStatus.Applied] = new() { AdjustmentStatus.Closed },
        [AdjustmentStatus.Rejected] = new() { AdjustmentStatus.Closed },
        [AdjustmentStatus.Closed] = new()
    };

    public static bool CanTransition(AdjustmentStatus from, AdjustmentStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(AdjustmentStatus from, AdjustmentStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<AdjustmentStatus> GetAllowedNext(AdjustmentStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<AdjustmentStatus>();
}

public static class GuaranteeStatusTransitions
{
    private static readonly Dictionary<GuaranteeStatus, HashSet<GuaranteeStatus>> ValidTransitions = new()
    {
        [GuaranteeStatus.Draft] = new() { GuaranteeStatus.Active, GuaranteeStatus.Cancelled },
        [GuaranteeStatus.Active] = new() { GuaranteeStatus.Renewed, GuaranteeStatus.Released, GuaranteeStatus.Expired, GuaranteeStatus.Executed, GuaranteeStatus.Cancelled },
        [GuaranteeStatus.Renewed] = new() { GuaranteeStatus.Active, GuaranteeStatus.Released, GuaranteeStatus.Expired, GuaranteeStatus.Executed, GuaranteeStatus.Cancelled },
        [GuaranteeStatus.Released] = new() { GuaranteeStatus.Expired, GuaranteeStatus.Cancelled },
        [GuaranteeStatus.Expired] = new() { GuaranteeStatus.Cancelled },
        [GuaranteeStatus.Executed] = new(),
        [GuaranteeStatus.Cancelled] = new()
    };

    public static bool CanTransition(GuaranteeStatus from, GuaranteeStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(GuaranteeStatus from, GuaranteeStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<GuaranteeStatus> GetAllowedNext(GuaranteeStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<GuaranteeStatus>();
}

public static class PenaltyStatusTransitions
{
    private static readonly Dictionary<PenaltyStatus, HashSet<PenaltyStatus>> ValidTransitions = new()
    {
        [PenaltyStatus.Draft] = new() { PenaltyStatus.Proposed, PenaltyStatus.Closed },
        [PenaltyStatus.Proposed] = new() { PenaltyStatus.UnderReview, PenaltyStatus.Closed },
        [PenaltyStatus.UnderReview] = new() { PenaltyStatus.Approved, PenaltyStatus.Rejected, PenaltyStatus.Closed },
        [PenaltyStatus.Approved] = new() { PenaltyStatus.Applied, PenaltyStatus.Closed },
        [PenaltyStatus.Rejected] = new() { PenaltyStatus.Closed },
        [PenaltyStatus.Applied] = new() { PenaltyStatus.Closed },
        [PenaltyStatus.Closed] = new()
    };

    public static bool CanTransition(PenaltyStatus from, PenaltyStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(PenaltyStatus from, PenaltyStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<PenaltyStatus> GetAllowedNext(PenaltyStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<PenaltyStatus>();
}

public static class InvoiceStatusTransitions
{
    private static readonly Dictionary<InvoiceStatus, HashSet<InvoiceStatus>> ValidTransitions = new()
    {
        [InvoiceStatus.Draft] = new() { InvoiceStatus.Issued, InvoiceStatus.Cancelled },
        [InvoiceStatus.Issued] = new() { InvoiceStatus.Paid, InvoiceStatus.PartiallyPaid, InvoiceStatus.Cancelled },
        [InvoiceStatus.PartiallyPaid] = new() { InvoiceStatus.Paid, InvoiceStatus.Cancelled },
        [InvoiceStatus.Paid] = new() { InvoiceStatus.Cancelled },
        [InvoiceStatus.Cancelled] = new()
    };

    public static bool CanTransition(InvoiceStatus from, InvoiceStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(InvoiceStatus from, InvoiceStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<InvoiceStatus> GetAllowedNext(InvoiceStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<InvoiceStatus>();
}
