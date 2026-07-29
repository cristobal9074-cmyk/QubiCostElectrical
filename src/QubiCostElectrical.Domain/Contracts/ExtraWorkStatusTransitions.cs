using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public static class ExtraWorkStatusTransitions
{
    private static readonly Dictionary<ExtraWorkStatus, HashSet<ExtraWorkStatus>> ValidTransitions = new()
    {
        [ExtraWorkStatus.Draft] = new() { ExtraWorkStatus.Instructed, ExtraWorkStatus.Submitted },
        [ExtraWorkStatus.Instructed] = new() { ExtraWorkStatus.InExecution, ExtraWorkStatus.Submitted, ExtraWorkStatus.Rejected },
        [ExtraWorkStatus.InExecution] = new() { ExtraWorkStatus.Submitted, ExtraWorkStatus.Rejected },
        [ExtraWorkStatus.Submitted] = new() { ExtraWorkStatus.Approved, ExtraWorkStatus.Rejected },
        [ExtraWorkStatus.Approved] = new() { ExtraWorkStatus.Valued, ExtraWorkStatus.Closed },
        [ExtraWorkStatus.Rejected] = new() { ExtraWorkStatus.Closed },
        [ExtraWorkStatus.Valued] = new() { ExtraWorkStatus.Closed },
        [ExtraWorkStatus.Closed] = new()
    };

    public static bool CanTransition(ExtraWorkStatus from, ExtraWorkStatus to)
        => ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    public static void EnsureCanTransition(ExtraWorkStatus from, ExtraWorkStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Transición inválida de {from} a {to}.");
    }

    public static IReadOnlySet<ExtraWorkStatus> GetAllowedNext(ExtraWorkStatus current)
        => ValidTransitions.TryGetValue(current, out var allowed) ? allowed : new HashSet<ExtraWorkStatus>();
}
