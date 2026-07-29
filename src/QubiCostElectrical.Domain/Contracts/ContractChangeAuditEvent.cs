using System;

namespace QubiCostElectrical;

public sealed class ContractChangeAuditEvent
{
    public ContractChangeAuditEventId Id { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public string EntityType { get; set; } = string.Empty; // ChangeRequest, ChangeOrder, ExtraWork, Claim
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
    public bool IsDeniedAttempt { get; set; }
}
