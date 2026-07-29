using System;

namespace QubiCostElectrical;

public sealed record ActivitySearchRequest
{
    public ScheduleId? ScheduleId { get; init; }
    public string? Query { get; init; }
    public ActivityStatus? Status { get; init; }
    public ActivityType? Type { get; init; }
    public string? ResponsibleId { get; init; }
    public int MaxResults { get; init; } = 100;
    public int Offset { get; init; }
}
