using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ChangeOrder
{
    public ChangeOrderId Id { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public ChangeRequestId? ChangeRequestId { get; set; }
    public string VisibleNumber { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime EffectiveDate { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public decimal ChangeAmount { get; set; }
    public int AddedDays { get; set; }
    public string AddedItems { get; set; } = string.Empty;
    public string ModifiedItems { get; set; } = string.Empty;
    public string RemovedItems { get; set; } = string.Empty;
    public string AffectedActivities { get; set; } = string.Empty;
    public string AffectedMilestones { get; set; } = string.Empty;
    public decimal CostImpact { get; set; }
    public int ScheduleImpactDays { get; set; }
    public decimal ForecastImpact { get; set; }
    public decimal EvmImpact { get; set; }
    public ChangeOrderStatus Status { get; set; } = ChangeOrderStatus.Draft;
    public string Approvers { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

public sealed class ChangeOrderLine
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ChangeOrderId ChangeOrderId { get; init; } = null!;
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string ChangeType { get; set; } = "Add"; // Add, Modify, Remove
    public string Comment { get; set; } = string.Empty;
}

public sealed class ChangeOrderVersion
{
    public ChangeOrderVersionId Id { get; init; } = null!;
    public ChangeOrderId ChangeOrderId { get; init; } = null!;
    public int VersionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string HashSha256 { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string ChangeReason { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; init; }
}

public sealed class ChangeOrderSnapshot
{
    public ChangeOrderSnapshotId Id { get; init; } = null!;
    public ChangeOrderId ChangeOrderId { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public string ProjectPath { get; set; } = string.Empty;
    public DateTime AsOfUtc { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
}

public sealed class ChangeOrderAuditEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ChangeOrderId ChangeOrderId { get; init; } = null!;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
}
