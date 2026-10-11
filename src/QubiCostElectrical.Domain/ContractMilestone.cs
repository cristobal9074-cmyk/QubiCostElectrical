#nullable enable
using System;

namespace QubiCostElectrical;

public sealed class ContractMilestone
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required ContractId ContractId { get; init; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime ContractualDate { get; set; }

    public string MilestoneType { get; set; } = string.Empty;

    public decimal RelatedAmount { get; set; }

    public string ActivityId { get; set; } = string.Empty;

    public string RequiredEvidence { get; set; } = string.Empty;

    public string Status { get; set; } = "Pendiente";

    public int ToleranceDays { get; set; }

    public string ResponsibleName { get; set; } = string.Empty;
}
