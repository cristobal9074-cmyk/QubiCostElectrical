using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class CloseoutReleaseService : ICloseoutReleaseService
{
    private readonly IGuaranteeReleaseRepository _guaranteeRepo;
    private readonly ICloseoutRetentionReleaseRepository _retentionRepo;
    public CloseoutReleaseService(IGuaranteeReleaseRepository guaranteeRepo, ICloseoutRetentionReleaseRepository retentionRepo)
    { _guaranteeRepo = guaranteeRepo; _retentionRepo = retentionRepo; }

    public async Task<GuaranteeReleaseRequest> RequestGuaranteeReleaseAsync(string closeoutId, string guaranteeRef, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var req = new GuaranteeReleaseRequest
        {
            Id = GuaranteeReleaseId.New(), CloseoutId = closeoutId,
            GuaranteeRef = guaranteeRef, Status = GuaranteeReleaseStatus.Requested,
            RequestedBy = actor, RequestedUtc = now,
            ModifiedBy = actor, ModifiedUtc = now,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _guaranteeRepo.SaveRequestAsync(req, ct);
        return req;
    }

    public async Task ApproveGuaranteeReleaseAsync(GuaranteeReleaseId id, string actor, CancellationToken ct)
    {
        var req = await _guaranteeRepo.GetRequestByIdAsync(id, ct);
        if (req == null) throw new InvalidOperationException("Solicitud no encontrada.");
        req.Status = GuaranteeReleaseStatus.Approved;
        req.ModifiedBy = actor;
        req.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _guaranteeRepo.SaveRequestAsync(req, ct);
        await _guaranteeRepo.SaveApprovalAsync(new GuaranteeReleaseApproval
        { ReleaseId = id, IsApproved = true, ApprovedBy = actor, ApprovalDate = DateTime.UtcNow.ToString("O") }, ct);
    }

    public async Task<RetentionReleaseRequest> RequestRetentionReleaseAsync(string closeoutId, decimal amount, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var req = new RetentionReleaseRequest
        {
            Id = RetentionReleaseId.New(), CloseoutId = closeoutId,
            RetentionAmount = amount, Status = RetentionReleaseStatus.Requested,
            RequestedBy = actor, RequestedUtc = now,
            ModifiedBy = actor, ModifiedUtc = now,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _retentionRepo.SaveAsync(req, ct);
        return req;
    }

    public async Task ApproveRetentionReleaseAsync(RetentionReleaseId id, string actor, CancellationToken ct)
    {
        var req = await _retentionRepo.GetByIdAsync(id, ct);
        if (req == null) throw new InvalidOperationException("Solicitud no encontrada.");
        req.Status = RetentionReleaseStatus.Approved;
        req.ApprovedBy = actor;
        req.ApprovalDate = DateTime.UtcNow.ToString("O");
        req.ModifiedBy = actor;
        req.ModifiedUtc = DateTime.UtcNow.ToString("O");
        await _retentionRepo.SaveAsync(req, ct);
    }
}
