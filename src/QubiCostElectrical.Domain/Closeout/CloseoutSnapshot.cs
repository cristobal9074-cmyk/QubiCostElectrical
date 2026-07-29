using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class CloseoutSnapshot
{
    public CloseoutSnapshotId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string StatusDate { get; set; } = string.Empty;
    public string ChecklistData { get; set; } = string.Empty;
    public string PunchListData { get; set; } = string.Empty;
    public string NonConformanceData { get; set; } = string.Empty;
    public string DocumentData { get; set; } = string.Empty;
    public string ReconciliationData { get; set; } = string.Empty;
    public string ReadinessScore { get; set; } = string.Empty;
    public string BlockerData { get; set; } = string.Empty;
    public string ExceptionData { get; set; } = string.Empty;
    public string AlertsData { get; set; } = string.Empty;
    public string SourceHashes { get; set; } = string.Empty;
    public string ServiceVersion { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CloseoutSnapshotComparison
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public CloseoutSnapshotId BaselineId { get; set; } = null!;
    public CloseoutSnapshotId ComparisonId { get; set; } = null!;
    public double ReadinessPctChange { get; set; }
    public int ItemsResolved { get; set; }
    public int ItemsAdded { get; set; }
    public int ItemsReopened { get; set; }
    public int ItemsModified { get; set; }
    public string Impact { get; set; } = string.Empty;
    public string Trend { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
}
