using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class PunchListService : IPunchListService
{
    private readonly IPunchListRepository _repo;
    private readonly ICloseoutAuditRepository _audit;

    public PunchListService(IPunchListRepository repo, ICloseoutAuditRepository audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<PunchItem> CreateAsync(string closeoutId, string title, string responsible, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var item = new PunchItem
        {
            Id = PunchItemId.New(),
            CloseoutId = closeoutId,
            Title = title,
            Responsible = responsible,
            DetectedDate = now,
            Status = PunchItemStatus.Open
        };
        await _repo.SaveAsync(item, ct);
        await _audit.SaveAsync(new CloseoutAuditEvent
        {
            CloseoutId = closeoutId, Action = "PunchItemCreated",
            EntityType = "PunchItem", EntityId = item.Id.Value,
            PerformedBy = actor, PerformedUtc = now
        }, ct);
        return item;
    }

    public async Task UpdateAsync(PunchItem item, string actor, CancellationToken ct)
    {
        item.Status = PunchItemStatus.InProgress;
        await _repo.SaveAsync(item, ct);
    }

    public async Task CloseAsync(PunchItemId id, string actor, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        if (item == null) throw new InvalidOperationException($"PunchItem {id.Value} not found");
        item.Status = PunchItemStatus.Closed;
        item.CorrectedDate = DateTime.UtcNow.ToString("O");
        await _repo.SaveAsync(item, ct);
        await _audit.SaveAsync(new CloseoutAuditEvent
        {
            CloseoutId = item.CloseoutId, Action = "PunchItemClosed",
            EntityType = "PunchItem", EntityId = id.Value,
            PerformedBy = actor, PerformedUtc = DateTime.UtcNow.ToString("O")
        }, ct);
    }

    public async Task ReopenAsync(PunchItemId id, string reason, string actor, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        if (item == null) throw new InvalidOperationException($"PunchItem {id.Value} not found");
        item.Status = PunchItemStatus.Reopened;
        item.Comment = reason;
        await _repo.SaveAsync(item, ct);
        await _audit.SaveAsync(new CloseoutAuditEvent
        {
            CloseoutId = item.CloseoutId, Action = "PunchItemReopened",
            EntityType = "PunchItem", EntityId = id.Value,
            PerformedBy = actor, PerformedUtc = DateTime.UtcNow.ToString("O"),
            Comment = reason
        }, ct);
    }

    public Task<List<PunchItem>> ListAsync(string closeoutId, CancellationToken ct) => _repo.ListAsync(closeoutId, ct);
}
