using System;
using System.Collections.Generic;
using System.Threading;

namespace QubiCostElectrical;

partial class AppServicesBuilder
{
    // ── Closeout fields ─────────────────────────────────────────────────
    private IProjectCloseoutRepository? projectCloseoutRepository;
    private ICloseoutChecklistRepository? closeoutChecklistRepository;
    private IPunchListRepository? punchListRepository;
    private ICloseoutNonConformanceRepository? closeoutNonConformanceRepository;
    private ICloseoutDocumentRepository? closeoutDocumentRepository;
    private ICloseoutEvidenceRepository? closeoutEvidenceRepository;
    private ICloseoutSnapshotRepository? closeoutSnapshotRepository;
    private ICloseoutAlertRepository? closeoutAlertRepository;
    private ICloseoutAuditRepository? closeoutAuditRepository;
    private ICloseoutPermissionChecker? closeoutPermissionChecker;
    private IProjectCloseoutService? projectCloseoutService;
    private ICloseoutChecklistService? closeoutChecklistService;
    private IPunchListService? punchListService;
    private ICloseoutDocumentService? closeoutDocumentService;
    private ICloseoutReconciliationService? closeoutReconciliationService;
    private ICloseoutReadinessService? closeoutReadinessService;

    // ── 12CDE: Final closeout ──────────────────────────────────────────
    private IProjectReceptionRepository? projectReceptionRepository;
    private IReceptionRecordRepository? receptionRecordRepository;
    private IAsBuiltRepository? asBuiltRepository;
    private IGuaranteeReleaseRepository? guaranteeReleaseRepository;
    private ICloseoutRetentionReleaseRepository? closeoutRetentionReleaseRepository;
    private IInventoryCloseoutRepository? inventoryCloseoutRepository;
    private ILessonLearnedRepository? lessonLearnedRepository;
    private IFinalCloseoutSnapshotRepository? finalCloseoutSnapshotRepository;
    private IProjectArchiveManifestRepository? projectArchiveManifestRepository;
    private ICloseoutFinalPermissionChecker? closeoutFinalPermissionChecker;
    private IProjectReceptionService? projectReceptionService;
    private IHandoverService? handoverService;
    private ICloseoutReleaseService? closeoutReleaseService;
    private IInventoryCloseoutService? inventoryCloseoutService;
    private ILessonLearnedService? lessonLearnedService;
    private IFinalCloseoutService? finalCloseoutService;
    private IProjectArchiveService? projectArchiveService;

    private void WireCloseoutServices(AppServices svc)
    {
        if (projectCloseoutRepository != null) svc.ProjectCloseoutRepository = projectCloseoutRepository;
        if (closeoutChecklistRepository != null) svc.CloseoutChecklistRepository = closeoutChecklistRepository;
        if (punchListRepository != null) svc.PunchListRepository = punchListRepository;
        if (closeoutNonConformanceRepository != null) svc.CloseoutNonConformanceRepository = closeoutNonConformanceRepository;
        if (closeoutDocumentRepository != null) svc.CloseoutDocumentRepository = closeoutDocumentRepository;
        if (closeoutEvidenceRepository != null) svc.CloseoutEvidenceRepository = closeoutEvidenceRepository;
        if (closeoutSnapshotRepository != null) svc.CloseoutSnapshotRepository = closeoutSnapshotRepository;
        if (closeoutAlertRepository != null) svc.CloseoutAlertRepository = closeoutAlertRepository;
        if (closeoutAuditRepository != null) svc.CloseoutAuditRepository = closeoutAuditRepository;
        if (closeoutPermissionChecker != null) svc.CloseoutPermissionChecker = closeoutPermissionChecker;
        if (projectCloseoutService != null) svc.ProjectCloseoutService = projectCloseoutService;
        if (closeoutChecklistService != null) svc.CloseoutChecklistService = closeoutChecklistService;
        if (punchListService != null) svc.PunchListService = punchListService;
        if (closeoutDocumentService != null) svc.CloseoutDocumentService = closeoutDocumentService;
        if (closeoutReconciliationService != null) svc.CloseoutReconciliationService = closeoutReconciliationService;
        if (closeoutReadinessService != null) svc.CloseoutReadinessService = closeoutReadinessService;
    }

