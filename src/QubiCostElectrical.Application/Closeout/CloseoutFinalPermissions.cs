namespace QubiCostElectrical;

public static class CloseoutFinalUserRolePermissions
{
    public static bool CanReceptionView(UserRole role) => role is UserRole.Admin or UserRole.Estimator or UserRole.Reviewer or UserRole.Approver;
    public static bool CanReceptionCreate(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanReceptionInspect(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanReceptionApprove(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanReceptionReject(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanFinalReceptionApprove(UserRole role) => role is UserRole.Admin;
    public static bool CanHandoverManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanAsBuiltManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanTrainingRecordManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanGuaranteeReleaseRequest(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanGuaranteeReleaseApprove(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanRetentionReleaseRequest(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanRetentionReleaseApprove(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanInventoryCloseoutManage(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanLessonLearnedView(UserRole role) => role is not UserRole.Viewer;
    public static bool CanLessonLearnedCreate(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanLessonLearnedReview(UserRole role) => role is UserRole.Admin or UserRole.Reviewer;
    public static bool CanLessonLearnedApprove(UserRole role) => role is UserRole.Admin or UserRole.Approver;
    public static bool CanLessonLearnedPublish(UserRole role) => role is UserRole.Admin;
    public static bool CanFinalCloseoutView(UserRole role) => role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;
    public static bool CanFinalCloseoutApprove(UserRole role) => role is UserRole.Admin;
    public static bool CanFinalCloseoutReopen(UserRole role) => role is UserRole.Admin;
    public static bool CanFinalSnapshotCreate(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanFinalCloseoutExport(UserRole role) => role is UserRole.Admin or UserRole.Estimator;
    public static bool CanProjectArchivePrepare(UserRole role) => role is UserRole.Admin;
    public static bool CanCloseoutFinalAuditView(UserRole role) => role is UserRole.Admin or UserRole.Reviewer or UserRole.Approver;
}

public interface ICloseoutFinalPermissionChecker
{
    bool CanReceptionView { get; }
    bool CanReceptionCreate { get; }
    bool CanReceptionInspect { get; }
    bool CanReceptionApprove { get; }
    bool CanReceptionReject { get; }
    bool CanFinalReceptionApprove { get; }
    bool CanHandoverManage { get; }
    bool CanAsBuiltManage { get; }
    bool CanTrainingRecordManage { get; }
    bool CanGuaranteeReleaseRequest { get; }
    bool CanGuaranteeReleaseApprove { get; }
    bool CanRetentionReleaseRequest { get; }
    bool CanRetentionReleaseApprove { get; }
    bool CanInventoryCloseoutManage { get; }
    bool CanLessonLearnedView { get; }
    bool CanLessonLearnedCreate { get; }
    bool CanLessonLearnedReview { get; }
    bool CanLessonLearnedApprove { get; }
    bool CanLessonLearnedPublish { get; }
    bool CanFinalCloseoutView { get; }
    bool CanFinalCloseoutApprove { get; }
    bool CanFinalCloseoutReopen { get; }
    bool CanFinalSnapshotCreate { get; }
    bool CanFinalCloseoutExport { get; }
    bool CanProjectArchivePrepare { get; }
    bool CanCloseoutFinalAuditView { get; }
}

public sealed class CloseoutFinalPermissionChecker : ICloseoutFinalPermissionChecker
{
    private readonly UserRole _role;
    public CloseoutFinalPermissionChecker(UserRole role) => _role = role;
    public bool CanReceptionView => CloseoutFinalUserRolePermissions.CanReceptionView(_role);
    public bool CanReceptionCreate => CloseoutFinalUserRolePermissions.CanReceptionCreate(_role);
    public bool CanReceptionInspect => CloseoutFinalUserRolePermissions.CanReceptionInspect(_role);
    public bool CanReceptionApprove => CloseoutFinalUserRolePermissions.CanReceptionApprove(_role);
    public bool CanReceptionReject => CloseoutFinalUserRolePermissions.CanReceptionReject(_role);
    public bool CanFinalReceptionApprove => CloseoutFinalUserRolePermissions.CanFinalReceptionApprove(_role);
    public bool CanHandoverManage => CloseoutFinalUserRolePermissions.CanHandoverManage(_role);
    public bool CanAsBuiltManage => CloseoutFinalUserRolePermissions.CanAsBuiltManage(_role);
    public bool CanTrainingRecordManage => CloseoutFinalUserRolePermissions.CanTrainingRecordManage(_role);
    public bool CanGuaranteeReleaseRequest => CloseoutFinalUserRolePermissions.CanGuaranteeReleaseRequest(_role);
    public bool CanGuaranteeReleaseApprove => CloseoutFinalUserRolePermissions.CanGuaranteeReleaseApprove(_role);
    public bool CanRetentionReleaseRequest => CloseoutFinalUserRolePermissions.CanRetentionReleaseRequest(_role);
    public bool CanRetentionReleaseApprove => CloseoutFinalUserRolePermissions.CanRetentionReleaseApprove(_role);
    public bool CanInventoryCloseoutManage => CloseoutFinalUserRolePermissions.CanInventoryCloseoutManage(_role);
    public bool CanLessonLearnedView => CloseoutFinalUserRolePermissions.CanLessonLearnedView(_role);
    public bool CanLessonLearnedCreate => CloseoutFinalUserRolePermissions.CanLessonLearnedCreate(_role);
    public bool CanLessonLearnedReview => CloseoutFinalUserRolePermissions.CanLessonLearnedReview(_role);
    public bool CanLessonLearnedApprove => CloseoutFinalUserRolePermissions.CanLessonLearnedApprove(_role);
    public bool CanLessonLearnedPublish => CloseoutFinalUserRolePermissions.CanLessonLearnedPublish(_role);
    public bool CanFinalCloseoutView => CloseoutFinalUserRolePermissions.CanFinalCloseoutView(_role);
    public bool CanFinalCloseoutApprove => CloseoutFinalUserRolePermissions.CanFinalCloseoutApprove(_role);
    public bool CanFinalCloseoutReopen => CloseoutFinalUserRolePermissions.CanFinalCloseoutReopen(_role);
    public bool CanFinalSnapshotCreate => CloseoutFinalUserRolePermissions.CanFinalSnapshotCreate(_role);
    public bool CanFinalCloseoutExport => CloseoutFinalUserRolePermissions.CanFinalCloseoutExport(_role);
    public bool CanProjectArchivePrepare => CloseoutFinalUserRolePermissions.CanProjectArchivePrepare(_role);
    public bool CanCloseoutFinalAuditView => CloseoutFinalUserRolePermissions.CanCloseoutFinalAuditView(_role);
}
