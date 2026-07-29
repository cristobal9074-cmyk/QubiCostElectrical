namespace QubiCostElectrical;

public enum ReceptionType { Partial, Provisional, Final, ClientHandover, InternalHandover }
public enum ReceptionStatus { Draft, Scheduled, InInspection, Observed, PendingCorrection, PendingApproval, Approved, Rejected, Completed, Cancelled }
public enum AsBuiltStatus { Required, InPreparation, Submitted, UnderReview, Approved, Rejected, Superseded, Archived }
public enum SurplusDisposalType { TransferToProject, ReturnToSupplier, ReturnToWarehouse, ClientHandover, Sale, Scrap, Loss, Other }
public enum LessonStatus { Draft, Submitted, UnderReview, Approved, Rejected, Published, Archived }
public enum LessonCategory
{
    Estimation, Budget, Apu, Planning, Productivity, Purchasing, Supplier,
    Contracts, Changes, Claims, Quality, Safety, Engineering, Documentation,
    Client, Risk, Cost, Schedule, Communication, Technology, Other
}
public enum ProjectLifecycleStatus { Active, Closing, ProvisionallyClosed, Closed, Reopened, Archived }
public enum GuaranteeReleaseStatus { Requested, UnderReview, Approved, Rejected, Released, PartiallyReleased, Cancelled }
public enum RetentionReleaseStatus { Requested, UnderReview, Approved, Rejected, Released, PartiallyReleased, Cancelled }
public enum InventoryCloseoutStatus { Pending, InProgress, Reconciled, WithDifferences, Closed }
