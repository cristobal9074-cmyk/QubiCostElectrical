using Xunit;

namespace QubiCostElectrical.Tests;

public class AuthorizationGuardTests
{
    // ── CheckUserActive ────────────────────────────────────────────────────────

    [Fact]
    public void CheckUserActive_NullUser_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());

        var result = guard.CheckUserActive(null);

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.UserDisabled, result.DenialCode);
    }

    [Fact]
    public void CheckUserActive_InactiveUser_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "test@empresa.cl", IsActive = false };

        var result = guard.CheckUserActive(user);

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.UserDisabled, result.DenialCode);
    }

    [Fact]
    public void CheckUserActive_ActiveUser_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "test@empresa.cl", IsActive = true };

        var result = guard.CheckUserActive(user);

        Assert.True(result.IsAllowed);
    }

    // ── CheckPermission ────────────────────────────────────────────────────────

    [Fact]
    public void CheckPermission_HasPermission_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());

        var result = guard.CheckPermission("CanEditBudget", () => true);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckPermission_MissingPermission_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());

        var result = guard.CheckPermission("CanManageUsers", () => false);

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.PermissionMissing, result.DenialCode);
    }

    // ── CheckApprovalSod ──────────────────────────────────────────────────────

    [Fact]
    public void CheckApprovalSod_CreatorCannotApprove_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            CreadoPor = "creator@empresa.cl",
            EnviadoPor = "submitter@empresa.cl"
        };

        var result = guard.CheckApprovalSod(version, "creator@empresa.cl");

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.CreatorCannotApprove, result.DenialCode);
    }

    [Fact]
    public void CheckApprovalSod_SubmitterCannotApprove_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            CreadoPor = "creator@empresa.cl",
            EnviadoPor = "submitter@empresa.cl"
        };

        var result = guard.CheckApprovalSod(version, "submitter@empresa.cl");

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.SubmitterCannotApprove, result.DenialCode);
    }

    [Fact]
    public void CheckApprovalSod_DifferentUser_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            CreadoPor = "creator@empresa.cl",
            EnviadoPor = "submitter@empresa.cl"
        };

        var result = guard.CheckApprovalSod(version, "approver@empresa.cl");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckApprovalSod_EmptyUserEmail_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow { Id = "v1" };

        var result = guard.CheckApprovalSod(version, "");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckApprovalSod_EmailCaseInsensitive_BlocksCorrectly()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            CreadoPor = "Creator@Empresa.Cl",
            EnviadoPor = ""
        };

        var result = guard.CheckApprovalSod(version, "creator@empresa.cl");

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.CreatorCannotApprove, result.DenialCode);
    }

    [Fact]
    public void CheckApprovalSod_EmailWithWhitespace_NormalizesCorrectly()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            CreadoPor = "  Creator@Empresa.Cl  ",
            EnviadoPor = ""
        };

        var result = guard.CheckApprovalSod(version, "creator@empresa.cl");

        Assert.False(result.IsAllowed);
    }

    // ── CheckIntegrity ─────────────────────────────────────────────────────────

    [Fact]
    public void CheckIntegrity_ApprovedVersionWithValidHash_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            Estado = "Aprobada",
            IntegrityHash = VersionIntegrityHasher.CalculateHash(
                new BudgetVersionRow { Id = "v1", Estado = "Aprobada", MontoVentaUf = 100m })
        };
        version.MontoVentaUf = 100m;
        version.IntegrityHash = VersionIntegrityHasher.CalculateHash(version);

        var result = guard.CheckIntegrity(version);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckIntegrity_NonApprovedState_SkipsCheck()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            Estado = "En revisión",
            IntegrityHash = ""
        };

        var result = guard.CheckIntegrity(version);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckIntegrity_TamperedContent_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            Estado = "Aprobada",
            MontoVentaUf = 100m
        };
        version.IntegrityHash = VersionIntegrityHasher.CalculateHash(version);
        version.MontoVentaUf = 999m; // tamper

        var result = guard.CheckIntegrity(version);

        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.IntegrityCheckFailed, result.DenialCode);
    }

    [Fact]
    public void CheckIntegrity_NullHashOnApproved_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var version = new BudgetVersionRow
        {
            Id = "v1",
            Estado = "Aprobada",
            IntegrityHash = ""
        };

        var result = guard.CheckIntegrity(version);

        Assert.False(result.IsAllowed);
    }

    // ── CheckReviewerCannotEdit ────────────────────────────────────────────────

    [Fact]
    public void CheckReviewerCannotEdit_ReviewerEmailMatchesUser_ReturnsDenied()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var result = guard.CheckReviewerCannotEdit("Reviewer", "reviewer@empresa.cl", "reviewer@empresa.cl");
        Assert.False(result.IsAllowed);
        Assert.Equal(AuthorizationDenialCode.ReviewerCannotEditEconomics, result.DenialCode);
    }

    [Fact]
    public void CheckReviewerCannotEdit_DifferentUser_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var result = guard.CheckReviewerCannotEdit("Reviewer", "reviewer@empresa.cl", "estimator@empresa.cl");
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckReviewerCannotEdit_EmptyReviewerEmail_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var result = guard.CheckReviewerCannotEdit("Reviewer", null, "reviewer@empresa.cl");
        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckReviewerCannotEdit_NonMatchingEmail_ReturnsAllowed()
    {
        var guard = new AuthorizationGuard(new StubPermissionService());
        var result = guard.CheckReviewerCannotEdit("Reviewer", "reviewer@empresa.cl", "estimator@empresa.cl");
        Assert.True(result.IsAllowed);
    }
}

