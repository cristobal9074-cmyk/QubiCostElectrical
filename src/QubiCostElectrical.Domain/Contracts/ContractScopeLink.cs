using System;

namespace QubiCostElectrical;

public sealed class ContractScopeLink
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ContractId ContractId { get; init; } = null!;
    public string ScopeType { get; set; } = string.Empty; // "WBS", "Partida", "APU", "Activity", "Resource"
    public string ReferenceId { get; set; } = string.Empty; // The code or ID of the linked entity
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount => Quantity * UnitPrice;
}
