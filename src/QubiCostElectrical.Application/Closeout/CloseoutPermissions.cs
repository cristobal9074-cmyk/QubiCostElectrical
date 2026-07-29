namespace QubiCostElectrical;

public static class CloseoutUserRolePermissions
{
    public static bool CanCloseoutView(UserRole role) => role is UserRole.Admin or UserRole.Estimator or UserRole.Reviewer or UserRole.Approver;
    public static bool CanCloseoutCreate(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanCloseoutEdit(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanCloseoutSubmit(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanCloseoutReview(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanCloseoutApproveReadiness(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanChecklistManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanChecklistVerify(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanPunchListManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanPunchListVerify(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanNcManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanDocumentManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanDocumentApprove(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanReconciliationView(UserRole role) => role is UserRole.Admin or UserRole.Estimator or UserRole.Reviewer or UserRole.Approver;
    public static bool CanReconciliationRun(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanExceptionRequest(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanExceptionApprove(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanSnapshotCreate(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanSnapshotCompare(UserRole role) => role is UserRole.Admin or UserRole.Estimator or UserRole.Reviewer;
    public static bool CanAlertManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanAuditView(UserRole role) => role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;
    public static bool CanCloseoutExport(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
}

public interface ICloseoutPermissionChecker
{
    bool CanCloseoutView { get; }
    bool CanCloseoutCreate { get; }
    bool CanCloseoutEdit { get; }
    bool CanCloseoutSubmit { get; }
    bool CanCloseoutReview { get; }
    bool CanCloseoutApproveReadiness { get; }
    bool CanChecklistManage { get; }
    bool CanChecklistVerify { get; }
    bool CanPunchListManage { get; }
    bool CanPunchListVerify { get; }
    bool CanNcManage { get; }
    bool CanDocumentManage { get; }
    bool CanDocumentApprove { get; }
    bool CanReconciliationView { get; }
    bool CanReconciliationRun { get; }
    bool CanExceptionRequest { get; }
    bool CanExceptionApprove { get; }
    bool CanSnapshotCreate { get; }
    bool CanSnapshotCompare { get; }
    bool CanAlertManage { get; }
    bool CanAuditView { get; }
    bool CanCloseoutExport { get; }
}

public sealed class CloseoutPermissionChecker : ICloseoutPermissionChecker
{
    private readonly UserRole _role;
    public CloseoutPermissionChecker(UserRole role) => _role = role;
    public bool CanCloseoutView => CloseoutUserRolePermissions.CanCloseoutView(_role);
    public bool CanCloseoutCreate => CloseoutUserRolePermissions.CanCloseoutCreate(_role);
    public bool CanCloseoutEdit => CloseoutUserRolePermissions.CanCloseoutEdit(_role);
    public bool CanCloseoutSubmit => CloseoutUserRolePermissions.CanCloseoutSubmit(_role);
    public bool CanCloseoutReview => CloseoutUserRolePermissions.CanCloseoutReview(_role);
    public bool CanCloseoutApproveReadiness => CloseoutUserRolePermissions.CanCloseoutApproveReadiness(_role);
    public bool CanChecklistManage => CloseoutUserRolePermissions.CanChecklistManage(_role);
    public bool CanChecklistVerify => CloseoutUserRolePermissions.CanChecklistVerify(_role);
    public bool CanPunchListManage => CloseoutUserRolePermissions.CanPunchListManage(_role);
    public bool CanPunchListVerify => CloseoutUserRolePermissions.CanPunchListVerify(_role);
    public bool CanNcManage => CloseoutUserRolePermissions.CanNcManage(_role);
    public bool CanDocumentManage => CloseoutUserRolePermissions.CanDocumentManage(_role);
    public bool CanDocumentApprove => CloseoutUserRolePermissions.CanDocumentApprove(_role);
    public bool CanReconciliationView => CloseoutUserRolePermissions.CanReconciliationView(_role);
    public bool CanReconciliationRun => CloseoutUserRolePermissions.CanReconciliationRun(_role);
    public bool CanExceptionRequest => CloseoutUserRolePermissions.CanExceptionRequest(_role);
    public bool CanExceptionApprove => CloseoutUserRolePermissions.CanExceptionApprove(_role);
    public bool CanSnapshotCreate => CloseoutUserRolePermissions.CanSnapshotCreate(_role);
    public bool CanSnapshotCompare => CloseoutUserRolePermissions.CanSnapshotCompare(_role);
    public bool CanAlertManage => CloseoutUserRolePermissions.CanAlertManage(_role);
    public bool CanAuditView => CloseoutUserRolePermissions.CanAuditView(_role);
    public bool CanCloseoutExport => CloseoutUserRolePermissions.CanCloseoutExport(_role);
}
