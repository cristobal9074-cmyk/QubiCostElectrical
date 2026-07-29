using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ChangeRequest
{
    public ChangeRequestId Id { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public string VisibleNumber { get; set; } = string.Empty;
    public ChangeRequestSource Source { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Cause { get; set; } = string.Empty;
    public string Requester { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public ChangeRequestUrgency Urgency { get; set; } = ChangeRequestUrgency.Normal;
    public string WbsCode { get; set; } = string.Empty;
    public string AffectedActivities { get; set; } = string.Empty;
    public string AffectedItems { get; set; } = string.Empty;
    public string SupportingDocuments { get; set; } = string.Empty;
    public decimal PreliminaryCostImpact { get; set; }
    public int PreliminaryDaysImpact { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Draft;
    public string Responsible { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}
