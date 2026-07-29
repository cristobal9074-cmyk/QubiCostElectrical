using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IPurchaseOrderRepository
{
    Task SaveAsync(PurchaseOrder order, CancellationToken ct = default);
    Task<PurchaseOrder?> GetByIdAsync(PurchaseOrderId id, CancellationToken ct = default);
    Task<List<PurchaseOrder>> GetByProjectAsync(string projectPath, CancellationToken ct = default);
    Task<List<PurchaseOrder>> GetByAwardAsync(AwardId awardId, CancellationToken ct = default);
}

public interface IPurchaseOrderService
{
    Task<List<PurchaseOrder>> GenerateFromAwardAsync(AwardId awardId, string actor, string baseCurrency = "CLP", decimal exchangeRate = 1m, CancellationToken ct = default);
    Task<PurchaseOrder> SubmitAsync(PurchaseOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PurchaseOrder> ApproveAsync(PurchaseOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PurchaseOrder> RejectAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<PurchaseOrder> IssueAsync(PurchaseOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<PurchaseOrder> ConfirmSupplierAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string confirmationRef, DateTime? committedDate, CancellationToken ct = default);
    Task<PurchaseOrder> CreateVersionAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string changeReason, Action<PurchaseOrder>? mutate = null, CancellationToken ct = default);
    Task<PurchaseOrder> CancelAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default);
    Task<PurchaseOrder> CloseAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string notes, CancellationToken ct = default);
    Task<PurchaseOrder?> GetAsync(PurchaseOrderId id, CancellationToken ct = default);
    Task<List<PurchaseOrder>> ListByProjectAsync(string projectPath, CancellationToken ct = default);
    Task<string> ExportCsvAsync(string projectPath, CancellationToken ct = default);
}

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IPurchaseOrderRepository _repo;
    private readonly IAwardRepository _awards;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;
    private readonly ProcurementSoDPolicy _sod;

    public PurchaseOrderService(
        IPurchaseOrderRepository repo,
        IAwardRepository awards,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions,
        ProcurementSoDPolicy? sod = null)
    {
        _repo = repo;
        _awards = awards;
        _audit = audit;
        _permissions = permissions;
        _sod = sod ?? new ProcurementSoDPolicy { PoCreatorCannotApprove = true };
    }

    public async Task<List<PurchaseOrder>> GenerateFromAwardAsync(
        AwardId awardId, string actor, string baseCurrency = "CLP", decimal exchangeRate = 1m, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderCreate, actor);
        var award = await _awards.GetByIdAsync(awardId, ct) ?? throw new InvalidOperationException("AwardNotFound");
        if (award.Status != AwardStatus.Approved) throw new InvalidOperationException("AwardNotApproved");
        var existing = await _repo.GetByAwardAsync(awardId, ct);
        if (existing.Count > 0) throw new InvalidOperationException("PurchaseOrdersAlreadyGenerated");

        var orders = new List<PurchaseOrder>();
        foreach (var group in award.Items.GroupBy(i => i.SupplierId.Value))
        {
            var poId = PurchaseOrderId.New();
            var first = group.First();
            var po = new PurchaseOrder
            {
                Id = poId,
                Number = $"OC-{DateTime.UtcNow:yyyyMMdd}-{poId.Value[..6].ToUpperInvariant()}",
                ProjectPath = award.ProjectPath,
                AwardId = award.Id,
                SupplierId = first.SupplierId,
                RfqId = award.RfqId,
                Status = PurchaseOrderStatus.Draft,
                CurrencyCode = first.CurrencyOriginal,
                ExchangeRate = exchangeRate <= 0m ? 1m : exchangeRate,
                ExchangeRateDate = DateTime.Today,
                CreatedBy = actor,
                CreatedUtc = DateTime.UtcNow,
                ModifiedBy = actor,
                ModifiedUtc = DateTime.UtcNow,
                Lines = group.Select(i => new PurchaseOrderLine
                {
                    LineId = Guid.NewGuid().ToString("N"),
                    PurchaseOrderId = poId,
                    AwardedItemId = i.AwardedItemId,
                    RfqItemId = i.RfqItemId,
                    Code = i.Code,
                    Description = i.Description,
                    QuantityOrdered = i.Quantity,
                    UnitPriceOriginal = i.UnitPriceOriginal != 0m ? i.UnitPriceOriginal : i.UnitPriceBase,
                    UnitPriceBase = i.UnitPriceBase
                }).ToList()
            };
            foreach (var line in po.Lines)
            {
                line.LineTotalOriginal = PrecisionPolicy.RoundImporte(line.UnitPriceOriginal * line.QuantityOrdered);
                line.LineTotalBase = PrecisionPolicy.RoundImporte(line.UnitPriceBase * line.QuantityOrdered);
            }
            if (string.IsNullOrWhiteSpace(po.CurrencyCode)) po.CurrencyCode = "CLP";
            PurchaseOrderCommittedCostEngine.RecalculateLinesAndTotals(po, baseCurrency, po.ExchangeRate);
            ThrowIf(PurchaseOrderValidation.Validate(po));
            await _repo.SaveAsync(po, ct);
            await Audit(po, "GenerateFromAward", actor, null, po.Status.ToString(), ct);
            orders.Add(po);
        }
        return orders;
    }

    public async Task<PurchaseOrder> SubmitAsync(PurchaseOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderSubmit, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        if (po.Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("InvalidPoStatus");
        ThrowIf(PurchaseOrderValidation.Validate(po));
        po.Status = PurchaseOrderStatus.PendingApproval;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "Submit", actor, PurchaseOrderStatus.Draft.ToString(), po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> ApproveAsync(PurchaseOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderApprove, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        if (_sod.PoCreatorCannotApprove && string.Equals(po.CreatedBy, actor, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("PermissionDenied:SoD");
        if (po.Status != PurchaseOrderStatus.PendingApproval) throw new InvalidOperationException("InvalidPoStatus");
        po.Status = PurchaseOrderStatus.Approved;
        po.Approver = actor;
        po.ApprovedUtc = DateTime.UtcNow;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "Approve", actor, PurchaseOrderStatus.PendingApproval.ToString(), po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> RejectAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderReject, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        po.Status = PurchaseOrderStatus.Draft;
        po.CancellationReason = reason;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "Reject", actor, PurchaseOrderStatus.PendingApproval.ToString(), po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> IssueAsync(PurchaseOrderId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderIssue, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        if (po.Status != PurchaseOrderStatus.Approved) throw new InvalidOperationException("InvalidPoStatus");
        po.Status = PurchaseOrderStatus.Issued;
        po.IssuedUtc = DateTime.UtcNow;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "Issue", actor, PurchaseOrderStatus.Approved.ToString(), po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> ConfirmSupplierAsync(
        PurchaseOrderId id, string expectedRowVersion, string actor, string confirmationRef, DateTime? committedDate, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderConfirm, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        if (po.Status != PurchaseOrderStatus.Issued) throw new InvalidOperationException("InvalidPoStatus");
        po.Status = PurchaseOrderStatus.ConfirmedBySupplier;
        po.ConfirmedUtc = DateTime.UtcNow;
        po.SupplierConfirmationRef = confirmationRef ?? string.Empty;
        if (committedDate is not null) po.CommittedDeliveryDate = committedDate;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "ConfirmSupplier", actor, PurchaseOrderStatus.Issued.ToString(), po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> CreateVersionAsync(
        PurchaseOrderId id, string expectedRowVersion, string actor, string changeReason, Action<PurchaseOrder>? mutate = null, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderEdit, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        if (po.Status is PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.Closed or PurchaseOrderStatus.Archived)
            throw new InvalidOperationException("InvalidPoStatus");
        var json = System.Text.Json.JsonSerializer.Serialize(po);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        po.Versions.Add(new PurchaseOrderVersionSnapshot
        {
            VersionId = Guid.NewGuid().ToString("N"),
            VersionNumber = po.Version,
            ChangeReason = changeReason,
            CreatedBy = actor,
            CreatedUtc = DateTime.UtcNow,
            PayloadHash = hash,
            JsonSnapshot = json
        });
        po.Version++;
        mutate?.Invoke(po);
        PurchaseOrderCommittedCostEngine.RecalculateLinesAndTotals(po, "CLP", po.ExchangeRate);
        ThrowIf(PurchaseOrderValidation.Validate(po));
        if (po.Status is PurchaseOrderStatus.Approved or PurchaseOrderStatus.Issued or PurchaseOrderStatus.ConfirmedBySupplier)
            po.Status = PurchaseOrderStatus.Draft;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "CreateVersion", actor, null, po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> CancelAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string reason, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderCancel, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        if (po.Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.Closed)
            throw new InvalidOperationException("InvalidPoStatus");
        po.Status = PurchaseOrderStatus.Cancelled;
        po.CancellationReason = reason;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "Cancel", actor, null, po.Status.ToString(), ct);
        return po;
    }

    public async Task<PurchaseOrder> CloseAsync(PurchaseOrderId id, string expectedRowVersion, string actor, string notes, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderClose, actor);
        var po = await LoadWritable(id, expectedRowVersion, ct);
        po.Status = PurchaseOrderStatus.Closed;
        po.CloseNotes = notes;
        Touch(po, actor);
        await _repo.SaveAsync(po, ct);
        await Audit(po, "Close", actor, null, po.Status.ToString(), ct);
        return po;
    }

    public Task<PurchaseOrder?> GetAsync(PurchaseOrderId id, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderView, "system");
        return _repo.GetByIdAsync(id, ct);
    }

    public Task<List<PurchaseOrder>> ListByProjectAsync(string projectPath, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderView, "system");
        return _repo.GetByProjectAsync(projectPath, ct);
    }

    public async Task<string> ExportCsvAsync(string projectPath, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.PurchaseOrderExport, "system");
        var list = await _repo.GetByProjectAsync(projectPath, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Number,SupplierId,Status,Currency,CommittedOriginal,CommittedBase,Version,Delivery");
        foreach (var po in list)
            sb.AppendLine($"{Escape(po.Number)},{po.SupplierId.Value},{po.Status},{po.CurrencyCode},{po.CommittedCostOriginal},{po.CommittedCostBase},{po.Version},{po.CommittedDeliveryDate:yyyy-MM-dd}");
        return sb.ToString();
    }

    private async Task<PurchaseOrder> LoadWritable(PurchaseOrderId id, string expectedRowVersion, CancellationToken ct)
    {
        var po = await _repo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("PoNotFound");
        if (!string.Equals(po.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        return po;
    }

    private static void Touch(PurchaseOrder po, string actor)
    {
        po.ModifiedBy = actor;
        po.ModifiedUtc = DateTime.UtcNow;
        po.RowVersion = Guid.NewGuid().ToString("N");
    }

    private void Ensure(string permission, string actor)
    {
        if (_permissions.Has(permission)) return;
        throw new UnauthorizedAccessException($"PermissionDenied:{permission}");
    }

    private static void ThrowIf(List<ProcurementValidationIssue> issues)
    {
        if (issues.Any(i => i.Severity == ProcurementValidationSeverity.Error))
            throw new InvalidOperationException(string.Join(";", issues.Select(i => i.Code)));
    }

    private Task Audit(PurchaseOrder po, string action, string actor, string? prev, string? next, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = "PurchaseOrder", EntityId = po.Id.Value, Action = action,
            Actor = actor, UtcTimestamp = DateTime.UtcNow, PreviousState = prev, NewState = next, ProjectPath = po.ProjectPath
        }, ct);

    private static string Escape(string v) => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
