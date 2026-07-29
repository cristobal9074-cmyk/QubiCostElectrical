using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical.Tests.Application.Procurement;

internal sealed class FakeSupplierRepository : ISupplierRepository
{
    private readonly ConcurrentDictionary<string, Supplier> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(Supplier supplier, CancellationToken ct = default)
    {
        _map[supplier.Id.Value] = supplier;
        return Task.CompletedTask;
    }
    public Task<Supplier?> GetByIdAsync(SupplierId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var s) ? s : null);
    public Task<Supplier?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)));
    public Task<List<Supplier>> SearchAsync(string? text, SupplierStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var q = _map.Values.AsEnumerable();
        if (status.HasValue) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(text))
            q = q.Where(x => x.Code.Contains(text, StringComparison.OrdinalIgnoreCase) || x.LegalName.Contains(text, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(q.OrderBy(x => x.Code).Skip(skip).Take(take <= 0 ? 50 : take).ToList());
    }
    public Task<List<string>> GetAllCodesAsync(CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Select(x => x.Code).ToList());
}

internal sealed class FakeContactRepository : ISupplierContactRepository
{
    private readonly ConcurrentDictionary<string, SupplierContact> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(SupplierContact contact, CancellationToken ct = default)
    {
        _map[contact.ContactId] = contact;
        return Task.CompletedTask;
    }
    public Task<List<SupplierContact>> GetBySupplierAsync(SupplierId supplierId, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(c => c.SupplierId.Value == supplierId.Value).ToList());
    public Task<SupplierContact?> GetByIdAsync(string contactId, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(contactId, out var c) ? c : null);
    public Task DeleteAsync(string contactId, CancellationToken ct = default)
    {
        _map.TryRemove(contactId, out _);
        return Task.CompletedTask;
    }
}

internal sealed class FakePrRepository : IPurchaseRequisitionRepository
{
    private readonly ConcurrentDictionary<string, PurchaseRequisition> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(PurchaseRequisition requisition, CancellationToken ct = default)
    {
        _map[requisition.Id.Value] = requisition;
        return Task.CompletedTask;
    }
    public Task<PurchaseRequisition?> GetByIdAsync(PurchaseRequisitionId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var r) ? r : null);
    public Task<List<PurchaseRequisition>> SearchAsync(string projectPath, PurchaseRequisitionStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var q = _map.Values.Where(x => x.ProjectPath == projectPath);
        if (status.HasValue) q = q.Where(x => x.Status == status);
        return Task.FromResult(q.OrderByDescending(x => x.Number).Skip(skip).Take(take <= 0 ? 50 : take).ToList());
    }
    public Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Count(x => x.ProjectPath == projectPath) + 1);
}

internal sealed class FakeRfqRepository : IRequestForQuotationRepository
{
    private readonly ConcurrentDictionary<string, RequestForQuotation> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(RequestForQuotation rfq, CancellationToken ct = default)
    {
        _map[rfq.Id.Value] = rfq;
        return Task.CompletedTask;
    }
    public Task<RequestForQuotation?> GetByIdAsync(RequestForQuotationId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var r) ? r : null);
    public Task<List<RequestForQuotation>> SearchAsync(string projectPath, RfqStatus? status, int skip, int take, CancellationToken ct = default)
    {
        var q = _map.Values.Where(x => x.ProjectPath == projectPath);
        if (status.HasValue) q = q.Where(x => x.Status == status);
        return Task.FromResult(q.OrderByDescending(x => x.Number).Skip(skip).Take(take <= 0 ? 50 : take).ToList());
    }
    public Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Count(x => x.ProjectPath == projectPath) + 1);
}

internal sealed class FakeQrRepository : IQuotationResponseRepository
{
    private readonly List<QuotationResponse> _list = new();
    public Task SaveAsync(QuotationResponse response, CancellationToken ct = default)
    {
        _list.RemoveAll(x => x.ResponseId == response.ResponseId);
        _list.Add(response);
        return Task.CompletedTask;
    }
    public Task<List<QuotationResponse>> GetByRfqAsync(RequestForQuotationId rfqId, CancellationToken ct = default) =>
        Task.FromResult(_list.Where(x => x.RfqId.Value == rfqId.Value).ToList());
}

internal sealed class FakeProcAuditRepository : IProcurementAuditRepository
{
    public List<ProcurementAuditEvent> Events { get; } = new();
    public Task RecordAsync(ProcurementAuditEvent evt, CancellationToken ct = default)
    {
        Events.Add(evt);
        return Task.CompletedTask;
    }
    public Task<List<ProcurementAuditEvent>> GetByEntityAsync(string entityType, string entityId, int limit = 100, CancellationToken ct = default) =>
        Task.FromResult(Events.Where(e => e.EntityType == entityType && e.EntityId == entityId).Take(limit).ToList());
}
