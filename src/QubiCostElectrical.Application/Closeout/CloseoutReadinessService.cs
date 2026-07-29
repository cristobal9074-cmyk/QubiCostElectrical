using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class CloseoutReadinessService : ICloseoutReadinessService
{
    private readonly IProjectCloseoutRepository _closeoutRepo;
    private readonly ICloseoutChecklistRepository _checklistRepo;
    private readonly IPunchListRepository _punchRepo;
    private readonly ICloseoutNonConformanceRepository _ncRepo;
    private readonly ICloseoutDocumentRepository _docRepo;
    private readonly ICloseoutSnapshotRepository _snapshotRepo;

    public CloseoutReadinessService(
        IProjectCloseoutRepository closeoutRepo,
        ICloseoutChecklistRepository checklistRepo,
        IPunchListRepository punchRepo,
        ICloseoutNonConformanceRepository ncRepo,
        ICloseoutDocumentRepository docRepo,
        ICloseoutSnapshotRepository snapshotRepo)
    {
        _closeoutRepo = closeoutRepo;
        _checklistRepo = checklistRepo;
        _punchRepo = punchRepo;
        _ncRepo = ncRepo;
        _docRepo = docRepo;
        _snapshotRepo = snapshotRepo;
    }

    public async Task<CloseoutReadinessResult> CalculateAsync(string closeoutId, CancellationToken ct)
    {
        var checklists = await _checklistRepo.ListChecklistsAsync(CloseoutId.From(closeoutId), ct);
        var allItems = new List<CloseoutChecklistItem>();
        foreach (var cl in checklists)
            allItems.AddRange(await _checklistRepo.ListItemsAsync(cl.Id, ct));

        var punchItems = await _punchRepo.ListAsync(closeoutId, ct);
        var ncs = await _ncRepo.ListAsync(closeoutId, ct);
        var docs = await _docRepo.ListDocumentsAsync(closeoutId, ct);

        var checklistPct = allItems.Count == 0 ? 100 : (double)allItems.Count(i => i.Status == ChecklistItemStatus.Verified) / allItems.Count * 100;
        var punchOpen = punchItems.Count(i => i.Status is PunchItemStatus.Open or PunchItemStatus.Assigned or PunchItemStatus.InProgress);
        var punchPct = punchItems.Count == 0 ? 100 : (double)punchItems.Count(i => i.Status == PunchItemStatus.Closed) / punchItems.Count * 100;
        var ncOpen = ncs.Count(i => i.Status is NonConformanceStatus.Open or NonConformanceStatus.UnderAnalysis or NonConformanceStatus.ActionRequired or NonConformanceStatus.InCorrection);
        var ncPct = ncs.Count == 0 ? 100 : (double)ncs.Count(i => i.Status == NonConformanceStatus.Closed) / ncs.Count * 100;
        var docPct = docs.Count == 0 ? 100 : (double)docs.Count(i => i.Status == DocumentStatus.Approved) / docs.Count * 100;

        var blockers = new List<CloseoutBlockingIssue>();
        if (allItems.Any(i => i.IsBlocking && i.Status != ChecklistItemStatus.Verified))
            blockers.Add(new CloseoutBlockingIssue { Code = "BLK-CHK", Title = "Checklist bloqueante pendiente", Dimension = CloseoutDimension.Technical, IsCritical = true });
        if (punchItems.Any(i => i.Status == PunchItemStatus.Open && i.Severity == "Critical"))
            blockers.Add(new CloseoutBlockingIssue { Code = "BLK-PUNCH", Title = "Punch crítico abierto", Dimension = CloseoutDimension.Technical, IsCritical = true });
        if (ncs.Any(i => i.Status == NonConformanceStatus.Open && i.Severity == "Critical"))
            blockers.Add(new CloseoutBlockingIssue { Code = "BLK-NC", Title = "NC crítica abierta", Dimension = CloseoutDimension.Technical, IsCritical = true });

        var scores = new List<CloseoutReadinessScore>
        {
            new() { Dimension = CloseoutDimension.Technical, Pct = (checklistPct + punchPct + ncPct) / 3, Blockers = blockers.Where(b => b.Dimension == CloseoutDimension.Technical).Select(b => b.Code).ToList() },
            new() { Dimension = CloseoutDimension.Documental, Pct = docPct },
            new() { Dimension = CloseoutDimension.Economic, Pct = 100 },
            new() { Dimension = CloseoutDimension.Contractual, Pct = 100 },
            new() { Dimension = CloseoutDimension.Purchasing, Pct = 100 },
            new() { Dimension = CloseoutDimension.Planning, Pct = 100 }
        };

        var globalPct = blockers.Any(b => b.IsCritical) ? Math.Min(scores.Average(s => s.Pct), 80) : scores.Average(s => s.Pct);

        return new CloseoutReadinessResult
        {
            GlobalPct = Math.Round(globalPct, 1),
            Dimensions = scores, Blockers = blockers,
            IsReady = blockers.Count == 0,
            GeneratedUtc = DateTime.UtcNow.ToString("O"),
            DataQuality = DataQualityStatus.Complete
        };
    }

    public async Task<CloseoutSnapshot> CreateSnapshotAsync(string closeoutId, string comment, string actor, CancellationToken ct)
    {
        var result = await CalculateAsync(closeoutId, ct);
        var now = DateTime.UtcNow.ToString("O");
        var snapshot = new CloseoutSnapshot
        {
            Id = CloseoutSnapshotId.New(),
            CloseoutId = closeoutId,
            StatusDate = now,
            ReadinessScore = System.Text.Json.JsonSerializer.Serialize(result),
            BlockerData = System.Text.Json.JsonSerializer.Serialize(result.Blockers),
            CreatedBy = actor, Comment = comment,
            CreatedUtc = now, Hash = Guid.NewGuid().ToString("N")
        };
        await _snapshotRepo.SaveAsync(snapshot, ct);
        return snapshot;
    }

    public Task<List<CloseoutSnapshot>> ListSnapshotsAsync(string closeoutId, CancellationToken ct) => _snapshotRepo.ListAsync(closeoutId, ct);
}
