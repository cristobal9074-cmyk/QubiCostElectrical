using System;
using System.Linq;

namespace QubiCostElectrical;

/// <summary>
/// Motor EVM determinista (PMI). No recalcula CPM ni muta baseline/forecast/cronograma.
/// </summary>
public static class EarnedValueEngine
{
    public static EarnedValueResult Calculate(EarnedValueInput input, DateTime? calculatedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        var issues = EarnedValueValidation.Validate(input).ToList();
        var hasBlocking = EarnedValueValidation.HasErrors(issues);

        var bac = RoundMoney(input.Bac);
        var pv = RoundMoney(input.Pv);
        var ev = RoundMoney(input.Ev);
        var ac = RoundMoney(input.Ac);

        decimal? spi = null;
        decimal? cpi = null;
        if (pv != 0m)
            spi = RoundIndex(ev / pv);
        else if (ev != 0m)
            issues.Add(new EarnedValueValidationIssue { Code = "SPI_DIV0", Message = "División por cero en SPI evitada.", IsError = false });

        if (ac != 0m)
            cpi = RoundIndex(ev / ac);
        else if (ev != 0m)
            issues.Add(new EarnedValueValidationIssue { Code = "CPI_DIV0", Message = "División por cero en CPI evitada.", IsError = false });

        var sv = RoundMoney(ev - pv);
        var cv = RoundMoney(ev - ac);

        var eac = ComputeEac(ac, bac, ev, cpi, spi, input.EacMethod);
        var etc = RoundMoney(eac - ac);
        var vac = RoundMoney(bac - eac);

        decimal? tcpi = null;
        var remainingBudget = bac - ev;
        var remainingFunds = bac - ac;
        if (remainingFunds != 0m)
            tcpi = RoundIndex(remainingBudget / remainingFunds);
        else if (remainingBudget != 0m)
            issues.Add(new EarnedValueValidationIssue { Code = "TCPI_DIV0", Message = "División por cero en TCPI evitada (BAC − AC = 0).", IsError = false });

        var pctPlanned = ClampPct(input.PercentPlanned > 0m ? input.PercentPlanned : (bac > 0m ? pv / bac * 100m : 0m));
        var pctEarned = ClampPct(input.PercentEarned > 0m ? input.PercentEarned : (bac > 0m ? ev / bac * 100m : 0m));
        var pctActual = ClampPct(input.PercentActual > 0m ? input.PercentActual : (bac > 0m ? ac / bac * 100m : 0m));

        var indicators = new EarnedValueIndicators
        {
            Pv = pv,
            Ev = ev,
            Ac = ac,
            Bac = bac,
            Eac = eac,
            Etc = etc,
            Vac = vac,
            Sv = sv,
            Cv = cv,
            Spi = spi,
            Cpi = cpi,
            Tcpi = tcpi,
            PercentPlanned = pctPlanned,
            PercentEarned = pctEarned,
            PercentActual = pctActual,
            EacMethodUsed = input.EacMethod
        };

        decimal? finishVarDays = null;
        if (input.BaselineFinish.HasValue && input.ForecastFinish.HasValue)
            finishVarDays = (decimal)(input.ForecastFinish.Value.Date - input.BaselineFinish.Value.Date).TotalDays;

        var forecast = new ForecastResult
        {
            ForecastCost = eac,
            ForecastFinishVarianceDays = finishVarDays,
            ForecastFinish = input.ForecastFinish,
            BaselineFinish = input.BaselineFinish
        };

        var variances = new VarianceResult
        {
            CostVariance = cv,
            ScheduleVariance = sv,
            ProductivityVariance = cpi.HasValue && spi.HasValue ? RoundIndex(cpi.Value - spi.Value) : null,
            PerformanceIndexDelta = cpi.HasValue && spi.HasValue ? RoundIndex((cpi.Value + spi.Value) / 2m - 1m) : null
        };

        var interpretations = EarnedValueInterpreter.Interpret(indicators);
        var utc = calculatedUtc ?? DateTime.UtcNow;

        return new EarnedValueResult
        {
            ScheduleId = input.ScheduleId,
            StatusDate = input.StatusDate,
            CurrencyCode = input.CurrencyCode,
            Indicators = indicators,
            Forecast = forecast,
            Variances = variances,
            Interpretations = interpretations,
            Issues = issues,
            IsValid = !hasBlocking,
            Explanation = BuildExplanation(indicators, interpretations),
            CalculatedUtc = utc
        };
    }

