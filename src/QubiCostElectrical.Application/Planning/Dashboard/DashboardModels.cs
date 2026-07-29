using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class DashboardKpi
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public decimal? Value { get; init; }
    public string DisplayValue { get; init; } = "Sin datos";
    public string Unit { get; init; } = string.Empty;
    public DashboardKpiStatus Status { get; init; } = DashboardKpiStatus.NoData;
    public string Interpretation { get; init; } = string.Empty;
    public DashboardTrendDirection Trend { get; init; } = DashboardTrendDirection.Unknown;
    public decimal? PreviousValue { get; init; }
    public decimal? Delta { get; init; }
    public DateTime? AsOfDate { get; init; }
    public string Source { get; init; } = string.Empty;
    public DashboardDataQuality DataQuality { get; init; } = DashboardDataQuality.Partial;
}

public sealed class DashboardDataPoint
{
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public decimal? Value { get; init; }
    public bool HasData { get; init; }
}

public sealed class DashboardSeries
{
    public DashboardSeriesKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public DashboardSeriesGranularity Granularity { get; init; }
    public DashboardSeriesMode Mode { get; init; }
    public DashboardSeriesUnit Unit { get; init; }
    public string CurrencyCode { get; init; } = "CLP";
    public IReadOnlyList<DashboardDataPoint> Points { get; init; } = Array.Empty<DashboardDataPoint>();
}

