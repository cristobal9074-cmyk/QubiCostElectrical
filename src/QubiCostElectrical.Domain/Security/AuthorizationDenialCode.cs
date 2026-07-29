namespace QubiCostElectrical;

public enum AuthorizationDenialCode
{
    UserDisabled,
    PermissionMissing,
    SelfApprovalDenied,
    CreatorCannotApprove,
    SubmitterCannotApprove,
    LastEditorCannotApprove,
    ReviewerCannotEditEconomics,
    ReviewerRequired,
    InvalidWorkflowState,
    IntegrityCheckFailed,
    ResourceReadOnly
}
