#nullable enable
namespace QubiCostElectrical;

public enum ChecklistItemStatus
{
    Pending,
    InProgress,
    SubmittedForVerification,
    Verified,
    Rejected,
    NotApplicable,
    Blocked,
    Overdue
}
