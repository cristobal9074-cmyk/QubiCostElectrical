using System.Collections.Generic;

namespace QubiCostElectrical;

public sealed class ProjectReception
{
    public ProjectReceptionId Id { get; init; } = null!;
    public string ProjectId { get; set; } = string.Empty;
    public string CloseoutId { get; set; } = string.Empty;
    public ReceptionType ReceptionType { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ScheduledDate { get; set; } = string.Empty;
    public string ActualDate { get; set; } = string.Empty;
    public string Client { get; set; } = string.Empty;
    public string Contractor { get; set; } = string.Empty;
    public string Inspector { get; set; } = string.Empty;
    public string ResponsibleTeam { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string DocumentRefs { get; set; } = string.Empty;
    public ReceptionStatus Status { get; set; } = ReceptionStatus.Draft;
    public string ApprovedBy { get; set; } = string.Empty;
    public string ApprovalDate { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedUtc { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReceptionInspection
{
    public ReceptionInspectionId Id { get; init; } = null!;
    public ProjectReceptionId ReceptionId { get; set; } = null!;
    public string InspectionDate { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Attendees { get; set; } = string.Empty;
    public string Inspector { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string InspectedItems { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public string PhotoRefs { get; set; } = string.Empty;
    public string DocumentRefs { get; set; } = string.Empty;
    public string PunchItemIds { get; set; } = string.Empty;
    public string NonConformanceIds { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string ApprovalSignature { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class ReceptionObservation
{
    public ReceptionObservationId Id { get; init; } = null!;
    public ProjectReceptionId ReceptionId { get; set; } = null!;
    public int Number { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PunchItemId { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
