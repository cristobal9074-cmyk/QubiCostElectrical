namespace QubiCostElectrical;

public enum ContractDashboardDataQuality
{
    Complete,
    Partial,
    Outdated,
    Inconsistent,
    IntegrityInvalid,
    MissingExchangeRate,
    MissingBaseline,
    MissingStatusDate,
    NotApplicable
}

public enum ContractAlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum ContractAlertStatus
{
    New,
    Acknowledged,
    Resolved,
    Dismissed
}

public enum ContractDashboardKpiStatus
{
    Good,
    Warning,
    Critical,
    NoData
}

public enum ContractDashboardTrend
{
    Improving,
    Stable,
    Worsening,
    NoData
}

public enum ContractComparisonScope
{
    StatusDate,
    SnapshotVsPrevious,
    OriginalVsCurrent,
    CurrentVsForecast
}
