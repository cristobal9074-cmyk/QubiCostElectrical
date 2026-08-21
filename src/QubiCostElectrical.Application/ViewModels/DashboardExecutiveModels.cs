namespace QubiCostElectrical;

public enum DashboardExecutiveKpiAccent
{
    Primary,
    Blue,
    Orange,
    RiskHigh,
    RiskMedium,
    RiskLow
}

public enum DashboardExecutiveKpiValueKind
{
    Money,
    Text,
    ConfigurationHint
}

public sealed record DashboardExecutiveKpiDefinition(
    string Title,
    string Caption,
    string HelpTooltip,
    bool IsPrimary,
    DashboardExecutiveKpiAccent Accent,
    DashboardExecutiveKpiValueKind ValueKind,
    decimal? MoneyValueUf,
    string? TextValue);

public sealed record DashboardExecutivePartidaRow(
    string Item,
    string Partida,
    decimal TotalUf);

public sealed record DashboardExecutiveMaterialRow(
    string Codigo,
    string Descripcion,
    string Unidad,
    decimal Cantidad,
    decimal TotalUf);

public sealed record DashboardSensitivityPoint(
    decimal MaterialVariationPct,
    decimal TotalVentaUf);

public sealed record DashboardExecutiveViewModel(
    IReadOnlyList<DashboardExecutiveKpiDefinition> Kpis,
    IReadOnlyList<DashboardExecutivePartidaRow> TopPartidas,
    IReadOnlyList<DashboardExecutiveMaterialRow> TopMateriales,
    IReadOnlyList<DashboardSensitivityPoint> SensitivityCurve,
    decimal MaterialImpact10Pct);
