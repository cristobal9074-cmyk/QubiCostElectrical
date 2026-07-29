namespace QubiCostElectrical;

/// <summary>Políticas explícitas de seguimiento (no silenciosas).</summary>
public sealed record ProgressPolicy
{
    public bool AllowOverProgress { get; init; }
    public bool AllowFutureActualFinishVsStatusDate { get; init; }
    public bool AllowPositiveProgressWithoutActualStart { get; init; }
    public bool AllowPercentDecreaseWithoutComment { get; init; }
    public bool AutoCorrectOutOfSequence { get; init; }
    public GlobalProgressWeighting GlobalWeighting { get; init; } = GlobalProgressWeighting.ByDuration;
    public string CurrencyCode { get; init; } = "CLP";

    public static ProgressPolicy Default { get; } = new();
}