public sealed class DashboardAlert
{
    public string AlertId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public string Code { get; init; } = string.Empty;
    public DashboardAlertSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public DateTime RaisedUtc { get; init; }
    public string? Responsible { get; init; }
    public string RecommendedAction { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public int ImpactScore { get; init; }
    public string? WbsCode { get; init; }
    public DashboardAlertLifecycle Lifecycle { get; init; } = DashboardAlertLifecycle.Open;
    public string? LifecycleNote { get; init; }
    public string? LifecycleBy { get; init; }
    public DateTime? LifecycleUtc { get; init; }
}

public sealed class DashboardDeviationItem
{
    public string RankKey { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public decimal AbsoluteImpact { get; init; }
    public decimal? PercentImpact { get; init; }
    public string Metric { get; init; } = string.Empty;
    public string? Responsible { get; init; }
}

public sealed class DashboardWbsNode
{
    public string NodeId { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public decimal PlannedProgressPct { get; init; }
    public decimal PhysicalProgressPct { get; init; }
    public decimal FinancialProgressPct { get; init; }
    public decimal Pv { get; init; }
    public decimal Ev { get; init; }
    public decimal Ac { get; init; }
    public decimal? Spi { get; init; }
    public decimal? Cpi { get; init; }
    public decimal ForecastCost { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public int CriticalCount { get; init; }
    public int AlertCount { get; init; }
    public DashboardTrendDirection Trend { get; init; }
    public decimal Weight { get; init; }
}

public sealed class DashboardResponsibleSummary
{
    public string Responsible { get; init; } = "Sin asignar";
    public int AssignedCount { get; init; }
    public int CompletedCount { get; init; }
    public int DelayedCount { get; init; }
    public int CriticalCount { get; init; }
    public decimal PhysicalProgressPct { get; init; }
    public decimal CostAc { get; init; }
    public decimal? Spi { get; init; }
    public decimal? Cpi { get; init; }
    public int AlertCount { get; init; }
    public int PendingCount { get; init; }
}

public sealed class DashboardFilter
{
    public DateTime? PeriodFrom { get; init; }
    public DateTime? PeriodTo { get; init; }
    public DateTime? StatusDate { get; init; }
    public string? BaselineId { get; init; }
    public string? WbsNodeId { get; init; }
    public string? Responsible { get; init; }
    public bool? CriticalOnly { get; init; }
    public bool? DelayedOnly { get; init; }
    public bool? OverCostOnly { get; init; }
    public bool? OutOfSequenceOnly { get; init; }
    public bool? WithoutUpdateOnly { get; init; }
    public DashboardAlertSeverity? MinSeverity { get; init; }
    public string? AlertCode { get; init; }
    public DashboardSeriesGranularity Granularity { get; init; } = DashboardSeriesGranularity.Weekly;
    public DashboardSeriesMode SeriesMode { get; init; } = DashboardSeriesMode.Cumulative;
    public DashboardSeriesUnit SeriesUnit { get; init; } = DashboardSeriesUnit.Cost;
    public string CurrencyCode { get; init; } = "CLP";
}

public sealed class DashboardComparison
{
    public string LeftLabel { get; init; } = string.Empty;
    public string RightLabel { get; init; } = string.Empty;
    public IReadOnlyList<DashboardKpi> LeftKpis { get; init; } = Array.Empty<DashboardKpi>();
    public IReadOnlyList<DashboardKpi> RightKpis { get; init; } = Array.Empty<DashboardKpi>();
    public IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();
}

public sealed class DashboardDrillDownTarget
{
    public string Kind { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string SuggestedRoute { get; init; } = string.Empty;
}

public sealed class ProjectExecutiveDashboard
{
    public ScheduleId ScheduleId { get; init; } = null!;
    public string ProjectName { get; init; } = string.Empty;
    public DateTime? StatusDate { get; init; }
    public string? ActiveBaselineId { get; init; }
    public string? ActiveBaselineName { get; init; }
    public DateTime? BaselineFinish { get; init; }
    public DateTime? PlanFinish { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public string CurrencyCode { get; init; } = "CLP";
    public DashboardDataQuality DataQuality { get; init; }
    public DateTime GeneratedUtc { get; init; }
    public IReadOnlyList<DashboardKpi> Kpis { get; init; } = Array.Empty<DashboardKpi>();
    public IReadOnlyList<DashboardSeries> Series { get; init; } = Array.Empty<DashboardSeries>();
    public IReadOnlyList<DashboardAlert> Alerts { get; init; } = Array.Empty<DashboardAlert>();
    public IReadOnlyList<DashboardDeviationItem> Deviations { get; init; } = Array.Empty<DashboardDeviationItem>();
    public IReadOnlyList<DashboardWbsNode> WbsNodes { get; init; } = Array.Empty<DashboardWbsNode>();
    public IReadOnlyList<DashboardResponsibleSummary> ByResponsible { get; init; } = Array.Empty<DashboardResponsibleSummary>();
    public IReadOnlyList<DashboardOperationalRow> OperationalRows { get; init; } = Array.Empty<DashboardOperationalRow>();
    public string ExecutiveSummary { get; init; } = string.Empty;
    public string SourceHashes { get; init; } = string.Empty;
}

public sealed class DashboardOperationalRow
{
    public string ActivityId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? WbsCode { get; init; }
    public string? Responsible { get; init; }
    public bool IsCritical { get; init; }
    public decimal PercentComplete { get; init; }
    public decimal? ScheduleVarianceDays { get; init; }
    public decimal ActualCost { get; init; }
    public decimal? CommittedCost { get; init; }
    public DateTime? ForecastFinish { get; init; }
    public bool OutOfSequence { get; init; }
    public bool WithoutUpdate { get; init; }
    public bool IsBlocked { get; init; }
    public int AlertCount { get; init; }
}

public sealed class DashboardSnapshot
{
    public string SnapshotId { get; init; } = null!;
    public ScheduleId ScheduleId { get; init; } = null!;
    public int Version { get; init; }
    public DateTime StatusDate { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public string SHA256 { get; init; } = string.Empty;
    public string EngineVersions { get; init; } = string.Empty;
    public string SourceHashes { get; init; } = string.Empty;
    public ProjectExecutiveDashboard Payload { get; init; } = null!;
}

public sealed class DashboardUserPreferences
{
    public string UserKey { get; init; } = string.Empty;
    public ScheduleId? ScheduleId { get; init; }
    public DashboardFilter Filter { get; init; } = new();
    public DashboardTrafficLightThresholds Thresholds { get; init; } = new();
    public DateTime UpdatedUtc { get; init; }
}
