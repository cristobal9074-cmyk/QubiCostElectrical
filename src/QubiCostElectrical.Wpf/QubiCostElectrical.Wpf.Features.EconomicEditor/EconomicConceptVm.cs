using System;

namespace QubiCostElectrical.Wpf.Features.EconomicEditor;

public sealed record EconomicConceptVm
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N");

	public string ConceptCode { get; set; } = "";

	public string ConceptName { get; set; } = "";

	public string Category { get; set; } = "Indirecto";

	public string ConceptType { get; set; } = "Porcentaje";

	public string BaseType { get; set; } = "SubtotalAnterior";

	public decimal RateOrAmount { get; set; }

	public int SortOrder { get; set; }

	public bool IsInformative { get; set; }

	public bool IncludedInTotal { get; set; } = true;

	public bool IsActive { get; set; } = true;

	public decimal CalculatedValue { get; set; }

	public decimal SubtotalBefore { get; set; }

	public decimal SubtotalAfter { get; set; }
}
