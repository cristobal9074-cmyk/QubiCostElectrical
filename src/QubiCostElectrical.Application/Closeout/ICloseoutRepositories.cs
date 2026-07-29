using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IProjectCloseoutRepository
{
    Task SaveAsync(ProjectCloseout closeout, CancellationToken ct = default);
    Task<ProjectCloseout?> GetByIdAsync(CloseoutId id, CancellationToken ct = default);
    Task<ProjectCloseout?> GetByProjectAsync(string projectId, CancellationToken ct = default);
    Task<List<ProjectCloseout>> ListAsync(string? projectId, CancellationToken ct = default);
}

public interface ICloseoutChecklistRepository
{
    Task SaveTemplateAsync(CloseoutChecklistTemplate template, CancellationToken ct = default);
    Task<CloseoutChecklistTemplate?> GetTemplateByIdAsync(ChecklistTemplateId id, CancellationToken ct = default);
    Task<List<CloseoutChecklistTemplate>> ListTemplatesAsync(CancellationToken ct = default);
    Task SaveChecklistAsync(CloseoutChecklist checklist, CancellationToken ct = default);
    Task<CloseoutChecklist?> GetChecklistByIdAsync(ChecklistId id, CancellationToken ct = default);
    Task<List<CloseoutChecklist>> ListChecklistsAsync(CloseoutId closeoutId, CancellationToken ct = default);
    Task SaveItemAsync(CloseoutChecklistItem item, CancellationToken ct = default);
    Task<CloseoutChecklistItem?> GetItemByIdAsync(ChecklistItemId id, CancellationToken ct = default);
    Task<List<CloseoutChecklistItem>> ListItemsAsync(ChecklistId checklistId, CancellationToken ct = default);
}

public interface IPunchListRepository
{
    Task SaveAsync(PunchItem item, CancellationToken ct = default);
    Task<PunchItem?> GetByIdAsync(PunchItemId id, CancellationToken ct = default);
    Task<List<PunchItem>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutNonConformanceRepository
{
    Task SaveAsync(CloseoutNonConformance nc, CancellationToken ct = default);
    Task<CloseoutNonConformance?> GetByIdAsync(NonConformanceId id, CancellationToken ct = default);
    Task<List<CloseoutNonConformance>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutDocumentRepository
{
    Task SaveRequirementAsync(CloseoutDocumentRequirement req, CancellationToken ct = default);
    Task<CloseoutDocumentRequirement?> GetRequirementByIdAsync(DocumentRequirementId id, CancellationToken ct = default);
    Task<List<CloseoutDocumentRequirement>> ListRequirementsAsync(string closeoutId, CancellationToken ct = default);
    Task SaveDocumentAsync(CloseoutDocument doc, CancellationToken ct = default);
    Task<CloseoutDocument?> GetDocumentByIdAsync(CloseoutDocumentId id, CancellationToken ct = default);
    Task<List<CloseoutDocument>> ListDocumentsAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutEvidenceRepository
{
    Task SaveAsync(CloseoutEvidence evidence, CancellationToken ct = default);
    Task<CloseoutEvidence?> GetByIdAsync(EvidenceId id, CancellationToken ct = default);
    Task<List<CloseoutEvidence>> ListAsync(string entityType, string entityId, CancellationToken ct = default);
}

public interface ICloseoutSnapshotRepository
{
    Task SaveAsync(CloseoutSnapshot snapshot, CancellationToken ct = default);
    Task<CloseoutSnapshot?> GetByIdAsync(CloseoutSnapshotId id, CancellationToken ct = default);
    Task<List<CloseoutSnapshot>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutAlertRepository
{
    Task SaveAsync(CloseoutAlert alert, CancellationToken ct = default);
    Task<List<CloseoutAlert>> ListAsync(string closeoutId, CancellationToken ct = default);
}

public interface ICloseoutAuditRepository
{
    Task SaveAsync(CloseoutAuditEvent evt, CancellationToken ct = default);
    Task<List<CloseoutAuditEvent>> ListAsync(string closeoutId, CancellationToken ct = default);
}
