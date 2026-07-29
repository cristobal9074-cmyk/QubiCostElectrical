using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QubiCostElectrical;

public interface IGoodsReceiptRepository
{
    Task SaveAsync(GoodsReceipt receipt, CancellationToken ct = default);
    Task<GoodsReceipt?> GetByIdAsync(GoodsReceiptId id, CancellationToken ct = default);
    Task<List<GoodsReceipt>> GetByPurchaseOrderAsync(PurchaseOrderId poId, CancellationToken ct = default);
    Task<List<GoodsReceipt>> GetByProjectAsync(string projectPath, CancellationToken ct = default);
}

public interface IGoodsReturnRepository
{
    Task SaveAsync(GoodsReturn ret, CancellationToken ct = default);
    Task<GoodsReturn?> GetByIdAsync(GoodsReturnId id, CancellationToken ct = default);
    Task<List<GoodsReturn>> GetByProjectAsync(string projectPath, CancellationToken ct = default);
}

public interface IProjectInventoryRepository
{
    Task SaveAsync(ProjectInventoryBalance balance, CancellationToken ct = default);
    Task<ProjectInventoryBalance?> GetAsync(string projectPath, string code, string lot, string location, CancellationToken ct = default);
    Task<List<ProjectInventoryBalance>> ListByProjectAsync(string projectPath, CancellationToken ct = default);
    Task SaveMovementAsync(InventoryMovement movement, CancellationToken ct = default);
    Task<List<InventoryMovement>> ListMovementsAsync(string projectPath, CancellationToken ct = default);
}

public interface IGoodsReceiptService
{
    Task<GoodsReceipt> CreateFromPurchaseOrderAsync(PurchaseOrderId poId, string actor, bool partial, CancellationToken ct = default);
    Task<GoodsReceipt> InspectAsync(GoodsReceiptId id, string expectedRowVersion, string actor, IReadOnlyList<GoodsReceiptLine> dispositions, CancellationToken ct = default);
    Task<GoodsReceipt> AcceptAsync(GoodsReceiptId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task<GoodsReturn> CreateReturnAsync(PurchaseOrderId poId, GoodsReceiptId? receiptId, IReadOnlyList<GoodsReturnLine> lines, string reason, string actor, CancellationToken ct = default);
    Task<GoodsReturn> ConfirmReturnAsync(GoodsReturnId id, string expectedRowVersion, string actor, CancellationToken ct = default);
    Task ReserveAsync(string projectPath, string code, string lot, string location, decimal qty, string actor, CancellationToken ct = default);
    Task<List<ProjectInventoryBalance>> ListInventoryAsync(string projectPath, CancellationToken ct = default);
    Task<List<GoodsReceipt>> ListReceiptsAsync(string projectPath, CancellationToken ct = default);
}

public sealed class GoodsReceiptService : IGoodsReceiptService
{
    private readonly IGoodsReceiptRepository _receipts;
    private readonly IGoodsReturnRepository _returns;
    private readonly IProjectInventoryRepository _inventory;
    private readonly IPurchaseOrderRepository _orders;
    private readonly IProcurementAuditRepository _audit;
    private readonly IProcurementPermissionChecker _permissions;

    public GoodsReceiptService(
        IGoodsReceiptRepository receipts,
        IGoodsReturnRepository returns,
        IProjectInventoryRepository inventory,
        IPurchaseOrderRepository orders,
        IProcurementAuditRepository audit,
        IProcurementPermissionChecker permissions)
    {
        _receipts = receipts;
        _returns = returns;
        _inventory = inventory;
        _orders = orders;
        _audit = audit;
        _permissions = permissions;
    }

    public async Task<GoodsReceipt> CreateFromPurchaseOrderAsync(PurchaseOrderId poId, string actor, bool partial, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.GoodsReceiptCreate, actor);
        var po = await _orders.GetByIdAsync(poId, ct) ?? throw new InvalidOperationException("PoNotFound");
        if (po.Status is not PurchaseOrderStatus.Issued and not PurchaseOrderStatus.ConfirmedBySupplier and not PurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException("InvalidPoStatusForReceipt");
        var id = GoodsReceiptId.New();
        var receipt = new GoodsReceipt
        {
            Id = id,
            PurchaseOrderId = poId,
            ProjectPath = po.ProjectPath,
            Number = $"GR-{DateTime.UtcNow:yyyyMMdd}-{id.Value[..6].ToUpperInvariant()}",
            Status = GoodsReceiptStatus.Draft,
            ReceiptDateUtc = DateTime.UtcNow,
            ReceivedBy = actor,
            Lines = po.Lines.Select(l =>
            {
                var pending = Math.Max(0m, l.QuantityOrdered - l.QuantityReceived);
                var qty = partial ? PrecisionPolicy.RoundCantidad(pending / 2m) : pending;
                if (qty <= 0m && !partial) qty = 0m;
                return new GoodsReceiptLine
                {
                    LineId = Guid.NewGuid().ToString("N"),
                    ReceiptId = id,
                    PurchaseOrderLineId = l.LineId,
                    Code = l.Code,
                    Description = l.Description,
                    QuantityOrdered = l.QuantityOrdered,
                    QuantityReceived = qty,
                    QuantityAccepted = qty,
                    Disposition = ReceiptLineDisposition.Accepted
                };
            }).Where(l => l.QuantityReceived > 0m).ToList()
        };
        if (receipt.Lines.Count == 0) throw new InvalidOperationException("NothingToReceive");
        ThrowIf(GoodsReceiptValidation.Validate(receipt));
        await _receipts.SaveAsync(receipt, ct);
        await Audit("GoodsReceipt", receipt.Id.Value, "Create", actor, po.ProjectPath, ct);
        return receipt;
    }

