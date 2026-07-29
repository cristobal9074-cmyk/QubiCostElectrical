namespace QubiCostElectrical;

public enum SupplierStatus
{
    Prospect = 0,
    Active = 1,
    Suspended = 2,
    Blocked = 3,
    Inactive = 4,
    Archived = 5
}

public enum SupplyCategoryKind
{
    Materials = 0,
    Equipment = 1,
    Tools = 2,
    Subcontracts = 3,
    Transport = 4,
    Services = 5,
    Rental = 6,
    Engineering = 7,
    Other = 8,
    Custom = 9
}

public enum PurchaseRequisitionStatus
{
    Draft = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    InQuotation = 4,
    PartiallyFulfilled = 5,
    Completed = 6,
    Cancelled = 7,
    Archived = 8
}

public enum PurchasePriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3,
    Critical = 4
}

public enum NeedOriginKind
{
    Manual = 0,
    Budget = 1,
    Apu = 2,
    Activity = 3,
    Schedule = 4,
    InsufficientStock = 5,
    ApprovedChange = 6,
    Other = 7
}

public enum RequisitionItemStatus
{
    Open = 0,
    Consolidated = 1,
    InRfq = 2,
    Fulfilled = 3,
    Cancelled = 4
}

public enum RfqStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Issued = 3,
    Open = 4,
    PartiallyResponded = 5,
    Closed = 6,
    Cancelled = 7,
    Archived = 8
}

public enum RfqInvitationStatus
{
    Pending = 0,
    Sent = 1,
    Confirmed = 2,
    Declined = 3,
    NoResponse = 4,
    Responded = 5
}

public enum QuotationResponseStatus
{
    Received = 0,
    UnderReview = 1,
    Rejected = 2,
    Archived = 3
}

public enum SupplierOfferStatus
{
    Draft = 0,
    Submitted = 1,
    UnderEvaluation = 2,
    Compared = 3,
    Awarded = 4,
    PartiallyAwarded = 5,
    Rejected = 6,
    Expired = 7,
    Archived = 8
}

public enum TechnicalComplianceLevel
{
    NotEvaluated = 0,
    NonCompliant = 1,
    Partial = 2,
    Compliant = 3,
    Exceeds = 4
}

public enum CommercialRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum AwardKind
{
    Total = 0,
    Partial = 1,
    ByItem = 2,
    ByQuantity = 3,
    MultiSupplier = 4
}

public enum AwardStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Archived = 5
}

public enum BidComparisonStatus
{
    Draft = 0,
    Ready = 1,
    RecommendationIssued = 2,
    Applied = 3,
    Archived = 4
}

public enum ProcurementDocumentKind
{
    Specification = 0,
    Drawing = 1,
    Datasheet = 2,
    CommercialTerms = 3,
    Offer = 4,
    Email = 5,
    Other = 6
}

public enum ProcurementValidationSeverity
{
    Error = 0,
    Warning = 1,
    Info = 2
}

public enum PurchaseOrderStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Issued = 3,
    ConfirmedBySupplier = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7,
    Closed = 8,
    Archived = 9
}

public enum GoodsReceiptStatus
{
    Draft = 0,
    Inspecting = 1,
    Accepted = 2,
    PartiallyAccepted = 3,
    Rejected = 4,
    Cancelled = 5
}

public enum ReceiptLineDisposition
{
    Accepted = 0,
    Rejected = 1,
    Damaged = 2,
    Shortage = 3,
    Surplus = 4
}

public enum InventoryMovementKind
{
    ReceiptEntry = 0,
    ReturnExit = 1,
    Reservation = 2,
    ReservationRelease = 3,
    Issue = 4,
    Adjustment = 5,
    InTransit = 6
}

public enum GoodsReturnStatus
{
    Draft = 0,
    Confirmed = 1,
    Cancelled = 2
}
