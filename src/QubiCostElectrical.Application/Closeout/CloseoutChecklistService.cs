using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class CloseoutChecklistService : ICloseoutChecklistService
{
    private readonly ICloseoutChecklistRepository _repo;
    private readonly ICloseoutAuditRepository _audit;

    public CloseoutChecklistService(ICloseoutChecklistRepository repo, ICloseoutAuditRepository audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<CloseoutChecklist> ApplyTemplateAsync(CloseoutId closeoutId, ChecklistTemplateId templateId, CancellationToken ct)
    {
        var template = await _repo.GetTemplateByIdAsync(templateId, ct);
        if (template == null) throw new InvalidOperationException($"Template {templateId.Value} not found");

        var now = DateTime.UtcNow.ToString("O");
        var checklist = new CloseoutChecklist
        {
            Id = ChecklistId.New(),
            CloseoutId = closeoutId,
            TemplateId = template.Id.Value,
            TemplateVersion = template.Version,
            Name = template.Name,
            CreatedUtc = now,
            ModifiedUtc = now
        };
        await _repo.SaveChecklistAsync(checklist, ct);
        return checklist;
    }

    public Task<List<CloseoutChecklistTemplate>> ListTemplatesAsync(CancellationToken ct) => _repo.ListTemplatesAsync(ct);

    public async Task<CloseoutChecklistTemplate> CreateTemplateAsync(string name, string? projectType, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var template = new CloseoutChecklistTemplate
        {
            Id = ChecklistTemplateId.New(),
            Name = name,
            ProjectType = projectType ?? "",
            CreatedUtc = now,
            CreatedBy = actor,
            ModifiedUtc = now,
            ModifiedBy = actor
        };
        await _repo.SaveTemplateAsync(template, ct);
        return template;
    }

    public async Task UpdateItemStatusAsync(ChecklistItemId itemId, ChecklistItemStatus status, string actor, string? comment, CancellationToken ct)
    {
        var item = await _repo.GetItemByIdAsync(itemId, ct);
        if (item == null) throw new InvalidOperationException($"Item {itemId.Value} not found");

        item.Status = status;
        if (comment != null) item.Comment = comment;
        await _repo.SaveItemAsync(item, ct);
        await _audit.SaveAsync(new CloseoutAuditEvent
        {
            CloseoutId = item.ChecklistId.Value,
            Action = $"ChecklistItemStatusChanged:{status}",
            EntityType = "ChecklistItem",
            EntityId = item.Id.Value,
            PreviousValue = item.Status.ToString(),
            NewValue = status.ToString(),
            PerformedBy = actor,
            PerformedUtc = DateTime.UtcNow.ToString("O")
        }, ct);
    }

    public async Task VerifyItemAsync(ChecklistItemId itemId, string actor, CancellationToken ct)
    {
        var item = await _repo.GetItemByIdAsync(itemId, ct);
        if (item == null) throw new InvalidOperationException($"Item {itemId.Value} not found");
        if (item.EvidenceRequired && string.IsNullOrEmpty(item.LinkedEvidenceIds))
            throw new InvalidOperationException("Evidence is required before verification");

        item.Status = ChecklistItemStatus.Verified;
        item.VerifiedBy = actor;
        item.VerificationDate = DateTime.UtcNow.ToString("O");
        await _repo.SaveItemAsync(item, ct);
    }

    public Task<List<CloseoutChecklistItem>> GetItemsAsync(ChecklistId checklistId, CancellationToken ct) => _repo.ListItemsAsync(checklistId, ct);
}
