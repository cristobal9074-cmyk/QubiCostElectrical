using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProjectReceptionService
{
    Task<ProjectReception> CreateAsync(string closeoutId, ReceptionType type, string title, string actor, CancellationToken ct = default);
    Task<List<ProjectReception>> ListAsync(string closeoutId, CancellationToken ct = default);
    Task ApproveAsync(ProjectReceptionId id, string actor, CancellationToken ct = default);
    Task RejectAsync(ProjectReceptionId id, string reason, string actor, CancellationToken ct = default);
}

public interface IHandoverService
{
    Task<HandoverRecord> CreateHandoverAsync(string closeoutId, string actor, CancellationToken ct = default);
    Task AcceptCertificateAsync(AcceptanceCertificateId id, string actor, CancellationToken ct = default);
    Task<List<AsBuiltDeliverable>> ListAsBuiltAsync(string closeoutId, CancellationToken ct = default);
    Task<TrainingRecord> RecordTrainingAsync(string closeoutId, string topic, string actor, CancellationToken ct = default);
}

public interface ICloseoutReleaseService
{
    Task<GuaranteeReleaseRequest> RequestGuaranteeReleaseAsync(string closeoutId, string guaranteeRef, string actor, CancellationToken ct = default);
    Task ApproveGuaranteeReleaseAsync(GuaranteeReleaseId id, string actor, CancellationToken ct = default);
    Task<RetentionReleaseRequest> RequestRetentionReleaseAsync(string closeoutId, decimal amount, string actor, CancellationToken ct = default);
    Task ApproveRetentionReleaseAsync(RetentionReleaseId id, string actor, CancellationToken ct = default);
}

public interface IInventoryCloseoutService
{
    Task<InventoryReconciliation> ReconcileAsync(string closeoutId, string actor, CancellationToken ct = default);
    Task<SurplusDisposal> DisposeSurplusAsync(InventoryCloseoutId inventoryId, string resource, decimal qty, SurplusDisposalType type, string actor, CancellationToken ct = default);
}

public interface ILessonLearnedService
{
    Task<LessonLearned> CreateAsync(string closeoutId, string title, LessonCategory category, string actor, CancellationToken ct = default);
    Task ApproveAsync(LessonLearnedId id, string actor, CancellationToken ct = default);
    Task PublishAsync(LessonLearnedId id, string actor, CancellationToken ct = default);
    Task<List<LessonLearned>> ListAsync(string? closeoutId, CancellationToken ct = default);
}

public interface IFinalCloseoutService
{
    Task<ProjectFinalResult> CalculateFinalResultAsync(string projectId, string closeoutId, CancellationToken ct = default);
    Task<FinalCloseoutSnapshot> CreateFinalSnapshotAsync(string closeoutId, string actor, CancellationToken ct = default);
    Task<ProjectCloseout> CloseProjectAsync(string closeoutId, string actor, CancellationToken ct = default);
    Task<ProjectCloseout> ReopenProjectAsync(string closeoutId, string reason, string actor, CancellationToken ct = default);
}

public interface IProjectArchiveService
{
    Task<ProjectArchiveManifest> PrepareManifestAsync(string projectId, string actor, CancellationToken ct = default);
    Task<ProjectArchiveManifest> GetManifestAsync(string projectId, CancellationToken ct = default);
}
