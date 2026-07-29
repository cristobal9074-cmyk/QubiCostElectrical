namespace QubiCostElectrical;

public enum DashboardKpiStatus
{
    Good,
    Warning,
    Critical,
    Neutral,
    NoData,
    NotApplicable
}

public enum DashboardDataQuality
{
    Complete,
    Partial,
    Outdated,
    Inconsistent,
    NoBaseline,
    NoCosts,
    NoProgress,
    InvalidIntegrity
}

public enum DashboardTrendDirection
{
    Up,
    Down,
    Flat,
    Unknown
}

public enum DashboardSeriesGranularity
{
    Daily,
    Weekly,
    Monthly
}

public enum DashboardSeriesMode
{
    Cumulative,
    Period
}

public enum DashboardSeriesUnit
{
    Cost,
    Percent,
    Quantity
}

public enum DashboardAlertSeverity
{
    Critical = 0,
    High = 1,
    Medium = 2,
    Low = 3,
    Info = 4
}

public enum DashboardAlertLifecycle
{
    Open,
    Reviewed,
    Acknowledged,
    Resolved,
    Dismissed
}

public enum DashboardSeriesKind
{
    PvCumulative,
    EvCumulative,
    AcCumulative,
    CommittedCumulative,
    ForecastCumulative,
    BaselineCumulative,
    PhysicalProgressCumulative,
    PlannedProgressCumulative
}
