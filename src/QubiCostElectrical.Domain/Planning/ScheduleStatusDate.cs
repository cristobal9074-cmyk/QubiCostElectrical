using System;

namespace QubiCostElectrical;

/// <summary>
/// Fecha de estado explícita por cronograma. No usa DateTime.Now como fuente de verdad.
/// </summary>
public sealed class ScheduleStatusDate
{
    public string Id { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public DateTime StatusDate { get; init; }
    public string TimeZoneId { get; init; } = string.Empty;
    public DateTime UpdatedUtc { get; init; }
    public string UpdatedBy { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}
