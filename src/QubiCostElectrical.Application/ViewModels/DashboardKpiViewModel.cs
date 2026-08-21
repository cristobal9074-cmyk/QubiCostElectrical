namespace QubiCostElectrical;

public sealed record DashboardKpiViewModel(
    string Title,
    string MetricKey,
    decimal CurrentValue,
    string FormattedValue,
    string Caption);
