using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ProjectFinalResult
{
    public ProjectFinalResultId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public string GenerationDate { get; set; } = string.Empty;
    public string ApplicationVersion { get; set; } = string.Empty;
    public string SourceHashes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public ProjectFinalFinancialResult Financial { get; set; } = new();
    public ProjectFinalScheduleResult Schedule { get; set; } = new();
    public ProjectFinalContractResult Contracts { get; set; } = new();
    public ProjectFinalProcurementResult Procurement { get; set; } = new();
    public ProjectFinalQualityResult Quality { get; set; } = new();
    public List<ProjectFinalKpi> Kpis { get; set; } = new();
    public List<ProjectFinalAlert> Alerts { get; set; } = new();
}

public sealed class ProjectFinalFinancialResult
{
    public decimal OriginalBudget { get; set; }
    public decimal CurrentBudget { get; set; }
    public decimal FinalCost { get; set; }
    public decimal FinalResult { get; set; }
    public decimal AbsoluteVariance { get; set; }
    public decimal PercentVariance { get; set; }
    public decimal FinalCommitted { get; set; }
    public decimal FinalValued { get; set; }
    public decimal FinalInvoiced { get; set; }
    public decimal FinalPaid { get; set; }
    public decimal FinalBalance { get; set; }
    public decimal Vac { get; set; }
    public decimal Cpi { get; set; } = 1;
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class ProjectFinalScheduleResult
{
    public string PlannedStart { get; set; } = string.Empty;
    public string ActualStart { get; set; } = string.Empty;
    public string BaselineEnd { get; set; } = string.Empty;
    public string CurrentEnd { get; set; } = string.Empty;
    public string ActualEnd { get; set; } = string.Empty;
    public int PlannedDuration { get; set; }
    public int ActualDuration { get; set; }
    public int VarianceDays { get; set; }
    public decimal Spi { get; set; } = 1;
}

public sealed class ProjectFinalContractResult
{
    public int TotalContracts { get; set; }
    public int ClosedContracts { get; set; }
    public int ChangeOrders { get; set; }
    public int Claims { get; set; }
    public int Adjustments { get; set; }
    public int Penalties { get; set; }
    public int ActiveGuarantees { get; set; }
    public int PendingRetentions { get; set; }
}

public sealed class ProjectFinalProcurementResult
{
    public decimal TotalPurchased { get; set; }
    public decimal TotalReceived { get; set; }
    public int ClosedOrders { get; set; }
    public int LateDeliveries { get; set; }
    public decimal SavingsOvercost { get; set; }
    public decimal SurplusAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
}

public sealed class ProjectFinalQualityResult
{
    public int TotalChecklistItems { get; set; }
    public int VerifiedChecklistItems { get; set; }
    public int TotalPunchItems { get; set; }
    public int ClosedPunchItems { get; set; }
    public int TotalNonConformances { get; set; }
    public int ClosedNonConformances { get; set; }
    public int TotalDocuments { get; set; }
    public int ApprovedDocuments { get; set; }
    public bool ProvisionalReceptionDone { get; set; }
    public bool FinalReceptionDone { get; set; }
}

public sealed class ProjectFinalKpi
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DataQuality { get; set; } = string.Empty;
    public string Interpretation { get; set; } = string.Empty;
}

public sealed class ProjectFinalAlert
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Dimension { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class FinalCloseoutSnapshot
{
    public FinalSnapshotId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public string CloseDate { get; set; } = string.Empty;
    public string ProjectData { get; set; } = string.Empty;
    public string FinalReconciliation { get; set; } = string.Empty;
    public string KpiData { get; set; } = string.Empty;
    public string Receptions { get; set; } = string.Empty;
    public string ContractsData { get; set; } = string.Empty;
    public string ProcurementData { get; set; } = string.Empty;
    public string EvmData { get; set; } = string.Empty;
    public string Documents { get; set; } = string.Empty;
    public string Guarantees { get; set; } = string.Empty;
    public string Retentions { get; set; } = string.Empty;
    public string Lessons { get; set; } = string.Empty;
    public string Alerts { get; set; } = string.Empty;
    public string SourceHashes { get; set; } = string.Empty;
    public string EngineVersions { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public bool IsOfficial { get; set; }
    public int CloseoutVersion { get; set; } = 1;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProjectArchiveManifest
{
    public ProjectArchiveManifestId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutVersion { get; set; } = string.Empty;
    public string ArchiveDate { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string FileLocation { get; set; } = string.Empty;
    public string DocumentsIndex { get; set; } = string.Empty;
    public string SnapshotRef { get; set; } = string.Empty;
    public string SourceHashes { get; set; } = string.Empty;
    public string SqlSchemaVersion { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string RetentionPolicy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
}
