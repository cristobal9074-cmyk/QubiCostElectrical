namespace QubiCostElectrical;

public static class ContractAdministrationPermissions
{
    public static bool CanViewAdjustment(UserRole role) => true;
    public static bool CanCreateAdjustment(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEditAdjustment(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanCalculateAdjustment(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanApplyAdjustment(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanViewGuarantee(UserRole role) => true;
    public static bool CanCreateGuarantee(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEditGuarantee(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanApproveGuarantee(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanViewPenalty(UserRole role) => true;
    public static bool CanCreatePenalty(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEditPenalty(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanApprovePenalty(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanViewInvoice(UserRole role) => true;
    public static bool CanCreateInvoice(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanEditInvoice(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanExportContractAdministration(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
}

public sealed class ContractAdministrationPermissionChecker : IContractAdministrationPermissionChecker
{
    public bool CanViewAdjustment(UserRole role) => ContractAdministrationPermissions.CanViewAdjustment(role);
    public bool CanCreateAdjustment(UserRole role) => ContractAdministrationPermissions.CanCreateAdjustment(role);
    public bool CanEditAdjustment(UserRole role) => ContractAdministrationPermissions.CanEditAdjustment(role);
    public bool CanCalculateAdjustment(UserRole role) => ContractAdministrationPermissions.CanCalculateAdjustment(role);
    public bool CanApplyAdjustment(UserRole role) => ContractAdministrationPermissions.CanApplyAdjustment(role);
    public bool CanViewGuarantee(UserRole role) => ContractAdministrationPermissions.CanViewGuarantee(role);
    public bool CanCreateGuarantee(UserRole role) => ContractAdministrationPermissions.CanCreateGuarantee(role);
    public bool CanEditGuarantee(UserRole role) => ContractAdministrationPermissions.CanEditGuarantee(role);
    public bool CanApproveGuarantee(UserRole role) => ContractAdministrationPermissions.CanApproveGuarantee(role);
    public bool CanViewPenalty(UserRole role) => ContractAdministrationPermissions.CanViewPenalty(role);
    public bool CanCreatePenalty(UserRole role) => ContractAdministrationPermissions.CanCreatePenalty(role);
    public bool CanEditPenalty(UserRole role) => ContractAdministrationPermissions.CanEditPenalty(role);
    public bool CanApprovePenalty(UserRole role) => ContractAdministrationPermissions.CanApprovePenalty(role);
    public bool CanViewInvoice(UserRole role) => ContractAdministrationPermissions.CanViewInvoice(role);
    public bool CanCreateInvoice(UserRole role) => ContractAdministrationPermissions.CanCreateInvoice(role);
    public bool CanEditInvoice(UserRole role) => ContractAdministrationPermissions.CanEditInvoice(role);
    public bool CanExportContractAdministration(UserRole role) => ContractAdministrationPermissions.CanExportContractAdministration(role);
}