    // ── Fluent With* methods ──────────────────────────────────────────
    public AppServicesBuilder WithProjectCloseoutRepository(IProjectCloseoutRepository value)
    {
        projectCloseoutRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutChecklistRepository(ICloseoutChecklistRepository value)
    {
        closeoutChecklistRepository = value;
        return this;
    }

    public AppServicesBuilder WithPunchListRepository(IPunchListRepository value)
    {
        punchListRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutNonConformanceRepository(ICloseoutNonConformanceRepository value)
    {
        closeoutNonConformanceRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutDocumentRepository(ICloseoutDocumentRepository value)
    {
        closeoutDocumentRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutEvidenceRepository(ICloseoutEvidenceRepository value)
    {
        closeoutEvidenceRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutSnapshotRepository(ICloseoutSnapshotRepository value)
    {
        closeoutSnapshotRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutAlertRepository(ICloseoutAlertRepository value)
    {
        closeoutAlertRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutAuditRepository(ICloseoutAuditRepository value)
    {
        closeoutAuditRepository = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutPermissionChecker(ICloseoutPermissionChecker value)
    {
        closeoutPermissionChecker = value;
        return this;
    }

    public AppServicesBuilder WithProjectCloseoutService(IProjectCloseoutService value)
    {
        projectCloseoutService = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutChecklistService(ICloseoutChecklistService value)
    {
        closeoutChecklistService = value;
        return this;
    }

    public AppServicesBuilder WithPunchListService(IPunchListService value)
    {
        punchListService = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutDocumentService(ICloseoutDocumentService value)
    {
        closeoutDocumentService = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutReconciliationService(ICloseoutReconciliationService value)
    {
        closeoutReconciliationService = value;
        return this;
    }

    public AppServicesBuilder WithCloseoutReadinessService(ICloseoutReadinessService value)
    {
        closeoutReadinessService = value;
        return this;
    }

    private void WireCloseoutFinalServices(AppServices svc)
    {
        if (projectReceptionRepository != null) svc.ProjectReceptionRepository = projectReceptionRepository;
        if (receptionRecordRepository != null) svc.ReceptionRecordRepository = receptionRecordRepository;
        if (asBuiltRepository != null) svc.AsBuiltRepository = asBuiltRepository;
        if (guaranteeReleaseRepository != null) svc.GuaranteeReleaseRepository = guaranteeReleaseRepository;
        if (closeoutRetentionReleaseRepository != null) svc.CloseoutRetentionReleaseRepository = closeoutRetentionReleaseRepository;
        if (inventoryCloseoutRepository != null) svc.InventoryCloseoutRepository = inventoryCloseoutRepository;
        if (lessonLearnedRepository != null) svc.LessonLearnedRepository = lessonLearnedRepository;
        if (finalCloseoutSnapshotRepository != null) svc.FinalCloseoutSnapshotRepository = finalCloseoutSnapshotRepository;
        if (projectArchiveManifestRepository != null) svc.ProjectArchiveManifestRepository = projectArchiveManifestRepository;
        if (closeoutFinalPermissionChecker != null) svc.CloseoutFinalPermissionChecker = closeoutFinalPermissionChecker;
        if (projectReceptionService != null) svc.ProjectReceptionService = projectReceptionService;
        if (handoverService != null) svc.HandoverService = handoverService;
        if (closeoutReleaseService != null) svc.CloseoutReleaseService = closeoutReleaseService;
        if (inventoryCloseoutService != null) svc.InventoryCloseoutService = inventoryCloseoutService;
        if (lessonLearnedService != null) svc.LessonLearnedService = lessonLearnedService;
        if (finalCloseoutService != null) svc.FinalCloseoutService = finalCloseoutService;
        if (projectArchiveService != null) svc.ProjectArchiveService = projectArchiveService;
    }

    // ── Fluent With* methods (12CDE) ───────────────────────────────────
    public AppServicesBuilder WithProjectReceptionRepository(IProjectReceptionRepository value)
    {
        projectReceptionRepository = value;
        return this;
    }
    public AppServicesBuilder WithReceptionRecordRepository(IReceptionRecordRepository value)
    {
        receptionRecordRepository = value;
        return this;
    }
    public AppServicesBuilder WithAsBuiltRepository(IAsBuiltRepository value)
    {
        asBuiltRepository = value;
        return this;
    }
    public AppServicesBuilder WithGuaranteeReleaseRepository(IGuaranteeReleaseRepository value)
    {
        guaranteeReleaseRepository = value;
        return this;
    }
    public AppServicesBuilder WithCloseoutRetentionReleaseRepository(ICloseoutRetentionReleaseRepository value)
    {
        closeoutRetentionReleaseRepository = value;
        return this;
    }
    public AppServicesBuilder WithInventoryCloseoutRepository(IInventoryCloseoutRepository value)
    {
        inventoryCloseoutRepository = value;
        return this;
    }
    public AppServicesBuilder WithLessonLearnedRepository(ILessonLearnedRepository value)
    {
        lessonLearnedRepository = value;
        return this;
    }
    public AppServicesBuilder WithFinalCloseoutSnapshotRepository(IFinalCloseoutSnapshotRepository value)
    {
        finalCloseoutSnapshotRepository = value;
        return this;
    }
    public AppServicesBuilder WithProjectArchiveManifestRepository(IProjectArchiveManifestRepository value)
    {
        projectArchiveManifestRepository = value;
        return this;
    }
    public AppServicesBuilder WithCloseoutFinalPermissionChecker(ICloseoutFinalPermissionChecker value)
    {
        closeoutFinalPermissionChecker = value;
        return this;
    }
    public AppServicesBuilder WithProjectReceptionService(IProjectReceptionService value)
    {
        projectReceptionService = value;
        return this;
    }
    public AppServicesBuilder WithHandoverService(IHandoverService value)
    {
        handoverService = value;
        return this;
    }
    public AppServicesBuilder WithCloseoutReleaseService(ICloseoutReleaseService value)
    {
        closeoutReleaseService = value;
        return this;
    }
    public AppServicesBuilder WithInventoryCloseoutService(IInventoryCloseoutService value)
    {
        inventoryCloseoutService = value;
        return this;
    }
    public AppServicesBuilder WithLessonLearnedService(ILessonLearnedService value)
    {
        lessonLearnedService = value;
        return this;
    }
    public AppServicesBuilder WithFinalCloseoutService(IFinalCloseoutService value)
    {
        finalCloseoutService = value;
        return this;
    }
    public AppServicesBuilder WithProjectArchiveService(IProjectArchiveService value)
    {
        projectArchiveService = value;
        return this;
    }
}
