using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class EarnedValueResult
{
    public ScheduleId ScheduleId { get; init; } = null!;
    public DateTime StatusDate { get; init; }
    public string CurrencyCode { get; init; } = "CLP";
    public EarnedValueIndicators Indicators { get; init; } = null!;
    public ForecastResult Forecast { get; init; } = null!;
    public VarianceResult Variances { get; init; } = null!;
    public IReadOnlyList<EvmInterpretation> Interpretations { get; init; } = Array.Empty<EvmInterpretation>();
    public IReadOnlyList<EarnedValueValidationIssue> Issues { get; init; } = Array.Empty<EarnedValueValidationIssue>();
    public bool IsValid { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public DateTime CalculatedUtc { get; init; }
}

public sealed class EarnedValueIndicators
{
    public decimal Pv { get; init; }
    public decimal Ev { get; init; }
    public decimal Ac { get; init; }
    public decimal Bac { get; init; }
    public decimal Eac { get; init; }
    public decimal Etc { get; init; }
    public decimal Vac { get; init; }
    public decimal Sv { get; init; }
    public decimal Cv { get; init; }
    public decimal? Spi { get; init; }
    public decimal? Cpi { get; init; }
    public decimal? Tcpi { get; init; }
    public decimal PercentPlanned { get; init; }
    public decimal PercentEarned { get; init; }
    public decimal PercentActual { get; init; }
    public EacMethod EacMethodUsed { get; init; }
}

public sealed class ForecastResult
{
    public decimal ForecastCost { get; init; }
    public decimal? ForecastFinishVarianceDays { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public DateTime? BaselineFinish { get; init; }
}

public sealed class VarianceResult
{
    public decimal CostVariance { get; init; }
    public decimal ScheduleVariance { get; init; }
    public decimal? ProductivityVariance { get; init; }
    public decimal? PerformanceIndexDelta { get; init; }
}

public sealed class EarnedValueValidationIssue
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsError { get; init; }
}

public sealed class EvmInterpretation
{
    public string Indicator { get; init; } = string.Empty;
    public decimal? Value { get; init; }
    public EvmTrafficLight Light { get; init; }
    public string Message { get; init; } = string.Empty;
}
