using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed record ContractFinancialSummary
{
    public decimal OriginalAmount { get; init; }
    public decimal CurrentAmount { get; init; }
    public decimal ChangeOrdersAmount { get; init; }
    public decimal AdjustmentsAmount { get; init; }
    public decimal ApprovedDeductions { get; init; }
    public decimal PresentedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public decimal InvoicedAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal AdvanceOriginal { get; init; }
    public decimal AdvanceAmortized { get; init; }
    public decimal RetentionAccumulated { get; init; }
    public decimal RetentionReleased { get; init; }
    public decimal ClaimsRequested { get; init; }
    public decimal ClaimsRecognized { get; init; }
    public decimal PenaltiesProposed { get; init; }
    public decimal PenaltiesApplied { get; init; }
    public decimal BalanceToValue { get; init; }
    public decimal ContractualBalance { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractTimeSummary
{
    public DateOnly OriginalEndDate { get; init; }
    public DateOnly CurrentEndDate { get; init; }
    public DateOnly? ForecastEndDate { get; init; }
    public int OriginalDurationDays { get; init; }
    public int AddedDays { get; init; }
    public int ApprovedChangeDays { get; init; }
    public int ClaimDaysRequested { get; init; }
    public int ClaimDaysRecognized { get; init; }
    public int DaysElapsed { get; init; }
    public decimal ProgressPct { get; init; }
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractChangeSummary
{
    public int OpenRequests { get; init; }
    public int PendingChanges { get; init; }
    public int ApprovedChanges { get; init; }
    public int ImplementedChanges { get; init; }
    public decimal PendingAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public int DaysRequested { get; init; }
    public int DaysApproved { get; init; }
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractPaymentSummary
{
    public decimal PresentedAmount { get; init; }
    public decimal ObservedAmount { get; init; }
    public decimal ApprovedAmount { get; init; }
    public decimal InvoicedAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal AccumulatedAmount { get; init; }
    public decimal BalanceToValue { get; init; }
    public decimal ContractualBalance { get; init; }
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractRiskSummary
{
    public int OpenClaims { get; init; }
    public int NegotiatingClaims { get; init; }
    public int AcceptedClaims { get; init; }
    public decimal ClaimedAmount { get; init; }
    public decimal RecognizedAmount { get; init; }
    public int ExpiringGuarantees { get; init; }
    public int ExpiredGuarantees { get; init; }
    public int PendingPenalties { get; init; }
    public int OverdueInvoices { get; init; }
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractAdministrationSummary
{
    public int ApprovedAdjustments { get; init; }
    public int ProposedPenalties { get; init; }
    public int AppliedPenalties { get; init; }
    public int ActiveGuarantees { get; init; }
    public int ExpiringGuarantees { get; init; }
    public int ExpiredGuarantees { get; init; }
    public int PendingInvoices { get; init; }
    public int OverdueInvoices { get; init; }
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractEvmSummary
{
    public decimal Bac { get; init; }
    public decimal Eac { get; init; }
    public decimal Etc { get; init; }
    public decimal Vac { get; init; }
    public decimal Cpi { get; init; }
    public decimal Spi { get; init; }
    public DateOnly? ForecastCompletion { get; init; }
    public ContractDashboardDataQuality Quality { get; init; }
}

public sealed record ContractDashboardKpi
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public ContractDashboardKpiStatus Status { get; init; }
    public ContractDashboardTrend Trend { get; init; }
    public string Interpretation { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public sealed record ContractDashboardTrendData
{
    public string PeriodLabel { get; init; } = string.Empty;
    public DateOnly PeriodDate { get; init; }
    public decimal Value { get; init; }
}

public sealed record ContractDashboardSeries
{
    public string SeriesCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public List<ContractDashboardTrendData> Points { get; init; } = new();
}

public sealed record ContractAlert
{
    public string AlertId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public ContractAlertSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ContractId ContractId { get; init; } = null!;
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Responsible { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public ContractAlertStatus Status { get; init; }
    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? ResolvedUtc { get; init; }
    public string? Resolution { get; init; }
}

public sealed record ContractDashboardSnapshot
{
    public string SnapshotId { get; init; } = string.Empty;
    public ContractId ContractId { get; init; } = null!;
    public int Version { get; init; }
    public DateTime StatusDate { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string Comment { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
}

public sealed record ContractComparisonResult
{
    public ContractComparisonScope Scope { get; init; }
    public string LabelA { get; init; } = string.Empty;
    public string LabelB { get; init; } = string.Empty;
    public decimal AmountChange { get; init; }
    public int DaysChange { get; init; }
    public List<string> NewChanges { get; init; } = new();
    public List<string> ResolvedClaims { get; init; } = new();
    public List<string> NewAlerts { get; init; } = new();
    public List<string> Findings { get; init; } = new();
}

public sealed record ContractExecutiveDashboard
{
    public ContractId ContractId { get; init; } = null!;
    public string ContractCode { get; init; } = string.Empty;
    public string ContractName { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateTime StatusDate { get; init; }
    public ContractDashboardDataQuality DataQuality { get; init; }
    public string ExecutiveSummary { get; init; } = string.Empty;
    public ContractFinancialSummary FinancialSummary { get; init; } = null!;
    public ContractTimeSummary TimeSummary { get; init; } = null!;
    public ContractChangeSummary ChangeSummary { get; init; } = null!;
    public ContractPaymentSummary PaymentSummary { get; init; } = null!;
    public ContractRiskSummary RiskSummary { get; init; } = null!;
    public ContractAdministrationSummary AdministrationSummary { get; init; } = null!;
    public ContractEvmSummary? EvmSummary { get; init; }
    public List<ContractDashboardKpi> Kpis { get; init; } = new();
    public List<ContractAlert> Alerts { get; init; } = new();
    public List<ContractDashboardSeries> Series { get; init; } = new();
    public DateTime GeneratedUtc { get; init; }
}
