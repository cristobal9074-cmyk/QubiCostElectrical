#nullable enable
namespace QubiCostElectrical;

public sealed class ProjectCloseout
{
	public required CloseoutId Id { get; init; }

	public string ProjectId { get; set; } = string.Empty;

	public string Code { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string Description { get; set; } = string.Empty;

	public string Responsible { get; set; } = string.Empty;

	public string TargetDate { get; set; } = string.Empty;

	public string StatusDate { get; set; } = string.Empty;

	public CloseoutStatus Status { get; set; }

	public double ReadinessPct { get; set; }

	public string RiskLevel { get; set; } = string.Empty;

	public string ActiveChecklistId { get; set; } = string.Empty;

	public int PunchItemCount { get; set; }

	public int NonConformanceCount { get; set; }

	public int PendingDocuments { get; set; }

	public int Blockers { get; set; }

	public string Comment { get; set; } = string.Empty;

	public int Version { get; set; } = 1;

	public string Hash { get; set; } = string.Empty;

	public string CreatedUtc { get; set; } = string.Empty;

	public string CreatedBy { get; set; } = string.Empty;

	public string ModifiedUtc { get; set; } = string.Empty;

	public string ModifiedBy { get; set; } = string.Empty;

	public string RowVersion { get; set; } = string.Empty;
}
