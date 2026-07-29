using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class DashboardTrafficLightThresholds
{
    public decimal SpiGreenMin { get; init; } = 1.00m;
    public decimal SpiAmberMin { get; init; } = 0.90m;
    public decimal CpiGreenMin { get; init; } = 1.00m;
    public decimal CpiAmberMin { get; init; } = 0.90m;
}

public static class DashboardTrafficLightPolicy
{
    public static DashboardKpiStatus EvaluateSpi(decimal? spi, DashboardTrafficLightThresholds? thresholds = null)
    {
        var t = thresholds ?? new DashboardTrafficLightThresholds();
        if (!spi.HasValue) return DashboardKpiStatus.NoData;
        if (spi.Value >= t.SpiGreenMin) return DashboardKpiStatus.Good;
        if (spi.Value >= t.SpiAmberMin) return DashboardKpiStatus.Warning;
        return DashboardKpiStatus.Critical;
    }

    public static DashboardKpiStatus EvaluateCpi(decimal? cpi, DashboardTrafficLightThresholds? thresholds = null)
    {
        var t = thresholds ?? new DashboardTrafficLightThresholds();
        if (!cpi.HasValue) return DashboardKpiStatus.NoData;
        if (cpi.Value >= t.CpiGreenMin) return DashboardKpiStatus.Good;
        if (cpi.Value >= t.CpiAmberMin) return DashboardKpiStatus.Warning;
        return DashboardKpiStatus.Critical;
    }

    public static DashboardKpiStatus EvaluateVac(decimal? vac)
    {
        if (!vac.HasValue) return DashboardKpiStatus.NoData;
        if (vac.Value < 0m) return DashboardKpiStatus.Critical;
        if (vac.Value == 0m) return DashboardKpiStatus.Neutral;
        return DashboardKpiStatus.Good;
    }

    public static DashboardKpiStatus EvaluateFinishVarianceDays(decimal? daysLate)
    {
        if (!daysLate.HasValue) return DashboardKpiStatus.NoData;
        if (daysLate.Value > 0m) return DashboardKpiStatus.Critical;
        if (daysLate.Value < 0m) return DashboardKpiStatus.Good;
        return DashboardKpiStatus.Neutral;
    }
}
