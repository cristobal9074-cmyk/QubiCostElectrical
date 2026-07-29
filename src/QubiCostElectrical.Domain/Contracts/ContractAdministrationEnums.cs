namespace QubiCostElectrical;

public enum AdjustmentType
{
    IPC = 0,
    UF = 1,
    CustomIndex = 2,
    PolynomialFormula = 3,
    FixedFactor = 4,
    ManualJustified = 5
}

public enum AdjustmentStatus
{
    Draft = 0,
    Calculated = 1,
    Applied = 2,
    Rejected = 3,
    Closed = 4
}

public enum GuaranteeType
{
    BidBond = 0,
    Performance = 1,
    CorrectExecution = 2,
    AdvancePayment = 3,
    GoodPerformance = 4,
    Retention = 5,
    Other = 6
}

public enum GuaranteeStatus
{
    Draft = 0,
    Active = 1,
    Renewed = 2,
    Released = 3,
    Expired = 4,
    Executed = 5,
    Cancelled = 6
}

public enum PenaltyType
{
    Delay = 0,
    NonCompliance = 1,
    Quality = 2,
    Safety = 3,
    Contractual = 4,
    Administrative = 5
}

public enum PenaltyStatus
{
    Draft = 0,
    Proposed = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Applied = 5,
    Closed = 6
}

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Cancelled = 4
}
