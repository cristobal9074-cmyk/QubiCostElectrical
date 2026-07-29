using System;

namespace QubiCostElectrical;

public sealed class AuthorizationGuard
{
    private readonly IPermissionService permissions;
    private readonly SoDPolicyConfig policies;

    public AuthorizationGuard(IPermissionService permissions, SoDPolicyConfig? policies = null)
    {
        this.permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        this.policies = policies ?? new SoDPolicyConfig();
    }

    public AuthorizationResult CheckPermission(string permissionName, Func<bool> hasPermission)
    {
        if (!hasPermission())
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.PermissionMissing,
                $"No tiene el permiso '{permissionName}'. Su rol {permissions.RoleDisplayName} no está autorizado.",
                permissionName);

        return AuthorizationResult.Allowed();
    }

    public AuthorizationResult CheckApprovalSod(BudgetVersionRow version, string userEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail))
            return AuthorizationResult.Allowed();

        if (policies.CreatorCannotApprove && !string.IsNullOrWhiteSpace(version.CreadoPor) &&
            EmailNormalizer.EqualsNormalized(version.CreadoPor, userEmail))
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.CreatorCannotApprove,
                "Segregación de funciones: el usuario que creó esta versión no puede aprobarla. Debe ser aprobada por un revisor diferente.",
                "VersionApprove",
                "CreatorCannotApprove");

        if (policies.SubmitterCannotApprove && !string.IsNullOrWhiteSpace(version.EnviadoPor) &&
            EmailNormalizer.EqualsNormalized(version.EnviadoPor, userEmail))
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.SubmitterCannotApprove,
                "Segregación de funciones: el usuario que envió esta versión a revisión no puede aprobarla. Debe ser aprobada por un revisor diferente.",
                "VersionApprove",
                "SubmitterCannotApprove");

        return AuthorizationResult.Allowed();
    }

    public AuthorizationResult CheckUserActive(UserAccount? user)
    {
        if (user == null)
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.UserDisabled,
                "El usuario no existe o ha sido deshabilitado.",
                "",
                "DisabledUserCannotAct");

        if (policies.DisabledUserCannotAct && !user.IsActive)
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.UserDisabled,
                "El usuario está deshabilitado. No puede ejecutar esta acción.",
                "",
                "DisabledUserCannotAct");

        return AuthorizationResult.Allowed();
    }

    public AuthorizationResult CheckReviewerCannotEdit(string userRole, string? reviewerEmail, string userEmail)
    {
        if (policies.ReviewerCannotEditEconomics && !string.IsNullOrWhiteSpace(reviewerEmail) &&
            EmailNormalizer.EqualsNormalized(reviewerEmail, userEmail))
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.ReviewerCannotEditEconomics,
                "El revisor no puede modificar el contenido económico durante una revisión activa.",
                "CanEditBudget",
                "ReviewerCannotEditEconomics");

        return AuthorizationResult.Allowed();
    }

    public AuthorizationResult CheckIntegrity(BudgetVersionRow version)
    {
        if (!policies.InvalidIntegrityBlocksApproval)
            return AuthorizationResult.Allowed();

        if (version.Estado != "Aprobada")
            return AuthorizationResult.Allowed();

        if (string.IsNullOrWhiteSpace(version.IntegrityHash))
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.IntegrityCheckFailed,
                "No se puede aprobar: la versión no tiene hash de integridad.",
                "VersionApprove",
                "InvalidIntegrityBlocksApproval");

        string currentHash = VersionIntegrityHasher.CalculateHash(version);
        if (!string.Equals(version.IntegrityHash, currentHash, StringComparison.OrdinalIgnoreCase))
            return AuthorizationResult.Denied(
                AuthorizationDenialCode.IntegrityCheckFailed,
                "No se puede aprobar: el contenido económico ha sido modificado desde la aprobación.",
                "VersionApprove",
                "InvalidIntegrityBlocksApproval");

        return AuthorizationResult.Allowed();
    }
}
