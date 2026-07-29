using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class CloseoutDocumentService : ICloseoutDocumentService
{
    private readonly ICloseoutDocumentRepository _repo;
    private readonly ICloseoutEvidenceRepository _evidenceRepo;
    private readonly ICloseoutAuditRepository _audit;

    public CloseoutDocumentService(ICloseoutDocumentRepository repo, ICloseoutEvidenceRepository evidenceRepo, ICloseoutAuditRepository audit)
    {
        _repo = repo;
        _evidenceRepo = evidenceRepo;
        _audit = audit;
    }

    public async Task<CloseoutDocumentRequirement> AddRequirementAsync(string closeoutId, string docType, string category, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var req = new CloseoutDocumentRequirement
        {
            Id = DocumentRequirementId.New(),
            CloseoutId = closeoutId,
            DocumentType = docType,
            Category = category
        };
        await _repo.SaveRequirementAsync(req, ct);
        return req;
    }

    public Task<List<CloseoutDocumentRequirement>> ListRequirementsAsync(string closeoutId, CancellationToken ct) => _repo.ListRequirementsAsync(closeoutId, ct);
    public Task<List<CloseoutDocument>> ListDocumentsAsync(string closeoutId, CancellationToken ct) => _repo.ListDocumentsAsync(closeoutId, ct);
}
