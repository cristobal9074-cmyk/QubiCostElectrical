namespace QubiCostElectrical;

public interface IPermissionService
{
    UserRole CurrentRole { get; }
    void SetRole(UserRole role);
    bool CanEditBudget { get; }
    bool CanExportDocuments { get; }
    bool CanEditGlobalPriceBank { get; }
    bool CanManageAppSettings { get; }
    bool CanApproveVersions { get; }
    bool CanSendVersionToReview { get; }
    bool CanStartVersionReview { get; }
    bool CanRejectVersion { get; }
    bool CanViewAudit { get; }
    bool CanManageUsers { get; }
    bool CanManageRoles { get; }
    bool CanManagePolicies { get; }
    bool CanCompareVersions { get; }
    string RoleDisplayName { get; }
    string RoleReadOnlyMessage { get; }

    // Contract Permissions
    bool CanContractView { get; }
    bool CanContractCreate { get; }
    bool CanContractEditDraft { get; }
    bool CanContractCreateVersion { get; }
    bool CanContractSubmit { get; }
    bool CanContractApprove { get; }
    bool CanContractReject { get; }
    bool CanContractSuspend { get; }
    bool CanContractClose { get; }
    bool CanContractTerminate { get; }
    bool CanContractDocumentManage { get; }
    bool CanContractAuditView { get; }
    bool CanContractIntegrityVerify { get; }

    // Payment Certificate Permissions
    bool CanPaymentCertificateView { get; }
    bool CanPaymentCertificateCreate { get; }
    bool CanPaymentCertificateEditDraft { get; }
    bool CanPaymentCertificateSubmit { get; }
    bool CanPaymentCertificateReview { get; }
    bool CanPaymentCertificateObserve { get; }
    bool CanPaymentCertificateCorrect { get; }
    bool CanPaymentCertificateApprove { get; }
    bool CanPaymentCertificateReject { get; }
    bool CanPaymentCertificateInvoice { get; }
    bool CanPaymentCertificateMarkPaid { get; }
    bool CanPaymentCertificateClose { get; }
    bool CanPaymentCertificateVoid { get; }
    bool CanValuationImport { get; }
    bool CanAdvanceManage { get; }
    bool CanRetentionManage { get; }
    bool CanPaymentCertificateAuditView { get; }
    bool CanPaymentCertificateExport { get; }
}

public sealed class UserRolePermissionService : IPermissionService
{
    private UserRole currentRole = UserRole.Viewer;

    public UserRole CurrentRole => currentRole;

    public void SetRole(UserRole role) => currentRole = role;

    public bool CanEditBudget => UserRolePermissions.CanEditBudget(currentRole);
    public bool CanExportDocuments => UserRolePermissions.CanExportDocuments(currentRole);
    public bool CanEditGlobalPriceBank => UserRolePermissions.CanEditGlobalPriceBank(currentRole);
    public bool CanManageAppSettings => UserRolePermissions.CanManageAppSettings(currentRole);
    public bool CanApproveVersions => UserRolePermissions.CanApproveVersions(currentRole);
    public bool CanSendVersionToReview => UserRolePermissions.CanSendVersionToReview(currentRole);
    public bool CanStartVersionReview => UserRolePermissions.CanStartVersionReview(currentRole);
    public bool CanRejectVersion => UserRolePermissions.CanRejectVersion(currentRole);
    public bool CanViewAudit => UserRolePermissions.CanViewAudit(currentRole);
    public bool CanManageUsers => UserRolePermissions.CanManageUsers(currentRole);
    public bool CanManageRoles => UserRolePermissions.CanManageRoles(currentRole);
    public bool CanManagePolicies => UserRolePermissions.CanManagePolicies(currentRole);
    public bool CanCompareVersions => UserRolePermissions.CanCompareVersions(currentRole);
    public string RoleDisplayName => UserRolePermissions.GetDisplayName(currentRole);
    public string RoleReadOnlyMessage => UserRolePermissions.GetRoleReadOnlyMessage(currentRole);

    // Contract Permissions
    public bool CanContractView => ContractPermissions.CanView(currentRole);
    public bool CanContractCreate => ContractPermissions.CanCreate(currentRole);
    public bool CanContractEditDraft => ContractPermissions.CanEditDraft(currentRole);
    public bool CanContractCreateVersion => ContractPermissions.CanCreateVersion(currentRole);
    public bool CanContractSubmit => ContractPermissions.CanSubmit(currentRole);
    public bool CanContractApprove => ContractPermissions.CanApprove(currentRole);
    public bool CanContractReject => ContractPermissions.CanReject(currentRole);
    public bool CanContractSuspend => ContractPermissions.CanSuspend(currentRole);
    public bool CanContractClose => ContractPermissions.CanClose(currentRole);
    public bool CanContractTerminate => ContractPermissions.CanTerminate(currentRole);
    public bool CanContractDocumentManage => ContractPermissions.CanDocumentManage(currentRole);
    public bool CanContractAuditView => ContractPermissions.CanAuditView(currentRole);
    public bool CanContractIntegrityVerify => ContractPermissions.CanIntegrityVerify(currentRole);

    // Payment Certificate Permissions
    public bool CanPaymentCertificateView => ContractPermissions.CanPaymentCertificateView(currentRole);
    public bool CanPaymentCertificateCreate => ContractPermissions.CanPaymentCertificateCreate(currentRole);
    public bool CanPaymentCertificateEditDraft => ContractPermissions.CanPaymentCertificateEditDraft(currentRole);
    public bool CanPaymentCertificateSubmit => ContractPermissions.CanPaymentCertificateSubmit(currentRole);
    public bool CanPaymentCertificateReview => ContractPermissions.CanPaymentCertificateReview(currentRole);
    public bool CanPaymentCertificateObserve => ContractPermissions.CanPaymentCertificateObserve(currentRole);
    public bool CanPaymentCertificateCorrect => ContractPermissions.CanPaymentCertificateCorrect(currentRole);
    public bool CanPaymentCertificateApprove => ContractPermissions.CanPaymentCertificateApprove(currentRole);
    public bool CanPaymentCertificateReject => ContractPermissions.CanPaymentCertificateReject(currentRole);
    public bool CanPaymentCertificateInvoice => ContractPermissions.CanPaymentCertificateInvoice(currentRole);
    public bool CanPaymentCertificateMarkPaid => ContractPermissions.CanPaymentCertificateMarkPaid(currentRole);
    public bool CanPaymentCertificateClose => ContractPermissions.CanPaymentCertificateClose(currentRole);
    public bool CanPaymentCertificateVoid => ContractPermissions.CanPaymentCertificateVoid(currentRole);
    public bool CanValuationImport => ContractPermissions.CanValuationImport(currentRole);
    public bool CanAdvanceManage => ContractPermissions.CanAdvanceManage(currentRole);
    public bool CanRetentionManage => ContractPermissions.CanRetentionManage(currentRole);
    public bool CanPaymentCertificateAuditView => ContractPermissions.CanPaymentCertificateAuditView(currentRole);
    public bool CanPaymentCertificateExport => ContractPermissions.CanPaymentCertificateExport(currentRole);
}
