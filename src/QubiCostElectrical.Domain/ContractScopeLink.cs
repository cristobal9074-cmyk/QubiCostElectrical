#nullable enable
using System;

namespace QubiCostElectrical;

public sealed class ContractScopeLink
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public required ContractId ContractId { get; init; }

    public string ScopeType { get; set; } = string.Empty;

    public string ReferenceId { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount => Quantity * UnitPrice;
}
