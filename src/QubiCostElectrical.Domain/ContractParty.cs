#nullable enable
using System;

namespace QubiCostElectrical;

public sealed class ContractParty
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	public required ContractId ContractId { get; init; }

	public ContractPartyRole Role { get; set; }

	public string Name { get; set; } = string.Empty;

	public string TaxId { get; set; } = string.Empty;

	public string LegalRepresentative { get; set; } = string.Empty;

	public string TechnicalContact { get; set; } = string.Empty;

	public string Email { get; set; } = string.Empty;

	public string Phone { get; set; } = string.Empty;
}
