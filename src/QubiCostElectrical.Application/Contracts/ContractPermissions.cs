namespace QubiCostElectrical;

public static class ContractPermissions
{
    public static bool CanView(UserRole role) => true; // Todos pueden ver

    public static bool CanCreate(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanEditDraft(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanCreateVersion(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanSubmit(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanApprove(UserRole role) =>
        role is UserRole.Approver or UserRole.Admin;

    public static bool CanReject(UserRole role) =>
        role is UserRole.Reviewer or UserRole.Approver or UserRole.Admin;

    public static bool CanSuspend(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanClose(UserRole role) =>
        role is UserRole.Admin or UserRole.Approver;

    public static bool CanTerminate(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanDocumentManage(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanAuditView(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanIntegrityVerify(UserRole role) =>
        role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;

    // Payment Certificate Permissions
    public static bool CanPaymentCertificateView(UserRole role) => true;

    public static bool CanPaymentCertificateCreate(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanPaymentCertificateEditDraft(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanPaymentCertificateSubmit(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanPaymentCertificateReview(UserRole role) =>
        role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;

    public static bool CanPaymentCertificateObserve(UserRole role) =>
        role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;

    public static bool CanPaymentCertificateCorrect(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanPaymentCertificateApprove(UserRole role) =>
        role is UserRole.Admin or UserRole.Approver;

    public static bool CanPaymentCertificateReject(UserRole role) =>
        role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;

    public static bool CanPaymentCertificateInvoice(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanPaymentCertificateMarkPaid(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator or UserRole.Approver;

    public static bool CanPaymentCertificateClose(UserRole role) =>
        role is UserRole.Admin or UserRole.Approver;

    public static bool CanPaymentCertificateVoid(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanValuationImport(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanAdvanceManage(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanRetentionManage(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator;

    public static bool CanPaymentCertificateAuditView(UserRole role) =>
        role is UserRole.Admin;

    public static bool CanPaymentCertificateExport(UserRole role) =>
        role is UserRole.Admin or UserRole.Estimator or UserRole.Reviewer or UserRole.Approver;
}
