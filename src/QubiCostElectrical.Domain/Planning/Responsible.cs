using System;

namespace QubiCostElectrical;

public sealed class Responsible
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ResponsibleType Type { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? InternalUserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string Remarks { get; set; } = string.Empty;
}
