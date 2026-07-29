using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class ProjectReceptionService : IProjectReceptionService
{
    private readonly IProjectReceptionRepository _repo;
    public ProjectReceptionService(IProjectReceptionRepository repo) => _repo = repo;

    public async Task<ProjectReception> CreateAsync(string closeoutId, ReceptionType type, string title, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var reception = new ProjectReception
        {
            Id = ProjectReceptionId.New(), CloseoutId = closeoutId,
            ReceptionType = type, Title = title,
            Status = ReceptionStatus.Draft, CreatedBy = actor, CreatedUtc = now,
            ModifiedBy = actor, ModifiedUtc = now,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _repo.SaveAsync(reception, ct);
        return reception;
    }

    public Task<List<ProjectReception>> ListAsync(string closeoutId, CancellationToken ct) => _repo.ListAsync(closeoutId, ct);

    public async Task ApproveAsync(ProjectReceptionId id, string actor, CancellationToken ct)
    {
        var r = await _repo.GetByIdAsync(id, ct);
        if (r == null) throw new InvalidOperationException("Recepción no encontrada.");
        if (r.Status is ReceptionStatus.Approved or ReceptionStatus.Completed or ReceptionStatus.Cancelled)
            throw new InvalidOperationException("Recepción ya finalizada.");
        r.Status = ReceptionStatus.Approved;
        r.ApprovedBy = actor;
        r.ApprovalDate = DateTime.UtcNow.ToString("O");
        r.ModifiedBy = actor;
        r.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _repo.SaveAsync(r, ct);
    }

    public async Task RejectAsync(ProjectReceptionId id, string reason, string actor, CancellationToken ct)
    {
        var r = await _repo.GetByIdAsync(id, ct);
        if (r == null) throw new InvalidOperationException("Recepción no encontrada.");
        r.Status = ReceptionStatus.Rejected;
        r.Observations = reason;
        r.ModifiedBy = actor;
        r.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _repo.SaveAsync(r, ct);
    }
}
