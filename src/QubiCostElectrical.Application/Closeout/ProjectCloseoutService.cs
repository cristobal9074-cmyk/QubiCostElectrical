using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class ProjectCloseoutService : IProjectCloseoutService
{
    private readonly IProjectCloseoutRepository _repo;
    private readonly ICloseoutAuditRepository _audit;

    public ProjectCloseoutService(IProjectCloseoutRepository repo, ICloseoutAuditRepository audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<ProjectCloseout> CreateAsync(string projectId, string code, string name, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var closeout = new ProjectCloseout
        {
            Id = CloseoutId.New(),
            ProjectId = projectId,
            Code = code,
            Name = name,
            Status = CloseoutStatus.Draft,
            CreatedUtc = now,
            CreatedBy = actor,
            ModifiedUtc = now,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _repo.SaveAsync(closeout, ct);
        await _audit.SaveAsync(new CloseoutAuditEvent
        {
            CloseoutId = closeout.Id.Value,
            Action = "CloseoutCreated",
            EntityType = "ProjectCloseout",
            EntityId = closeout.Id.Value,
            PerformedBy = actor,
            PerformedUtc = now,
            NewValue = name
        }, ct);
        return closeout;
    }

    public Task<ProjectCloseout?> GetAsync(CloseoutId id, CancellationToken ct) => _repo.GetByIdAsync(id, ct);
    public Task<ProjectCloseout?> GetByProjectAsync(string projectId, CancellationToken ct) => _repo.GetByProjectAsync(projectId, ct);
    public Task<List<ProjectCloseout>> ListAsync(string? projectId, CancellationToken ct) => _repo.ListAsync(projectId, ct);

    public async Task SaveAsync(ProjectCloseout closeout, string actor, CancellationToken ct)
    {
        closeout.ModifiedUtc = DateTime.UtcNow.ToString("O");
        closeout.ModifiedBy = actor;
        await _repo.SaveAsync(closeout, ct);
    }
}
