using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProjectCloseoutService
{
    Task<ProjectCloseout> CreateAsync(string projectId, string code, string name, string actor, CancellationToken ct = default);
    Task<ProjectCloseout?> GetAsync(CloseoutId id, CancellationToken ct = default);
    Task<ProjectCloseout?> GetByProjectAsync(string projectId, CancellationToken ct = default);
    Task SaveAsync(ProjectCloseout closeout, string actor, CancellationToken ct = default);
    Task<List<ProjectCloseout>> ListAsync(string? projectId, CancellationToken ct = default);
}

public interface ICloseoutChecklistService
{
    Task<CloseoutChecklist> ApplyTemplateAsync(CloseoutId closeoutId, ChecklistTemplateId templateId, CancellationToken ct = default);
    Task<List<CloseoutChecklistTemplate>> ListTemplatesAsync(CancellationToken ct = default);
    Task<CloseoutChecklistTemplate> CreateTemplateAsync(string name, string? projectType, string actor, CancellationToken ct = default);
    Task UpdateItemStatusAsync(ChecklistItemId itemId, ChecklistItemStatus status, string actor, string? comment, CancellationToken ct = default);
    Task VerifyItemAsync(ChecklistItemId itemId, string actor, CancellationToken ct = default);
    Task<List<CloseoutChecklistItem>> GetItemsAsync(ChecklistId checklistId, CancellationToken ct = default);
}

public interface IPunchListService
{
    Task<PunchItem> CreateAsync(string closeoutId, string title, string responsible, string actor, CancellationToken ct = default);
    Task UpdateAsync(PunchItem item, string actor, CancellationToken ct = default);
    Task CloseAsync(PunchItemId id, string actor, CancellationToken ct = default);
    Task ReopenAsync(PunchItemId id, string reason, string actor, CancellationToken ct = default);
    Task<List<PunchItem>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutDocumentService
{
    Task<CloseoutDocumentRequirement> AddRequirementAsync(string closeoutId, string docType, string category, string actor, CancellationToken ct = default);
    Task<List<CloseoutDocumentRequirement>> ListRequirementsAsync(string closeoutId, CancellationToken ct = default);
    Task<List<CloseoutDocument>> ListDocumentsAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutReconciliationService
{
    Task<CloseoutReadinessResult> ReconcileAsync(string projectId, string closeoutId, string statusDate, string currency, CancellationToken ct = default);
}

public interface ICloseoutReadinessService
{
    Task<CloseoutReadinessResult> CalculateAsync(string closeoutId, CancellationToken ct = default);
    Task<CloseoutSnapshot> CreateSnapshotAsync(string closeoutId, string comment, string actor, CancellationToken ct = default);
    Task<List<CloseoutSnapshot>> ListSnapshotsAsync(string closeoutId, CancellationToken ct = default);
}
