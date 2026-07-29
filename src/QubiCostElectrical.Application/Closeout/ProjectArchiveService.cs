using System;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class ProjectArchiveService : IProjectArchiveService
{
    private readonly IProjectArchiveManifestRepository _repo;
    public ProjectArchiveService(IProjectArchiveManifestRepository repo) => _repo = repo;

    public async Task<ProjectArchiveManifest> PrepareManifestAsync(string projectId, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var manifest = new ProjectArchiveManifest
        {
            Id = ProjectArchiveManifestId.New(), ProjectId = projectId,
            ArchiveDate = now, Status = "Prepared",
            CreatedBy = actor, CreatedUtc = now,
            ModifiedBy = actor, ModifiedUtc = now
        };
        await _repo.SaveAsync(manifest, ct);
        return manifest;
    }

    public async Task<ProjectArchiveManifest> GetManifestAsync(string projectId, CancellationToken ct)
        => await _repo.GetByProjectAsync(projectId, ct) ?? new ProjectArchiveManifest();
}