    public async Task<GoodsReceipt> InspectAsync(
        GoodsReceiptId id, string expectedRowVersion, string actor, IReadOnlyList<GoodsReceiptLine> dispositions, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.GoodsReceiptInspect, actor);
        var receipt = await LoadReceipt(id, expectedRowVersion, ct);
        foreach (var d in dispositions)
        {
            var line = receipt.Lines.FirstOrDefault(l => l.LineId == d.LineId) ?? throw new InvalidOperationException("ReceiptLineNotFound");
            line.QuantityAccepted = d.QuantityAccepted;
            line.QuantityRejected = d.QuantityRejected;
            line.QuantityDamaged = d.QuantityDamaged;
            line.QuantityShortage = d.QuantityShortage;
            line.QuantitySurplus = d.QuantitySurplus;
            line.Disposition = d.Disposition;
            line.Lot = d.Lot;
            line.Location = d.Location;
            line.InspectionNotes = d.InspectionNotes;
        }
        receipt.Status = GoodsReceiptStatus.Inspecting;
        receipt.RowVersion = Guid.NewGuid().ToString("N");
        ThrowIf(GoodsReceiptValidation.Validate(receipt));
        await _receipts.SaveAsync(receipt, ct);
        await Audit("GoodsReceipt", receipt.Id.Value, "Inspect", actor, receipt.ProjectPath, ct);
        return receipt;
    }

    public async Task<GoodsReceipt> AcceptAsync(GoodsReceiptId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.GoodsReceiptAccept, actor);
        var receipt = await LoadReceipt(id, expectedRowVersion, ct);
        ThrowIf(GoodsReceiptValidation.Validate(receipt));
        var po = await _orders.GetByIdAsync(receipt.PurchaseOrderId, ct) ?? throw new InvalidOperationException("PoNotFound");
        foreach (var line in receipt.Lines)
        {
            var bal = await GetOrCreateBalance(receipt.ProjectPath, line.Code, line.Description, line.Lot, string.IsNullOrWhiteSpace(line.Location) ? receipt.Location : line.Location, ct);
            if (line.QuantityAccepted > 0m)
            {
                InventoryEngine.ApplyReceiptAcceptance(bal, line);
                await _inventory.SaveAsync(bal, ct);
                await _inventory.SaveMovementAsync(new InventoryMovement
                {
                    MovementId = Guid.NewGuid().ToString("N"), ProjectPath = receipt.ProjectPath, Code = line.Code,
                    Kind = InventoryMovementKind.ReceiptEntry, Quantity = line.QuantityAccepted, Lot = line.Lot,
                    Location = bal.Location, ReferenceType = "GoodsReceipt", ReferenceId = receipt.Id.Value,
                    Actor = actor, UtcTimestamp = DateTime.UtcNow
                }, ct);
            }
            var poLine = po.Lines.FirstOrDefault(l => l.LineId == line.PurchaseOrderLineId);
            if (poLine is not null)
                poLine.QuantityReceived = PrecisionPolicy.RoundCantidad(poLine.QuantityReceived + line.QuantityAccepted);
        }

        var anyRejected = receipt.Lines.Any(l => l.QuantityRejected > 0m || l.Disposition == ReceiptLineDisposition.Rejected);
        var allAccepted = receipt.Lines.All(l => l.QuantityAccepted > 0m && l.QuantityRejected == 0m && l.QuantityDamaged == 0m);
        receipt.Status = allAccepted ? GoodsReceiptStatus.Accepted : anyRejected && receipt.Lines.Any(l => l.QuantityAccepted > 0m)
            ? GoodsReceiptStatus.PartiallyAccepted
            : anyRejected ? GoodsReceiptStatus.Rejected : GoodsReceiptStatus.PartiallyAccepted;
        receipt.RowVersion = Guid.NewGuid().ToString("N");
        await _receipts.SaveAsync(receipt, ct);

