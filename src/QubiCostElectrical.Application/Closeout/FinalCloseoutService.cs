using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class FinalCloseoutService : IFinalCloseoutService
{
    private readonly IProjectCloseoutRepository _closeoutRepo;
    private readonly IFinalCloseoutSnapshotRepository _snapshotRepo;
    private readonly ICloseoutAuditRepository _auditRepo;

    public FinalCloseoutService(IProjectCloseoutRepository closeoutRepo, IFinalCloseoutSnapshotRepository snapshotRepo, ICloseoutAuditRepository auditRepo)
    { _closeoutRepo = closeoutRepo; _snapshotRepo = snapshotRepo; _auditRepo = auditRepo; }

    public Task<ProjectFinalResult> CalculateFinalResultAsync(string projectId, string closeoutId, CancellationToken ct)
    {
        var result = new ProjectFinalResult
        {
            Id = ProjectFinalResultId.New(), ProjectId = projectId, CloseoutId = closeoutId,
            GenerationDate = DateTime.UtcNow.ToString("O"),
            Financial = new ProjectFinalFinancialResult { CurrencyCode = "CLP" },
            Schedule = new ProjectFinalScheduleResult(),
            Contracts = new ProjectFinalContractResult(),
            Procurement = new ProjectFinalProcurementResult { CurrencyCode = "CLP" },
            Quality = new ProjectFinalQualityResult()
        };
        return Task.FromResult(result);
    }

    public async Task<FinalCloseoutSnapshot> CreateFinalSnapshotAsync(string closeoutId, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var snapshot = new FinalCloseoutSnapshot
        {
            Id = FinalSnapshotId.New(), CloseoutId = closeoutId,
            CloseDate = now, IsOfficial = false, CloseoutVersion = 1,
            Author = actor, CreatedUtc = now,
            HashSha256 = Guid.NewGuid().ToString("N"),
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _snapshotRepo.SaveAsync(snapshot, ct);
        await _auditRepo.SaveAsync(new CloseoutAuditEvent
        { CloseoutId = closeoutId, Action = "FinalSnapshotCreated", EntityType = "FinalCloseoutSnapshot",
          EntityId = snapshot.Id.Value, PerformedBy = actor, PerformedUtc = now }, ct);
        return snapshot;
    }

    public async Task<ProjectCloseout> CloseProjectAsync(string closeoutId, string actor, CancellationToken ct)
    {
        var closeout = await _closeoutRepo.GetByIdAsync(CloseoutId.From(closeoutId), ct);
        if (closeout == null) throw new InvalidOperationException("Cierre no encontrado.");
        closeout.Status = CloseoutStatus.Archived;
        closeout.ModifiedBy = actor;
        closeout.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _closeoutRepo.SaveAsync(closeout, ct);
        return closeout;
    }

    public async Task<ProjectCloseout> ReopenProjectAsync(string closeoutId, string reason, string actor, CancellationToken ct)
    {
        var closeout = await _closeoutRepo.GetByIdAsync(CloseoutId.From(closeoutId), ct);
        if (closeout == null) throw new InvalidOperationException("Cierre no encontrado.");
        closeout.Status = CloseoutStatus.Draft;
        closeout.Comment = reason;
        closeout.ModifiedBy = actor;
        closeout.ModifiedUtc = DateTime.UtcNow.ToString("O");
        closeout.Version++;
        await _closeoutRepo.SaveAsync(closeout, ct);
        return closeout;
    }
}
