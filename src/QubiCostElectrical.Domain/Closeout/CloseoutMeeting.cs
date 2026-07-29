namespace QubiCostElectrical;

public sealed class CloseoutMeeting
{
    public CloseoutMeetingId Id { get; init; } = null!;
    public string CloseoutId { get; set; } = string.Empty;
    public string MeetingDate { get; set; } = string.Empty;
    public string Participants { get; set; } = string.Empty;
    public string Agenda { get; set; } = string.Empty;
    public string Agreements { get; set; } = string.Empty;
    public string PendingItems { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string LinkedDocumentIds { get; set; } = string.Empty;
    public string MinutesPath { get; set; } = string.Empty;
    public MeetingStatus Status { get; set; } = MeetingStatus.Scheduled;
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedUtc { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}
