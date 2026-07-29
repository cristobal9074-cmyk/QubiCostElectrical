using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IRfqService
{
    Task<RequestForQuotation> CreateFromRequisitionsAsync(string projectPath, IReadOnlyList<PurchaseRequisitionId> requisitionIds, string title, string actor, CancellationToken ct = default);
    Task InviteSupplierAsync(RequestForQuotationId rfqId, SupplierId supplierId, string? contactId, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<RequestForQuotation> ApproveAsync(RequestForQuotationId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<RequestForQuotation> IssueAsync(RequestForQuotationId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<QuotationResponse> RegisterResponseAsync(QuotationResponse response, string actor, CancellationToken ct = default);
    Task<RequestForQuotation?> GetAsync(RequestForQuotationId id, CancellationToken ct = default);
    Task<List<RequestForQuotation>> SearchAsync(string projectPath, RfqStatus? status, int skip, int take, CancellationToken ct = default);
    Task CancelAsync(RequestForQuotationId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
}

public sealed class RfqService : IRfqService
{
    private readonly IRequestForQuotationRepository _rfqRepo;
    private readonly IPurchaseRequisitionRepository _prRepo;
    private readonly ISupplierRepository _suppliers;
    private readonly ISupplierContactRepository _contacts;
    private readonly IQuotationResponseRepository _responses;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;

    public RfqService(
        IRequestForQuotationRepository rfqRepo,
        IPurchaseRequisitionRepository prRepo,
        ISupplierRepository suppliers,
        ISupplierContactRepository contacts,
        IQuotationResponseRepository responses,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions)
    {
        _rfqRepo = rfqRepo;
        _prRepo = prRepo;
        _suppliers = suppliers;
        _contacts = contacts;
        _responses = responses;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<RequestForQuotation> CreateFromRequisitionsAsync(
        string projectPath, IReadOnlyList<PurchaseRequisitionId> requisitionIds, string title, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqCreate, actor, "Rfq", "Create");
        if (requisitionIds.Count == 0) throw new InvalidOperationException("RequisitionNotFound");
        var items = new List<RfqItem>();
        var linked = new List<string>();
        foreach (var rid in requisitionIds)
        {
            var pr = await _prRepo.GetByIdAsync(rid, ct) ?? throw new InvalidOperationException("RequisitionNotFound");
            if (pr.Status != PurchaseRequisitionStatus.Approved && pr.Status != PurchaseRequisitionStatus.InQuotation)
                throw new InvalidOperationException("RequisitionNotApproved");
            linked.Add(pr.Id.Value);
            foreach (var it in pr.Items.Where(i => i.Status is RequisitionItemStatus.Open or RequisitionItemStatus.Consolidated))
            {
                items.Add(new RfqItem
                {
                    ItemId = Guid.NewGuid().ToString("N"),
                    RfqId = RequestForQuotationId.From("pending"),
                    ResourceId = it.ResourceId,
                    Code = it.Code,
                    Description = it.Description,
                    TechnicalSpec = it.TechnicalSpec,
                    Unit = it.Unit,
                    Quantity = it.PendingQuantity,
                    RequiredDate = it.RequiredDate,
                    WbsNodeId = it.WbsNodeId,
                    ActivityId = it.ActivityId,
                    SourceRequisitionId = pr.Id.Value,
                    SourceRequisitionItemId = it.ItemId,
                    ReferenceCurrency = it.CurrencyCode
                });
            }
            pr.Status = PurchaseRequisitionStatus.InQuotation;
            pr.RowVersion = Guid.NewGuid().ToString("N");
            await _prRepo.SaveAsync(pr, ct);
        }

        var n = await _rfqRepo.GetNextNumberAsync(projectPath, ct);
        var id = RequestForQuotationId.New();
        var rfq = new RequestForQuotation
        {
            Id = id,
            Number = $"RFQ-{n:D5}",
            ProjectPath = projectPath,
            Title = string.IsNullOrWhiteSpace(title) ? $"RFQ {n:D5}" : title,
            DeadlineUtc = DateTime.UtcNow.Date.AddDays(10),
            Responsible = actor,
            Status = RfqStatus.Draft,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = actor,
            LinkedRequisitionIds = linked,
            Items = items.Select(i => new RfqItem
            {
                ItemId = i.ItemId,
                RfqId = id,
                ResourceId = i.ResourceId,
                Code = i.Code,
                Description = i.Description,
                TechnicalSpec = i.TechnicalSpec,
                Unit = i.Unit,
                Quantity = i.Quantity,
                RequiredDate = i.RequiredDate,
                WbsNodeId = i.WbsNodeId,
                ActivityId = i.ActivityId,
                SourceRequisitionId = i.SourceRequisitionId,
                SourceRequisitionItemId = i.SourceRequisitionItemId,
                ReferenceCurrency = i.ReferenceCurrency
            }).ToList()
        };
        ThrowIfErrors(ProcurementValidation.ValidateRfq(rfq));
        await _rfqRepo.SaveAsync(rfq, ct);
        await Audit(projectPath, "Rfq", id.Value, "Create", actor, null, rfq.Number, null, "Ok", ct);
        return rfq;
    }

    public async Task InviteSupplierAsync(
        RequestForQuotationId rfqId, SupplierId supplierId, string? contactId, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqEdit, actor, "Invitation", "Invite");
        var rfq = await LoadWritable(rfqId, expectedRowVersion, ct);
        var supplier = await _suppliers.GetByIdAsync(supplierId, ct) ?? throw new InvalidOperationException("SupplierNotFound");
        if (!ProcurementValidation.IsEligible(supplier))
            throw new InvalidOperationException("SupplierNotEligible");
        if (rfq.Invitations.Any(i => i.SupplierId.Value == supplierId.Value))
            throw new InvalidOperationException("DuplicateSupplierInvitation");
        if (!string.IsNullOrWhiteSpace(contactId))
        {
            var contact = await _contacts.GetByIdAsync(contactId, ct) ?? throw new InvalidOperationException("InvalidSupplierContact");
            if (!contact.IsActive) throw new InvalidOperationException("InvalidSupplierContact");
        }
        rfq.Invitations.Add(new RfqSupplierInvitation
        {
            InvitationId = Guid.NewGuid().ToString("N"),
            RfqId = rfq.Id,
            SupplierId = supplierId,
            ContactId = contactId,
            Status = RfqInvitationStatus.Pending,
            Responsible = actor
        });
        rfq.RowVersion = Guid.NewGuid().ToString("N");
        await _rfqRepo.SaveAsync(rfq, ct);
        await Audit(rfq.ProjectPath, "Invitation", supplierId.Value, "Invite", actor, null, rfq.Id.Value, null, "Ok", ct);
    }

    public async Task<RequestForQuotation> ApproveAsync(RequestForQuotationId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqApprove, actor, "Rfq", "Approve");
        var rfq = await LoadWritable(id, expectedRowVersion, ct);
        var prev = rfq.Status;
        rfq.Status = RfqStatus.Approved;
        rfq.RowVersion = Guid.NewGuid().ToString("N");
        await _rfqRepo.SaveAsync(rfq, ct);
        await Audit(rfq.ProjectPath, "Rfq", id.Value, "Approve", actor, prev.ToString(), rfq.Status.ToString(), null, "Ok", ct);
        return rfq;
    }

    public async Task<RequestForQuotation> IssueAsync(RequestForQuotationId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqIssue, actor, "Rfq", "Issue");
        var rfq = await LoadWritable(id, expectedRowVersion, ct);
        if (rfq.Invitations.Count == 0) throw new InvalidOperationException("NoSuppliersSelected");
        ThrowIfErrors(ProcurementValidation.ValidateRfq(rfq));
        var prev = rfq.Status;
        rfq.Status = RfqStatus.Issued;
        rfq.IssuedUtc = DateTime.UtcNow;
        foreach (var inv in rfq.Invitations.Where(i => i.Status == RfqInvitationStatus.Pending))
        {
            inv.Status = RfqInvitationStatus.Sent;
            inv.SentUtc = DateTime.UtcNow;
        }
        rfq.Status = RfqStatus.Open;
        rfq.RowVersion = Guid.NewGuid().ToString("N");
        await _rfqRepo.SaveAsync(rfq, ct);
        await Audit(rfq.ProjectPath, "Rfq", id.Value, "Issue", actor, prev.ToString(), rfq.Status.ToString(), null, "Ok", ct);
        return rfq;
    }

    public async Task<QuotationResponse> RegisterResponseAsync(QuotationResponse response, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.QuotationResponseRegister, actor, "QuotationResponse", "Register");
        var rfq = await _rfqRepo.GetByIdAsync(response.RfqId, ct) ?? throw new InvalidOperationException("RfqNotFound");
        if (rfq.Status is not RfqStatus.Open and not RfqStatus.Issued and not RfqStatus.PartiallyResponded)
            throw new InvalidOperationException("InvalidQuotationResponse");
        if (!rfq.Invitations.Any(i => i.SupplierId.Value == response.SupplierId.Value))
            throw new InvalidOperationException("SupplierNotEligible");
        response = new QuotationResponse
        {
            ResponseId = string.IsNullOrWhiteSpace(response.ResponseId) ? Guid.NewGuid().ToString("N") : response.ResponseId,
            RfqId = response.RfqId,
            SupplierId = response.SupplierId,
            ReceivedUtc = response.ReceivedUtc == default ? DateTime.UtcNow : response.ReceivedUtc,
            CurrencyCode = string.IsNullOrWhiteSpace(response.CurrencyCode) ? rfq.ComparisonCurrency : response.CurrencyCode,
            ValidityUtc = response.ValidityUtc,
            DeliveryLeadDays = response.DeliveryLeadDays,
            Comment = response.Comment ?? string.Empty,
            OfferDocumentRef = response.OfferDocumentRef,
            Status = QuotationResponseStatus.Received,
            IntegrityHash = ComputeHash(response),
            RecordedBy = actor
        };
        await _responses.SaveAsync(response, ct);
        var inv = rfq.Invitations.First(i => i.SupplierId.Value == response.SupplierId.Value);
        inv.Status = RfqInvitationStatus.Responded;
        var responded = rfq.Invitations.Count(i => i.Status == RfqInvitationStatus.Responded);
        rfq.Status = responded >= rfq.Invitations.Count ? RfqStatus.Closed : RfqStatus.PartiallyResponded;
        rfq.RowVersion = Guid.NewGuid().ToString("N");
        await _rfqRepo.SaveAsync(rfq, ct);
        await Audit(rfq.ProjectPath, "QuotationResponse", response.ResponseId, "Register", actor, null, response.SupplierId.Value, null, "Ok", ct);
        return response;
    }

    public Task<RequestForQuotation?> GetAsync(RequestForQuotationId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqView, "system", "Rfq", "Get");
        return _rfqRepo.GetByIdAsync(id, ct);
    }

    public Task<List<RequestForQuotation>> SearchAsync(string projectPath, RfqStatus? status, int skip, int take, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqView, "system", "Rfq", "Search");
        return _rfqRepo.SearchAsync(projectPath, status, skip, take, ct);
    }

    public async Task CancelAsync(RequestForQuotationId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.RfqEdit, actor, "Rfq", "Cancel");
        var rfq = await LoadWritable(id, expectedRowVersion, ct);
        var prev = rfq.Status;
        rfq.Status = RfqStatus.Cancelled;
        rfq.Remarks = reason;
        rfq.RowVersion = Guid.NewGuid().ToString("N");
        await _rfqRepo.SaveAsync(rfq, ct);
        await Audit(rfq.ProjectPath, "Rfq", id.Value, "Cancel", actor, prev.ToString(), rfq.Status.ToString(), reason, "Ok", ct);
    }

    private async Task<RequestForQuotation> LoadWritable(RequestForQuotationId id, string expectedRowVersion, CancellationToken ct)
    {
        var rfq = await _rfqRepo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("RfqNotFound");
        if (!string.Equals(rfq.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        return rfq;
    }

    private static string ComputeHash(QuotationResponse r)
    {
        var payload = $"{r.RfqId.Value}|{r.SupplierId.Value}|{r.ReceivedUtc:O}|{r.CurrencyCode}|{r.DeliveryLeadDays}|{r.Comment}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private void Ensure(string permission, string actor, string entity, string action)
    {
        if (_permissions.Has(permission)) return;
        _ = Audit("", entity, "", "Denied", actor, null, permission, action, "Denied", CancellationToken.None);
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }

    private Task Audit(string project, string entity, string id, string action, string actor, string? prev, string? next, string? comment, string result, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectPath = project,
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
