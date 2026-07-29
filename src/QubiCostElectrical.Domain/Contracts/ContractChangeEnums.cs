namespace QubiCostElectrical;

public enum ChangeRequestStatus
{
    Draft = 0,
    Submitted = 1,
    UnderAnalysis = 2,
    QuotationRequired = 3,
    Negotiation = 4,
    Approved = 5,
    Rejected = 6,
    ConvertedToChangeOrder = 7,
    Cancelled = 8,
    Closed = 9
}

public enum ChangeRequestUrgency
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum ChangeOrderStatus
{
    Draft = 0,
    PendingReview = 1,
    UnderNegotiation = 2,
    PendingApproval = 3,
    Approved = 4,
    Rejected = 5,
    Implemented = 6,
    Closed = 7,
    Cancelled = 8,
    Superseded = 9
}

public enum ExtraWorkStatus
{
    Draft = 0,
    Instructed = 1,
    InExecution = 2,
    Submitted = 3,
    Approved = 4,
    Rejected = 5,
    Valued = 6,
    Closed = 7
}

public enum ClaimType
{
    ScopeChange = 0,
    ClientDelay = 1,
    Interference = 2,
    AccessRestriction = 3,
    DesignChange = 4,
    ExceptionalWeather = 5,
    ForceMajeure = 6,
    Suspension = 7,
    Acceleration = 8,
    ProductivityLoss = 9,
    Other = 10
}

public enum ClaimStatus
{
    Draft = 0,
    Notified = 1,
    Submitted = 2,
    UnderReview = 3,
    InformationRequested = 4,
    Negotiation = 5,
    Accepted = 6,
    PartiallyAccepted = 7,
    Rejected = 8,
    Withdrawn = 9,
    Closed = 10
}

public enum ChangeRequestSource
{
    Client = 0,
    Internal = 1,
    Design = 2,
    Regulation = 3,
    Technical = 4,
    Procurement = 5,
    Construction = 6,
    Other = 7
}

public enum ExtraWorkOrigin
{
    ClientInstruction = 0,
    FieldCondition = 1,
    DesignChange = 2,
    Unforeseen = 3,
    Regulatory = 4,
    Emergency = 5,
    Other = 6
}
