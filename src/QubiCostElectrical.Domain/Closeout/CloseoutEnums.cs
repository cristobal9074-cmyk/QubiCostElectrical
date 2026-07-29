namespace QubiCostElectrical;

public enum CloseoutStatus
{
    Draft, InPreparation, UnderReview, Blocked,
    ReadyForProvisionalReception, ApprovedForReception,
    Rejected, Cancelled, Archived
}

public enum ChecklistItemStatus
{
    Pending, InProgress, SubmittedForVerification,
    Verified, Rejected, NotApplicable, Blocked, Overdue
}

public enum PunchItemStatus
{
    Open, Assigned, InProgress, WaitingVerification,
    Rejected, Closed, Reopened, Cancelled
}

public enum NonConformanceStatus
{
    Open, UnderAnalysis, ActionRequired,
    InCorrection, WaitingVerification,
    Closed, Reopened, Cancelled
}

public enum DocumentStatus
{
    Required, PendingSubmission, Submitted,
    UnderReview, Approved, Rejected
}

public enum EvidenceStatus
{
    Pending, Submitted, Verified, Rejected
}

public enum CloseoutAlertSeverity
{
    Info, Warning, Critical, Blocking
}

public enum DataQualityStatus
{
    Complete, Partial, Outdated, Inconsistent,
    MissingSource, MissingExchangeRate,
    IntegrityInvalid, NotApplicable
}

public enum CloseoutDimension
{
    Technical, Documental, Economic, Contractual,
    Purchasing, Planning
}

public enum CloseoutExceptionStatus
{
    Requested, Approved, Rejected, Expired, Revoked
}

public enum MeetingStatus
{
    Scheduled, InProgress, Completed, Cancelled
}
