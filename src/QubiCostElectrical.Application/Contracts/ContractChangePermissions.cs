namespace QubiCostElectrical;

public static class ContractChangePermissions
{
    public static bool CanViewChangeRequest(UserRole role) => true;
    public static bool CanCreateChangeRequest(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEditChangeRequest(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanSubmitChangeRequest(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanAnalyzeChangeRequest(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanCreateChangeOrder(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEditChangeOrderDraft(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanNegotiateChangeOrder(UserRole role) => role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;
    public static bool CanApproveChangeOrder(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanRejectChangeOrder(UserRole role) => role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;
    public static bool CanImplementChangeOrder(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanManageExtraWork(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanViewClaim(UserRole role) => true;
    public static bool CanCreateClaim(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanSubmitClaim(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEvaluateClaim(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanRespondClaim(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanApproveClaim(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanViewContractChangeAudit(UserRole role) => role is UserRole.Admin;
    public static bool CanExportContractChange(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
}

public sealed class ContractChangePermissionChecker : IContractChangePermissionChecker
{
    public bool CanViewChangeRequest(UserRole role) => ContractChangePermissions.CanViewChangeRequest(role);
    public bool CanCreateChangeRequest(UserRole role) => ContractChangePermissions.CanCreateChangeRequest(role);
    public bool CanEditChangeRequest(UserRole role) => ContractChangePermissions.CanEditChangeRequest(role);
    public bool CanSubmitChangeRequest(UserRole role) => ContractChangePermissions.CanSubmitChangeRequest(role);
    public bool CanAnalyzeChangeRequest(UserRole role) => ContractChangePermissions.CanAnalyzeChangeRequest(role);
    public bool CanCreateChangeOrder(UserRole role) => ContractChangePermissions.CanCreateChangeOrder(role);
    public bool CanEditChangeOrderDraft(UserRole role) => ContractChangePermissions.CanEditChangeOrderDraft(role);
    public bool CanNegotiateChangeOrder(UserRole role) => ContractChangePermissions.CanNegotiateChangeOrder(role);
    public bool CanApproveChangeOrder(UserRole role) => ContractChangePermissions.CanApproveChangeOrder(role);
    public bool CanRejectChangeOrder(UserRole role) => ContractChangePermissions.CanRejectChangeOrder(role);
    public bool CanImplementChangeOrder(UserRole role) => ContractChangePermissions.CanImplementChangeOrder(role);
    public bool CanManageExtraWork(UserRole role) => ContractChangePermissions.CanManageExtraWork(role);
    public bool CanViewClaim(UserRole role) => ContractChangePermissions.CanViewClaim(role);
    public bool CanCreateClaim(UserRole role) => ContractChangePermissions.CanCreateClaim(role);
    public bool CanSubmitClaim(UserRole role) => ContractChangePermissions.CanSubmitClaim(role);
    public bool CanEvaluateClaim(UserRole role) => ContractChangePermissions.CanEvaluateClaim(role);
    public bool CanRespondClaim(UserRole role) => ContractChangePermissions.CanRespondClaim(role);
    public bool CanApproveClaim(UserRole role) => ContractChangePermissions.CanApproveClaim(role);
    public bool CanViewContractChangeAudit(UserRole role) => ContractChangePermissions.CanViewContractChangeAudit(role);
    public bool CanExportContractChange(UserRole role) => ContractChangePermissions.CanExportContractChange(role);
}
