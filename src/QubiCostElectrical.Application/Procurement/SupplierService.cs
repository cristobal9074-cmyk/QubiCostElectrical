using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface ISupplierService
{
    Task<Supplier> CreateAsync(Supplier draft, string actor, CancellationToken ct = default);
    Task<Supplier> UpdateAsync(Supplier supplier, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task DisableAsync(SupplierId id, string actor, CancellationToken ct = default);
    Task<Supplier?> GetAsync(SupplierId id, CancellationToken ct = default);
    Task<List<Supplier>> SearchAsync(string? text, SupplierStatus? status, int skip, int take, CancellationToken ct = default);
    Task<SupplierContact> UpsertContactAsync(SupplierContact contact, string actor, CancellationToken ct = default);
    Task DeactivateContactAsync(string contactId, string actor, CancellationToken ct = default);
    Task<List<SupplierContact>> ListContactsAsync(SupplierId supplierId, CancellationToken ct = default);
}

public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _suppliers;
    private readonly ISupplierContactRepository _contacts;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;

    public SupplierService(
        ISupplierRepository suppliers,
        ISupplierContactRepository contacts,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions)
    {
        _suppliers = suppliers;
        _contacts = contacts;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<Supplier> CreateAsync(Supplier draft, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierCreate, actor, "Supplier", "Create");
        if (draft.Id is null)
            draft = WithId(draft, SupplierId.New());
        draft = NormalizeNew(draft, actor);
        var issues = ProcurementValidation.ValidateSupplier(draft, await _suppliers.GetAllCodesAsync(ct));
        ThrowIfErrors(issues);
        await _suppliers.SaveAsync(draft, ct);
        await Audit("Supplier", draft.Id.Value, "Create", actor, null, draft.Status.ToString(), draft.Code, "Ok", ct);
        return draft;
    }

    public async Task<Supplier> UpdateAsync(Supplier supplier, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierEdit, actor, "Supplier", "Update");
        var existing = await _suppliers.GetByIdAsync(supplier.Id, ct)
            ?? throw new InvalidOperationException("SupplierNotFound");
        if (!string.Equals(existing.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        var issues = ProcurementValidation.ValidateSupplier(supplier, await _suppliers.GetAllCodesAsync(ct), supplier.Id.Value);
        ThrowIfErrors(issues);
        supplier.ModifiedUtc = DateTime.UtcNow;
        supplier.ModifiedBy = actor;
        supplier.RowVersion = Guid.NewGuid().ToString("N");
        await _suppliers.SaveAsync(supplier, ct);
        await Audit("Supplier", supplier.Id.Value, "Update", actor, existing.Status.ToString(), supplier.Status.ToString(), null, "Ok", ct);
        return supplier;
    }

    public async Task DisableAsync(SupplierId id, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierDisable, actor, "Supplier", "Disable");
        var s = await _suppliers.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("SupplierNotFound");
        var prev = s.Status;
        s.Status = SupplierStatus.Inactive;
        s.ModifiedUtc = DateTime.UtcNow;
        s.ModifiedBy = actor;
        s.RowVersion = Guid.NewGuid().ToString("N");
        await _suppliers.SaveAsync(s, ct);
        await Audit("Supplier", id.Value, "Disable", actor, prev.ToString(), s.Status.ToString(), null, "Ok", ct);
    }

    public Task<Supplier?> GetAsync(SupplierId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierView, "system", "Supplier", "Get");
        return _suppliers.GetByIdAsync(id, ct);
    }

    public Task<List<Supplier>> SearchAsync(string? text, SupplierStatus? status, int skip, int take, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierView, "system", "Supplier", "Search");
        return _suppliers.SearchAsync(text, status, skip, take, ct);
    }

    public async Task<SupplierContact> UpsertContactAsync(SupplierContact contact, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierEdit, actor, "Contact", "Upsert");
        _ = await _suppliers.GetByIdAsync(contact.SupplierId, ct) ?? throw new InvalidOperationException("SupplierNotFound");
        var siblings = await _contacts.GetBySupplierAsync(contact.SupplierId, ct);
        var issues = ProcurementValidation.ValidateContact(contact, siblings);
        ThrowIfErrors(issues);
        await _contacts.SaveAsync(contact, ct);
        await Audit("Contact", contact.ContactId, "Upsert", actor, null, contact.Name, null, "Ok", ct);
        return contact;
    }

    public async Task DeactivateContactAsync(string contactId, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.SupplierEdit, actor, "Contact", "Deactivate");
        var c = await _contacts.GetByIdAsync(contactId, ct) ?? throw new InvalidOperationException("InvalidSupplierContact");
        c.IsActive = false;
        c.IsPrimary = false;
        await _contacts.SaveAsync(c, ct);
        await Audit("Contact", contactId, "Deactivate", actor, "Active", "Inactive", null, "Ok", ct);
    }

    public Task<List<SupplierContact>> ListContactsAsync(SupplierId supplierId, CancellationToken ct = default) =>
        _contacts.GetBySupplierAsync(supplierId, ct);

    private static Supplier WithId(Supplier d, SupplierId id) =>
        new()
        {
            Id = id,
            Code = d.Code,
            LegalName = d.LegalName,
            TradeName = d.TradeName,
            TaxId = d.TaxId,
            Country = d.Country,
            Address = d.Address,
            City = d.City,
            Email = d.Email,
            Phone = d.Phone,
            Website = d.Website,
            Specialties = d.Specialties,
            CategoriesJson = d.CategoriesJson,
            AcceptedCurrenciesJson = d.AcceptedCurrenciesJson,
            UsualPaymentTerms = d.UsualPaymentTerms,
            UsualLeadTimeDays = d.UsualLeadTimeDays,
            Status = d.Status,
            Remarks = d.Remarks,
            CreatedUtc = d.CreatedUtc,
            CreatedBy = d.CreatedBy,
            ModifiedUtc = d.ModifiedUtc,
            ModifiedBy = d.ModifiedBy,
            RowVersion = d.RowVersion,
            CompatibilityMetadata = d.CompatibilityMetadata
        };

    private static Supplier NormalizeNew(Supplier d, string actor) =>
        new()
        {
            Id = d.Id,
            Code = d.Code?.Trim() ?? string.Empty,
            LegalName = d.LegalName?.Trim() ?? string.Empty,
            TradeName = d.TradeName ?? string.Empty,
            TaxId = d.TaxId ?? string.Empty,
            Country = d.Country ?? string.Empty,
            Address = d.Address ?? string.Empty,
            City = d.City ?? string.Empty,
            Email = d.Email ?? string.Empty,
            Phone = d.Phone ?? string.Empty,
            Website = d.Website ?? string.Empty,
            Specialties = d.Specialties ?? string.Empty,
            CategoriesJson = d.CategoriesJson ?? "[]",
            AcceptedCurrenciesJson = d.AcceptedCurrenciesJson ?? "[\"CLP\"]",
            UsualPaymentTerms = d.UsualPaymentTerms ?? string.Empty,
            UsualLeadTimeDays = d.UsualLeadTimeDays,
            Status = d.Status == default ? SupplierStatus.Prospect : d.Status,
            Remarks = d.Remarks ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = actor,
            RowVersion = Guid.NewGuid().ToString("N"),
            CompatibilityMetadata = d.CompatibilityMetadata ?? string.Empty
        };

    private void Ensure(string permission, string actor, string entity, string action)
    {
        if (_permissions.Has(permission)) return;
        _ = Audit(entity, "", "Denied", actor, null, permission, action, "Denied", CancellationToken.None);
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }

    private Task Audit(string entity, string id, string action, string actor, string? prev, string? next, string? comment, string result, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            EntityType = entity,
            EntityId = id,
            Action = action,
            Actor = actor,
            UtcTimestamp = DateTime.UtcNow,
            PreviousState = prev,
            NewState = next,
            Comment = comment ?? string.Empty,
            Result = result
        }, ct);

    private static void ThrowIfErrors(IEnumerable<ProcurementValidationIssue> issues)
    {
        var err = issues.FirstOrDefault(i => i.Severity == ProcurementValidationSeverity.Error);
        if (err is not null) throw new InvalidOperationException($"{err.Code}:{err.Message}");
    }
}
