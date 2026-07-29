using System;
using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ContractClaim
{
    public ClaimId Id { get; init; } = null!;
    public ContractId ContractId { get; init; } = null!;
    public string VisibleNumber { get; set; } = string.Empty;
    public ClaimType Type { get; set; }
    public string EventDescription { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime NotificationDate { get; set; }
    public string Cause { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal AmountClaimed { get; set; }
    public int DaysClaimed { get; set; }
    public decimal AmountAwarded { get; set; }
    public int DaysAwarded { get; set; }
    public string CurrencyCode { get; set; } = "CLP";
    public string AffectedActivities { get; set; } = string.Empty;
    public string WbsCode { get; set; } = string.Empty;
    public string SupportingDocuments { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime ModifiedUtc { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
}

public sealed class ClaimEvent
{
    public ClaimEventId Id { get; init; } = null!;
    public ClaimId ClaimId { get; init; } = null!;
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = string.Empty;
}

public sealed class ClaimEvidence
{
    public ClaimEvidenceId Id { get; init; } = null!;
    public ClaimId ClaimId { get; init; } = null!;
    public string DocumentReference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedUtc { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}

public sealed class ClaimEvaluation
{
    public ClaimEvaluationId Id { get; init; } = null!;
    public ClaimId ClaimId { get; init; } = null!;
    public string Evaluator { get; set; } = string.Empty;
    public DateTime EvaluationDate { get; set; }
    public decimal EvaluatedAmount { get; set; }
    public int EvaluatedDays { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}

public sealed class ClaimResponse
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public ClaimId ClaimId { get; init; } = null!;
    public string Responder { get; set; } = string.Empty;
    public DateTime ResponseDate { get; set; }
    public string ResponseType { get; set; } = string.Empty; // Accepted, PartiallyAccepted, Rejected
    public string ResponseDetail { get; set; } = string.Empty;
    public decimal OfferedAmount { get; set; }
    public int OfferedDays { get; set; }
    public string Comment { get; set; } = string.Empty;
}