    public static decimal ComputeActivityEv(decimal budget, decimal percentComplete, EarnedValueMethod method, decimal physicalProgress = 0m)
    {
        if (budget < 0m) return 0m;
        return method switch
        {
            EarnedValueMethod.PhysicalPercentComplete => RoundMoney(budget * ClampPct(physicalProgress) / 100m),
            EarnedValueMethod.FixedFormula0_100 => percentComplete >= 100m ? RoundMoney(budget) : 0m,
            EarnedValueMethod.FixedFormula50_50 => percentComplete <= 0m ? 0m : percentComplete >= 100m ? RoundMoney(budget) : RoundMoney(budget * 0.5m),
            EarnedValueMethod.UnitsComplete => RoundMoney(budget * ClampPct(percentComplete) / 100m),
            EarnedValueMethod.MilestoneWeighted => RoundMoney(budget * ClampPct(percentComplete) / 100m),
            _ => RoundMoney(budget * ClampPct(percentComplete) / 100m)
        };
    }

    public static decimal ComputePvAtStatus(
        decimal budget,
        DateTime? plannedStart,
        DateTime? plannedFinish,
        DateTime statusDate)
    {
        if (budget <= 0m) return 0m;
        if (!plannedStart.HasValue || !plannedFinish.HasValue) return 0m;
        if (statusDate.Date < plannedStart.Value.Date) return 0m;
        if (statusDate.Date >= plannedFinish.Value.Date) return RoundMoney(budget);
        var span = (plannedFinish.Value.Date - plannedStart.Value.Date).TotalDays;
        if (span <= 0) return RoundMoney(budget);
        var elapsed = (statusDate.Date - plannedStart.Value.Date).TotalDays;
        return RoundMoney(budget * (decimal)(elapsed / span));
    }

    private static decimal ComputeEac(decimal ac, decimal bac, decimal ev, decimal? cpi, decimal? spi, EacMethod method)
    {
        return method switch
        {
            EacMethod.AcPlusRemainingBudget => RoundMoney(ac + (bac - ev)),
            EacMethod.AcPlusRemainingOverCpiSpi when cpi is > 0m && spi is > 0m =>
                RoundMoney(ac + (bac - ev) / (cpi.Value * spi.Value)),
            EacMethod.BacOverCpi when cpi is > 0m => RoundMoney(bac / cpi.Value),
            EacMethod.BacOverCpi => RoundMoney(ac + (bac - ev)),
            _ => RoundMoney(ac + (bac - ev))
        };
    }

    private static string BuildExplanation(EarnedValueIndicators ind, System.Collections.Generic.IReadOnlyList<EvmInterpretation> interp)
    {
        var spiMsg = interp.FirstOrDefault(i => i.Indicator == "SPI")?.Message ?? "";
        var cpiMsg = interp.FirstOrDefault(i => i.Indicator == "CPI")?.Message ?? "";
        return $"PV={ind.Pv:N0}; EV={ind.Ev:N0}; AC={ind.Ac:N0}; BAC={ind.Bac:N0}; SPI={ind.Spi}; CPI={ind.Cpi}. {spiMsg} {cpiMsg}".Trim();
    }

    private static decimal RoundMoney(decimal v) => decimal.Round(v, 2, MidpointRounding.ToEven);
    private static decimal RoundIndex(decimal v) => decimal.Round(v, 4, MidpointRounding.ToEven);
    private static decimal ClampPct(decimal v)
    {
        if (v < 0m) return 0m;
        if (v > 100m) return 100m;
        return decimal.Round(v, 4, MidpointRounding.ToEven);
    }
}
