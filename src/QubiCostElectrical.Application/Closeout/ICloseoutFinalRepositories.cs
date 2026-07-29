using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProjectReceptionRepository
{
    Task SaveAsync(ProjectReception reception, CancellationToken ct = default);
    Task<ProjectReception?> GetByIdAsync(ProjectReceptionId id, CancellationToken ct = default);
    Task<List<ProjectReception>> ListAsync(string closeoutId, CancellationToken ct = default);
    Task SaveInspectionAsync(ReceptionInspection inspection, CancellationToken ct = default);
    Task<List<ReceptionInspection>> ListInspectionsAsync(ProjectReceptionId receptionId, CancellationToken ct = default);
    Task SaveObservationAsync(ReceptionObservation obs, CancellationToken ct = default);
    Task<List<ReceptionObservation>> ListObservationsAsync(ProjectReceptionId receptionId, CancellationToken ct = default);
}

public interface IReceptionRecordRepository
{
    Task SaveRecordAsync(ReceptionRecord record, CancellationToken ct = default);
    Task<ReceptionRecord?> GetRecordByIdAsync(ReceptionRecordId id, CancellationToken ct = default);
    Task<List<ReceptionRecord>> ListRecordsAsync(ProjectReceptionId receptionId, CancellationToken ct = default);
    Task SaveHandoverAsync(HandoverRecord record, CancellationToken ct = default);
    Task<List<HandoverRecord>> ListHandoversAsync(string closeoutId, CancellationToken ct = default);
    Task SaveCertificateAsync(AcceptanceCertificate cert, CancellationToken ct = default);
    Task<List<AcceptanceCertificate>> ListCertificatesAsync(string projectId, CancellationToken ct = default);
}

public interface IAsBuiltRepository
{
    Task SaveAsync(AsBuiltDeliverable item, CancellationToken ct = default);
    Task<AsBuiltDeliverable?> GetByIdAsync(AsBuiltDeliverableId id, CancellationToken ct = default);
    Task<List<AsBuiltDeliverable>> ListAsync(string closeoutId, CancellationToken ct = default);
    Task SaveTrainingAsync(TrainingRecord record, CancellationToken ct = default);
    Task<List<TrainingRecord>> ListTrainingAsync(string closeoutId, CancellationToken ct = default);
}

public interface IGuaranteeReleaseRepository
{
    Task SaveRequestAsync(GuaranteeReleaseRequest request, CancellationToken ct = default);
    Task<GuaranteeReleaseRequest?> GetRequestByIdAsync(GuaranteeReleaseId id, CancellationToken ct = default);
    Task<List<GuaranteeReleaseRequest>> ListRequestsAsync(string closeoutId, CancellationToken ct = default);
    Task SaveApprovalAsync(GuaranteeReleaseApproval approval, CancellationToken ct = default);
}

public interface ICloseoutRetentionReleaseRepository
{
    Task SaveAsync(RetentionReleaseRequest request, CancellationToken ct = default);
    Task<RetentionReleaseRequest?> GetByIdAsync(RetentionReleaseId id, CancellationToken ct = default);
    Task<List<RetentionReleaseRequest>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface IInventoryCloseoutRepository
{
    Task SaveReconciliationAsync(InventoryReconciliation rec, CancellationToken ct = default);
    Task<InventoryReconciliation?> GetByIdAsync(InventoryCloseoutId id, CancellationToken ct = default);
    Task<List<InventoryReconciliation>> ListAsync(string closeoutId, CancellationToken ct = default);
    Task SaveDisposalAsync(SurplusDisposal disposal, CancellationToken ct = default);
    Task<List<SurplusDisposal>> ListDisposalsAsync(InventoryCloseoutId inventoryId, CancellationToken ct = default);
}

public interface ILessonLearnedRepository
{
    Task SaveAsync(LessonLearned lesson, CancellationToken ct = default);
    Task<LessonLearned?> GetByIdAsync(LessonLearnedId id, CancellationToken ct = default);
    Task<List<LessonLearned>> ListAsync(string? closeoutId, CancellationToken ct = default);
    Task SaveEvidenceAsync(LessonEvidence evidence, CancellationToken ct = default);
    Task<List<LessonEvidence>> ListEvidenceAsync(LessonLearnedId lessonId, CancellationToken ct = default);
}

public interface IFinalCloseoutSnapshotRepository
{
    Task SaveAsync(FinalCloseoutSnapshot snapshot, CancellationToken ct = default);
    Task<FinalCloseoutSnapshot?> GetOfficialAsync(string closeoutId, CancellationToken ct = default);
    Task<List<FinalCloseoutSnapshot>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface IProjectArchiveManifestRepository
{
    Task SaveAsync(ProjectArchiveManifest manifest, CancellationToken ct = default);
    Task<ProjectArchiveManifest?> GetByProjectAsync(string projectId, CancellationToken ct = default);
    Task<List<ProjectArchiveManifest>> ListAsync(CancellationToken ct = default);
}
