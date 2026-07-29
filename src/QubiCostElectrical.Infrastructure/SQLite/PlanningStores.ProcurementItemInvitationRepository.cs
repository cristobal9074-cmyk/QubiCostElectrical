using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public sealed class SqlitePurchaseRequisitionItemRepository : IPurchaseRequisitionItemRepository
{
    private readonly IPurchaseRequisitionRepository _prs;
    public SqlitePurchaseRequisitionItemRepository(IPurchaseRequisitionRepository prs) => _prs = prs;

    public async Task<List<PurchaseRequisitionItem>> GetByRequisitionAsync(PurchaseRequisitionId requisitionId, CancellationToken ct = default)
    {
        var pr = await _prs.GetByIdAsync(requisitionId, ct);
        return pr?.Items.ToList() ?? new List<PurchaseRequisitionItem>();
    }
}

public sealed class SqliteRfqSupplierInvitationRepository : IRfqSupplierInvitationRepository
{
    private readonly IRequestForQuotationRepository _rfqs;
    public SqliteRfqSupplierInvitationRepository(IRequestForQuotationRepository rfqs) => _rfqs = rfqs;

    public async Task<List<RfqSupplierInvitation>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default)
    {
        var rfq = await _rfqs.GetByIdAsync(rfqId, ct);
        return rfq?.Invitations.ToList() ?? new List<RfqSupplierInvitation>();
    }
}
