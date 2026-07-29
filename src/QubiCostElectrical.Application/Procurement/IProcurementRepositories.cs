using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface ISupplierRepository
{
    Task SaveAsync(Supplier supplier, CancellationToken ct = default);
    Task<Supplier?> GetByIdAsync(SupplierId id, CancellationToken ct = default);
    Task<Supplier?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Supplier>> SearchAsync(string? text, SupplierStatus? status, int skip, int take, CancellationToken ct = default);
    Task<List<string>> GetAllCodesAsync(CancellationToken ct = default);
}

public interface ISupplierContactRepository
{
    Task SaveAsync(SupplierContact contact, CancellationToken ct = default);
    Task<List<SupplierContact>> GetBySupplierAsync(SupplierId supplierId, CancellationToken ct = default);
    Task<SupplierContact?> GetByIdAsync(string contactId, CancellationToken ct = default);
    Task DeleteAsync(string contactId, CancellationToken ct = default);
}

public interface IPurchaseRequisitionRepository
{
    Task SaveAsync(PurchaseRequisition requisition, CancellationToken ct = default);
    Task<PurchaseRequisition?> GetByIdAsync(PurchaseRequisitionId id, CancellationToken ct = default);
    Task<List<PurchaseRequisition>> SearchAsync(string projectPath, PurchaseRequisitionStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
}

public interface IRequestForQuotationRepository
{
    Task SaveAsync(RequestForQuotation rfq, CancellationToken ct = default);
    Task<RequestForQuotation?> GetByIdAsync(RequestForQuotationId id, CancellationToken ct = default);
    Task<List<RequestForQuotation>> SearchAsync(string projectPath, RfqStatus? status, int skip, int take, CancellationToken ct = default);
    Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default);
}

public interface IQuotationResponseRepository
{
    Task SaveAsync(QuotationResponse response, CancellationToken ct = default);
    Task<List<QuotationResponse>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}

public interface IProcurementDocumentRepository
{
    Task SaveAsync(ProcurementDocumentRef doc, CancellationToken ct = default);
    Task<List<ProcurementDocumentRef>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default);
}

public interface IProcurementAuditRepository
{
    Task RecordAsync(ProcurementAuditEvent evt, CancellationToken ct = default);
    Task<List<ProcurementAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 100, CancellationToken ct = default);
}

public interface IPurchaseRequisitionItemRepository
{
    Task<List<PurchaseRequisitionItem>> GetByRequisitionAsync(PurchaseRequisitionId requisitionId, CancellationToken ct = default);
}

public interface IRfqSupplierInvitationRepository
{
    Task<List<RfqSupplierInvitation>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}

public interface ISupplierOfferRepository
{
    Task SaveAsync(SupplierOffer offer, CancellationToken ct = default);
    Task<SupplierOffer?> GetByIdAsync(SupplierOfferId id, CancellationToken ct = default);
    Task<List<SupplierOffer>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}

public interface ITechnicalEvaluationRepository
{
    Task SaveAsync(TechnicalEvaluation evaluation, CancellationToken ct = default);
    Task<List<TechnicalEvaluation>> GetByOfferAsync(SupplierOfferId offerId, CancellationToken ct = default);
    Task<List<TechnicalEvaluation>> GetByRfqOffersAsync(IEnumerable<SupplierOfferId> offerIds, CancellationToken ct = default);
}

public interface ICommercialEvaluationRepository
{
    Task SaveAsync(CommercialEvaluation evaluation, CancellationToken ct = default);
    Task<CommercialEvaluation?> GetByOfferAsync(SupplierOfferId offerId, CancellationToken ct = default);
    Task<List<CommercialEvaluation>> GetByRfqOffersAsync(IEnumerable<SupplierOfferId> offerIds, CancellationToken ct = default);
}

public interface IBidComparisonRepository
{
    Task SaveAsync(BidComparison comparison, CancellationToken ct = default);
    Task<BidComparison?> GetByIdAsync(BidComparisonId id, CancellationToken ct = default);
    Task<List<BidComparison>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}

public interface IAwardRepository
{
    Task SaveAsync(AwardDecision award, CancellationToken ct = default);
    Task<AwardDecision?> GetByIdAsync(AwardId id, CancellationToken ct = default);
    Task<List<AwardDecision>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default);
}
