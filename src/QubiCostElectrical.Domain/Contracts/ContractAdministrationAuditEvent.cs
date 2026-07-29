using System;

namespace QubiCostElectrical;

public sealed class ContractAdministrationAuditEvent
{
    public ContractAdministrationAuditEventId Id { get; init; } = null!;
    public ContractId ContractId { get; set; } = null!;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
    public bool IsDeniedAttempt { get; set; }
}
