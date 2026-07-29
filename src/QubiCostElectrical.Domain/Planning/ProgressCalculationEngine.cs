using System;

namespace QubiCostElectrical;

/// <summary>Cálculo de porcentajes y productividad (Domain puro, decimal).</summary>
public static class ProgressCalculationEngine
{
    public static decimal ClampPercent(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 100m) return 100m;
        return decimal.Round(value, 4, MidpointRounding.ToEven);
    }

    public static decimal CalculatePercentByDuration(PlanningDuration? planned, PlanningDuration? remaining)
    {
        if (planned is null || planned.Value <= 0) return 0m;
        var plannedHours = (decimal)planned.ToHours().Value;
        if (plannedHours <= 0m) return 0m;
        var remainingHours = remaining is null ? plannedHours : (decimal)remaining.ToHours().Value;
        if (remainingHours < 0m) remainingHours = 0m;
        if (remainingHours > plannedHours) remainingHours = plannedHours;
        return ClampPercent((1m - (remainingHours / plannedHours)) * 100m);
    }

    public static decimal CalculatePercentByQuantity(decimal? planned, decimal? executed)
    {
        if (!planned.HasValue || planned.Value <= 0m) return 0m;
        if (!executed.HasValue) return 0m;
        return ClampPercent((executed.Value / planned.Value) * 100m);
    }

    public static decimal ResolvePercentComplete(
        ProgressMethod method,
        decimal manualPercent,
        PlanningDuration? plannedDuration,
        PlanningDuration? remainingDuration,
        decimal? plannedQuantity,
        decimal? executedQuantity)
    {
        return method switch
        {
            ProgressMethod.ByDuration => CalculatePercentByDuration(plannedDuration, remainingDuration),
            ProgressMethod.ByQuantity or ProgressMethod.ByPhysicalUnits =>
                CalculatePercentByQuantity(plannedQuantity, executedQuantity),
            ProgressMethod.ByWeightedMilestones => ClampPercent(manualPercent),
            ProgressMethod.Other => ClampPercent(manualPercent),
            _ => ClampPercent(manualPercent)
        };
    }

    public static decimal CalculatePhysicalByQuantity(decimal? planned, decimal? executed, bool allowOver)
    {
        if (!planned.HasValue || planned.Value <= 0m) return 0m;
        if (!executed.HasValue) return 0m;
        var pct = (executed.Value / planned.Value) * 100m;
        if (!allowOver && pct > 100m) pct = 100m;
        return ClampPercent(pct);
    }

    public static decimal? CalculateProductivity(decimal? executedQuantity, decimal? actualHours)
    {
        if (!executedQuantity.HasValue || !actualHours.HasValue) return null;
        if (actualHours.Value <= 0m) return null;
        return decimal.Round(executedQuantity.Value / actualHours.Value, 6, MidpointRounding.ToEven);
    }

    public static decimal? ProductivityVariance(decimal? actual, decimal? planned)
    {
        if (!actual.HasValue || !planned.HasValue) return null;
        return decimal.Round(actual.Value - planned.Value, 6, MidpointRounding.ToEven);
    }

    public static decimal CalculateFinancialFromCosts(decimal? actualCost, decimal? committedCost, decimal? plannedCost, bool preferCommitted)
    {
        if (!plannedCost.HasValue || plannedCost.Value <= 0m) return 0m;
        var used = preferCommitted
            ? (committedCost ?? actualCost ?? 0m)
            : (actualCost ?? committedCost ?? 0m);
        return ClampPercent((used / plannedCost.Value) * 100m);
    }

    public static decimal? RemainingQuantity(decimal? planned, decimal? executed)
    {
        if (!planned.HasValue) return null;
        var rem = planned.Value - (executed ?? 0m);
        return rem < 0m ? 0m : rem;
    }
}