        var fully = po.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
        po.Status = fully ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;
        po.RowVersion = Guid.NewGuid().ToString("N");
        po.ModifiedUtc = DateTime.UtcNow;
        await _orders.SaveAsync(po, ct);
        await Audit("GoodsReceipt", receipt.Id.Value, "Accept", actor, receipt.ProjectPath, ct);
        return receipt;
    }

    public async Task<GoodsReturn> CreateReturnAsync(
        PurchaseOrderId poId, GoodsReceiptId? receiptId, IReadOnlyList<GoodsReturnLine> lines, string reason, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.GoodsReturnManage, actor);
        var po = await _orders.GetByIdAsync(poId, ct) ?? throw new InvalidOperationException("PoNotFound");
        var ret = new GoodsReturn
        {
            Id = GoodsReturnId.New(),
            PurchaseOrderId = poId,
            ReceiptId = receiptId,
            ProjectPath = po.ProjectPath,
            Status = GoodsReturnStatus.Draft,
            Reason = reason,
            CreatedBy = actor,
            CreatedUtc = DateTime.UtcNow,
            Lines = lines.ToList()
        };
        await _returns.SaveAsync(ret, ct);
        await Audit("GoodsReturn", ret.Id.Value, "Create", actor, po.ProjectPath, ct);
        return ret;
    }

    public async Task<GoodsReturn> ConfirmReturnAsync(GoodsReturnId id, string expectedRowVersion, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.GoodsReturnManage, actor);
        var ret = await _returns.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("ReturnNotFound");
        if (!string.Equals(ret.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        foreach (var line in ret.Lines)
        {
            var bal = await GetOrCreateBalance(ret.ProjectPath, line.Code, line.Code, line.Lot, line.Location, ct);
            InventoryEngine.ApplyReturn(bal, line.Quantity);
            await _inventory.SaveAsync(bal, ct);
            await _inventory.SaveMovementAsync(new InventoryMovement
            {
                MovementId = Guid.NewGuid().ToString("N"), ProjectPath = ret.ProjectPath, Code = line.Code,
                Kind = InventoryMovementKind.ReturnExit, Quantity = line.Quantity, Lot = line.Lot, Location = line.Location,
                ReferenceType = "GoodsReturn", ReferenceId = ret.Id.Value, Actor = actor, UtcTimestamp = DateTime.UtcNow
            }, ct);
        }
        ret.Status = GoodsReturnStatus.Confirmed;
        ret.RowVersion = Guid.NewGuid().ToString("N");
        await _returns.SaveAsync(ret, ct);
        await Audit("GoodsReturn", ret.Id.Value, "Confirm", actor, ret.ProjectPath, ct);
        return ret;
    }

    public async Task ReserveAsync(string projectPath, string code, string lot, string location, decimal qty, string actor, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.InventoryReserve, actor);
        var bal = await GetOrCreateBalance(projectPath, code, code, lot, location, ct);
        InventoryEngine.Reserve(bal, qty);
        await _inventory.SaveAsync(bal, ct);
        await _inventory.SaveMovementAsync(new InventoryMovement
        {
            MovementId = Guid.NewGuid().ToString("N"), ProjectPath = projectPath, Code = code,
            Kind = InventoryMovementKind.Reservation, Quantity = qty, Lot = lot, Location = location,
            Actor = actor, UtcTimestamp = DateTime.UtcNow
        }, ct);
    }

    public Task<List<ProjectInventoryBalance>> ListInventoryAsync(string projectPath, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.InventoryView, "system");
        return _inventory.ListByProjectAsync(projectPath, ct);
    }

    public Task<List<GoodsReceipt>> ListReceiptsAsync(string projectPath, CancellationToken ct = default)
    {
        Ensure(ProcurementPermissions.GoodsReceiptView, "system");
        return _receipts.GetByProjectAsync(projectPath, ct);
    }

    private async Task<ProjectInventoryBalance> GetOrCreateBalance(
        string projectPath, string code, string description, string lot, string location, CancellationToken ct)
    {
        var existing = await _inventory.GetAsync(projectPath, code, lot ?? "", location ?? "", ct);
        if (existing is not null) return existing;
        return new ProjectInventoryBalance
        {
            Id = InventoryItemId.New(),
            ProjectPath = projectPath,
            Code = code,
            Description = description,
            Lot = lot ?? "",
            Location = location ?? "",
            ModifiedUtc = DateTime.UtcNow
        };
    }

    private async Task<GoodsReceipt> LoadReceipt(GoodsReceiptId id, string expectedRowVersion, CancellationToken ct)
    {
        var receipt = await _receipts.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("ReceiptNotFound");
        if (!string.Equals(receipt.RowVersion, expectedRowVersion, StringComparison.Ordinal))
            throw new InvalidOperationException("ConcurrencyConflict");
        return receipt;
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

    private Task Audit(string entity, string id, string action, string actor, string project, CancellationToken ct) =>
        _audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = entity, EntityId = id, Action = action,
            Actor = actor, UtcTimestamp = DateTime.UtcNow, ProjectPath = project
        }, ct);
}