public class StubPermissionService : IPermissionService
{
    public UserRole CurrentRole => UserRole.Estimator;
    public void SetRole(UserRole role) {}
    public bool CanEditBudget => true;
    public bool CanExportDocuments => true;
    public bool CanEditGlobalPriceBank => true;
    public bool CanManageAppSettings => false;
    public bool CanApproveVersions => false;
    public bool CanSendVersionToReview => true;
    public bool CanStartVersionReview => true;
    public bool CanRejectVersion => true;
    public bool CanViewAudit => true;
    public bool CanManageUsers => false;
    public bool CanManageRoles => false;
    public bool CanManagePolicies => false;
    public bool CanCompareVersions => true;
    public string RoleDisplayName => "Estimador";
    public string RoleReadOnlyMessage => "";

    public bool CanContractView => true;
    public bool CanContractCreate => true;
    public bool CanContractEditDraft => true;
    public bool CanContractCreateVersion => true;
    public bool CanContractSubmit => true;
    public bool CanContractApprove => true;
    public bool CanContractReject => true;
    public bool CanContractSuspend => true;
    public bool CanContractClose => true;
    public bool CanContractTerminate => true;
    public bool CanContractDocumentManage => true;
    public bool CanContractAuditView => true;
    public bool CanContractIntegrityVerify => true;

    // Payment Certificate Permissions
    public bool CanPaymentCertificateView => true;
    public bool CanPaymentCertificateCreate => true;
    public bool CanPaymentCertificateEditDraft => true;
    public bool CanPaymentCertificateSubmit => true;
    public bool CanPaymentCertificateReview => true;
    public bool CanPaymentCertificateObserve => true;
    public bool CanPaymentCertificateCorrect => true;
    public bool CanPaymentCertificateApprove => true;
    public bool CanPaymentCertificateReject => true;
    public bool CanPaymentCertificateInvoice => true;
    public bool CanPaymentCertificateMarkPaid => true;
    public bool CanPaymentCertificateClose => true;
    public bool CanPaymentCertificateVoid => true;
    public bool CanValuationImport => true;
    public bool CanAdvanceManage => true;
    public bool CanRetentionManage => true;
    public bool CanPaymentCertificateAuditView => true;
    public bool CanPaymentCertificateExport => true;
}
