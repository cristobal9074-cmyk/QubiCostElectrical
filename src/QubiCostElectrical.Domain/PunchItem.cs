#nullable enable
namespace QubiCostElectrical;

public sealed class PunchItem
{
	public required PunchItemId Id { get; init; }

	public string ProjectId { get; set; } = string.Empty;

	public string CloseoutId { get; set; } = string.Empty;

	public int Number { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;

	public string Priority { get; set; } = string.Empty;

	public string Severity { get; set; } = string.Empty;

	public string WbsCode { get; set; } = string.Empty;

	public string ActivityId { get; set; } = string.Empty;

	public string ContractId { get; set; } = string.Empty;

	public string Responsible { get; set; } = string.Empty;

	public string DetectedDate { get; set; } = string.Empty;

	public string DueDate { get; set; } = string.Empty;

	public string CorrectedDate { get; set; } = string.Empty;

	public string VerifiedDate { get; set; } = string.Empty;

	public PunchItemStatus Status { get; set; }

	public decimal EstimatedCostImpact { get; set; }

	public double EstimatedScheduleImpact { get; set; }

	public string LinkedEvidenceIds { get; set; } = string.Empty;

	public string Comment { get; set; } = string.Empty;

	public string RowVersion { get; set; } = string.Empty;
}
