#nullable enable
namespace QubiCostElectrical;

public sealed class CloseoutSnapshot
{
	public required CloseoutSnapshotId Id { get; init; }

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
