using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class EarnedValueSnapshot
{
    public string SnapshotId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public int Version { get; init; }
    public DateTime StatusDate { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string SHA256 { get; init; } = string.Empty;
    public EarnedValueResult Result { get; init; } = null!;
}

public sealed class EarnedValueHistoryEntry
{
    public string EntryId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public DateTime StatusDate { get; init; }
    public DateTime RecordedUtc { get; init; }
    public string RecordedBy { get; init; } = string.Empty;
    public decimal Pv { get; init; }
    public decimal Ev { get; init; }
    public decimal Ac { get; init; }
    public decimal Bac { get; init; }
    public decimal? Spi { get; init; }
    public decimal? Cpi { get; init; }
    public decimal? Tcpi { get; init; }
    public decimal Eac { get; init; }
    public string Source { get; init; } = "Calculate";
}

public sealed class EarnedValueComparison
{
    public string LeftLabel { get; init; } = string.Empty;
    public string RightLabel { get; init; } = string.Empty;
    public DateTime? LeftStatusDate { get; init; }
    public DateTime? RightStatusDate { get; init; }
    public decimal PvDelta { get; init; }
    public decimal EvDelta { get; init; }
    public decimal AcDelta { get; init; }
    public decimal BacDelta { get; init; }
    public decimal? SpiDelta { get; init; }
    public decimal? CpiDelta { get; init; }
    public decimal? TcpiDelta { get; init; }
    public decimal EacDelta { get; init; }
    public decimal CostVarianceDelta { get; init; }
    public decimal ScheduleVarianceDelta { get; init; }
    public IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();
}
