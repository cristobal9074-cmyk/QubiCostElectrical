using System;

namespace QubiCostElectrical;

public sealed class Constraint
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ConstraintType Type { get; set; }
    public DateTime? ConstraintDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
