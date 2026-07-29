using System.Collections.Generic;
using System.Threading;

namespace QubiCostElectrical;

partial class AppServices
{
    // Repositories
    public IProjectCloseoutRepository ProjectCloseoutRepository { get; internal set; } = null!;
    public ICloseoutChecklistRepository CloseoutChecklistRepository { get; internal set; } = null!;
    public IPunchListRepository PunchListRepository { get; internal set; } = null!;
    public ICloseoutNonConformanceRepository CloseoutNonConformanceRepository { get; internal set; } = null!;
    public ICloseoutDocumentRepository CloseoutDocumentRepository { get; internal set; } = null!;
    public ICloseoutEvidenceRepository CloseoutEvidenceRepository { get; internal set; } = null!;
    public ICloseoutSnapshotRepository CloseoutSnapshotRepository { get; internal set; } = null!;
    public ICloseoutAlertRepository CloseoutAlertRepository { get; internal set; } = null!;
    public ICloseoutAuditRepository CloseoutAuditRepository { get; internal set; } = null!;

    // Permission checker
    public ICloseoutPermissionChecker CloseoutPermissionChecker { get; internal set; } = null!;

    // Services
    public IProjectCloseoutService ProjectCloseoutService { get; internal set; } = null!;
    public ICloseoutChecklistService CloseoutChecklistService { get; internal set; } = null!;
    public IPunchListService PunchListService { get; internal set; } = null!;
    public ICloseoutDocumentService CloseoutDocumentService { get; internal set; } = null!;
    public ICloseoutReconciliationService CloseoutReconciliationService { get; internal set; } = null!;
    public ICloseoutReadinessService CloseoutReadinessService { get; internal set; } = null!;

    // ── 12CDE: Final closeout ──────────────────────────────────────────
    public IProjectReceptionRepository ProjectReceptionRepository { get; internal set; } = null!;
    public IReceptionRecordRepository ReceptionRecordRepository { get; internal set; } = null!;
    public IAsBuiltRepository AsBuiltRepository { get; internal set; } = null!;
    public IGuaranteeReleaseRepository GuaranteeReleaseRepository { get; internal set; } = null!;
    public ICloseoutRetentionReleaseRepository CloseoutRetentionReleaseRepository { get; internal set; } = null!;
    public IInventoryCloseoutRepository InventoryCloseoutRepository { get; internal set; } = null!;
    public ILessonLearnedRepository LessonLearnedRepository { get; internal set; } = null!;
    public IFinalCloseoutSnapshotRepository FinalCloseoutSnapshotRepository { get; internal set; } = null!;
    public IProjectArchiveManifestRepository ProjectArchiveManifestRepository { get; internal set; } = null!;
    public ICloseoutFinalPermissionChecker CloseoutFinalPermissionChecker { get; internal set; } = null!;
    public IProjectReceptionService ProjectReceptionService { get; internal set; } = null!;
    public IHandoverService HandoverService { get; internal set; } = null!;
    public ICloseoutReleaseService CloseoutReleaseService { get; internal set; } = null!;
    public IInventoryCloseoutService InventoryCloseoutService { get; internal set; } = null!;
    public ILessonLearnedService LessonLearnedService { get; internal set; } = null!;
    public IFinalCloseoutService FinalCloseoutService { get; internal set; } = null!;
    public IProjectArchiveService ProjectArchiveService { get; internal set; } = null!;

    public void SetCloseoutServices(
        IProjectCloseoutRepository projectCloseoutRepo,
        ICloseoutChecklistRepository checklistRepo,
        IPunchListRepository punchListRepo,
        ICloseoutNonConformanceRepository nonConformanceRepo,
        ICloseoutDocumentRepository documentRepo,
        ICloseoutEvidenceRepository evidenceRepo,
        ICloseoutSnapshotRepository snapshotRepo,
        ICloseoutAlertRepository alertRepo,
        ICloseoutAuditRepository auditRepo,
        ICloseoutPermissionChecker permissionChecker,
        IProjectCloseoutService projectCloseoutService,
        ICloseoutChecklistService checklistService,
        IPunchListService punchListService,
        ICloseoutDocumentService documentService,
        ICloseoutReconciliationService reconciliationService,
        ICloseoutReadinessService readinessService)
    {
        ProjectCloseoutRepository = projectCloseoutRepo;
        CloseoutChecklistRepository = checklistRepo;
        PunchListRepository = punchListRepo;
        CloseoutNonConformanceRepository = nonConformanceRepo;
        CloseoutDocumentRepository = documentRepo;
        CloseoutEvidenceRepository = evidenceRepo;
        CloseoutSnapshotRepository = snapshotRepo;
        CloseoutAlertRepository = alertRepo;
        CloseoutAuditRepository = auditRepo;
        CloseoutPermissionChecker = permissionChecker;
        ProjectCloseoutService = projectCloseoutService;
        CloseoutChecklistService = checklistService;
        PunchListService = punchListService;
        CloseoutDocumentService = documentService;
        CloseoutReconciliationService = reconciliationService;
        CloseoutReadinessService = readinessService;
    }

    public void SetCloseoutFinalServices(
        IProjectReceptionRepository receptionRepo,
        IReceptionRecordRepository recordRepo,
        IAsBuiltRepository asBuiltRepo,
        IGuaranteeReleaseRepository guaranteeRepo,
        ICloseoutRetentionReleaseRepository retentionRepo,
        IInventoryCloseoutRepository inventoryRepo,
        ILessonLearnedRepository lessonRepo,
        IFinalCloseoutSnapshotRepository snapshotRepo,
        IProjectArchiveManifestRepository archiveRepo,
        ICloseoutFinalPermissionChecker permissionChecker,
        IProjectReceptionService receptionService,
        IHandoverService handoverService,
        ICloseoutReleaseService releaseService,
        IInventoryCloseoutService inventoryCloseoutService,
        ILessonLearnedService lessonLearnedService,
        IFinalCloseoutService finalCloseoutService,
        IProjectArchiveService archiveService)
    {
        ProjectReceptionRepository = receptionRepo;
        ReceptionRecordRepository = recordRepo;
        AsBuiltRepository = asBuiltRepo;
        GuaranteeReleaseRepository = guaranteeRepo;
        CloseoutRetentionReleaseRepository = retentionRepo;
        InventoryCloseoutRepository = inventoryRepo;
        LessonLearnedRepository = lessonRepo;
        FinalCloseoutSnapshotRepository = snapshotRepo;
        ProjectArchiveManifestRepository = archiveRepo;
        CloseoutFinalPermissionChecker = permissionChecker;
        ProjectReceptionService = receptionService;
        HandoverService = handoverService;
        CloseoutReleaseService = releaseService;
        InventoryCloseoutService = inventoryCloseoutService;
        LessonLearnedService = lessonLearnedService;
        FinalCloseoutService = finalCloseoutService;
        ProjectArchiveService = archiveService;
    }
}
