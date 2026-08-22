using System;

namespace QubiCostElectrical;

public sealed record ApuCatalogEntry
{
	public string ApuId { get; init; } = Guid.NewGuid().ToString("N");

	public string Code { get; init; } = "";

	public string Name { get; init; } = "";

	public string Description { get; init; } = "";

	public string Category { get; init; } = "";

	public string Unit { get; init; } = "UN";

	public int VersionCount { get; init; }

	public int ActiveVersionNumber { get; init; }

	public string ActiveVersionId { get; init; } = "";

	public ApuVersionStatus ActiveVersionStatus { get; init; }

	public DateTime LastModifiedAt { get; init; }

	public decimal LastPrice { get; init; }

	public string ApprovedVersionId { get; init; } = "";

	public int ApprovedVersionNumber { get; init; }

	public string Owner { get; init; } = "";

	public string Tags { get; init; } = "";

	public bool IsActive { get; init; } = true;
}
