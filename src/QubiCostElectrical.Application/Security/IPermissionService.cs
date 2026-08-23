namespace QubiCostElectrical;

public interface IPermissionService
{
    UserRole CurrentRole { get; }
    void SetRole(UserRole role);
    bool CanEditBudget { get; }
    bool CanExportDocuments { get; }
    bool CanEditGlobalPriceBank { get; }
    bool CanManageAppSettings { get; }
    string RoleDisplayName { get; }
    string RoleReadOnlyMessage { get; }

    // Contratos: permisos de ciclo de vida y controles de segregación de funciones (SoD).
    bool CanContractCreate { get; }
    bool CanContractEditDraft { get; }
    bool CanContractSubmit { get; }
    bool CanContractApprove { get; }
    bool CanContractReject { get; }
    bool CanContractSuspend { get; }
    bool CanContractClose { get; }
    bool CanContractTerminate { get; }
    bool CanContractDocumentManage { get; }
    bool CanContractIntegrityVerify { get; }
    bool CanContractAuditView { get; }
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

    public string RoleDisplayName => UserRolePermissions.GetDisplayName(currentRole);

    public string RoleReadOnlyMessage => UserRolePermissions.GetRoleReadOnlyMessage(currentRole);

    // Contratos: Estimator puede crear/editar/enviar a revisión; solo Admin aprueba,
    // rechaza, suspende, cierra o termina (segregación de funciones: quien redacta no aprueba).
    public bool CanContractCreate => currentRole == UserRole.Admin || currentRole == UserRole.Estimator;

    public bool CanContractEditDraft => currentRole == UserRole.Admin || currentRole == UserRole.Estimator;

    public bool CanContractSubmit => currentRole == UserRole.Admin || currentRole == UserRole.Estimator;

    public bool CanContractApprove => currentRole == UserRole.Admin;

    public bool CanContractReject => currentRole == UserRole.Admin;

    public bool CanContractSuspend => currentRole == UserRole.Admin;

    public bool CanContractClose => currentRole == UserRole.Admin;

    public bool CanContractTerminate => currentRole == UserRole.Admin;

    public bool CanContractDocumentManage => currentRole == UserRole.Admin || currentRole == UserRole.Estimator;

    public bool CanContractIntegrityVerify => currentRole == UserRole.Admin || currentRole == UserRole.Estimator;

    public bool CanContractAuditView => currentRole == UserRole.Admin || currentRole == UserRole.Estimator;
}
