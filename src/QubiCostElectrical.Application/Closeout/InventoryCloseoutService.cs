using System;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class InventoryCloseoutService : IInventoryCloseoutService
{
    private readonly IInventoryCloseoutRepository _repo;
    public InventoryCloseoutService(IInventoryCloseoutRepository repo) => _repo = repo;

    public async Task<InventoryReconciliation> ReconcileAsync(string closeoutId, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var rec = new InventoryReconciliation
        {
            Id = InventoryCloseoutId.New(), CloseoutId = closeoutId,
            ReconciliationDate = now, Status = InventoryCloseoutStatus.InProgress,
            CreatedBy = actor, CreatedUtc = now,
            ModifiedBy = actor, ModifiedUtc = now,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _repo.SaveReconciliationAsync(rec, ct);
        return rec;
    }

    public async Task<SurplusDisposal> DisposeSurplusAsync(InventoryCloseoutId inventoryId, string resource, decimal qty, SurplusDisposalType type, string actor, CancellationToken ct)
    {
        var now = DateTime.UtcNow.ToString("O");
        var disp = new SurplusDisposal
        {
            Id = SurplusDisposalId.New(), InventoryId = inventoryId,
            ResourceCode = resource, Quantity = qty, DisposalType = type,
            Responsible = actor, CreatedBy = actor, CreatedUtc = now,
            RowVersion = Guid.NewGuid().ToString("N")
        };
        await _repo.SaveDisposalAsync(disp, ct);
        return disp;
    }
}
