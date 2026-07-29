using System;

namespace QubiCostElectrical;

public sealed class BudgetLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BudgetItemId { get; set; } = string.Empty;
    public string? ApuId { get; set; }
    public AllocationMethod AllocationMethod { get; set; }
    public decimal AllocationValue { get; set; }
    public bool IsPrimary { get; set; }
}
