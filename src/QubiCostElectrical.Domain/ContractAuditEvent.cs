using System;

namespace QubiCostElectrical;

public sealed class ContractAuditEvent
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	public required ContractId ContractId { get; init; }

	public string Action { get; set; } = string.Empty;

	public string Actor { get; set; } = string.Empty;

	public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

	public string DetailsJson { get; set; } = "{}";
}
