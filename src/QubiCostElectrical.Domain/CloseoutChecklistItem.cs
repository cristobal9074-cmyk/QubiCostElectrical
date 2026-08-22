#nullable enable
namespace QubiCostElectrical;

public sealed class CloseoutChecklistItem
{
	public required ChecklistItemId Id { get; init; }

	public required ChecklistId ChecklistId { get; set; }

	public string Code { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;

	public bool IsMandatory { get; set; }

	public bool IsBlocking { get; set; }

	public string Responsible { get; set; } = string.Empty;

	public string DueDate { get; set; } = string.Empty;

	public ChecklistItemStatus Status { get; set; }

	public bool EvidenceRequired { get; set; }

	public string LinkedEvidenceIds { get; set; } = string.Empty;

	public string Comment { get; set; } = string.Empty;

	public string VerifiedBy { get; set; } = string.Empty;

	public string VerificationDate { get; set; } = string.Empty;

	public string RowVersion { get; set; } = string.Empty;
}
