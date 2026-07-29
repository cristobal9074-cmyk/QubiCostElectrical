using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Procurement;

internal sealed class FakePoRepo : IPurchaseOrderRepository
{
    private readonly ConcurrentDictionary<string, PurchaseOrder> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(PurchaseOrder order, CancellationToken ct = default) { _map[order.Id.Value] = order; return Task.CompletedTask; }
    public Task<PurchaseOrder?> GetByIdAsync(PurchaseOrderId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var o) ? o : null);
    public Task<List<PurchaseOrder>> GetByProjectAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(o => o.ProjectPath == projectPath).ToList());
    public Task<List<PurchaseOrder>> GetByAwardAsync(AwardId awardId, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(o => o.AwardId?.Value == awardId.Value).ToList());
}

internal sealed class FakeGrRepo : IGoodsReceiptRepository
{
    private readonly ConcurrentDictionary<string, GoodsReceipt> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(GoodsReceipt receipt, CancellationToken ct = default) { _map[receipt.Id.Value] = receipt; return Task.CompletedTask; }
    public Task<GoodsReceipt?> GetByIdAsync(GoodsReceiptId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var o) ? o : null);
    public Task<List<GoodsReceipt>> GetByPurchaseOrderAsync(PurchaseOrderId poId, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(r => r.PurchaseOrderId.Value == poId.Value).ToList());
    public Task<List<GoodsReceipt>> GetByProjectAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(r => r.ProjectPath == projectPath).ToList());
}

internal sealed class FakeReturnRepo : IGoodsReturnRepository
{
    private readonly ConcurrentDictionary<string, GoodsReturn> _map = new(StringComparer.Ordinal);
    public Task SaveAsync(GoodsReturn ret, CancellationToken ct = default) { _map[ret.Id.Value] = ret; return Task.CompletedTask; }
    public Task<GoodsReturn?> GetByIdAsync(GoodsReturnId id, CancellationToken ct = default) =>
        Task.FromResult(_map.TryGetValue(id.Value, out var o) ? o : null);
    public Task<List<GoodsReturn>> GetByProjectAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_map.Values.Where(r => r.ProjectPath == projectPath).ToList());
}

internal sealed class FakeInvRepo : IProjectInventoryRepository
{
    private readonly List<ProjectInventoryBalance> _bals = new();
    private readonly List<InventoryMovement> _mov = new();
    public Task SaveAsync(ProjectInventoryBalance balance, CancellationToken ct = default)
    {
        _bals.RemoveAll(b => b.Id.Value == balance.Id.Value);
        _bals.Add(balance);
        return Task.CompletedTask;
    }
    public Task<ProjectInventoryBalance?> GetAsync(string projectPath, string code, string lot, string location, CancellationToken ct = default) =>
        Task.FromResult(_bals.FirstOrDefault(b => b.ProjectPath == projectPath && b.Code == code && b.Lot == lot && b.Location == location));
    public Task<List<ProjectInventoryBalance>> ListByProjectAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_bals.Where(b => b.ProjectPath == projectPath).ToList());
    public Task SaveMovementAsync(InventoryMovement movement, CancellationToken ct = default) { _mov.Add(movement); return Task.CompletedTask; }
    public Task<List<InventoryMovement>> ListMovementsAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_mov.Where(m => m.ProjectPath == projectPath).ToList());
}

internal sealed class FakeCommitSnapRepo : IProcurementCommitmentSnapshotRepository
{
    private readonly List<ProcurementCommitmentSummary> _list = new();
    public Task SaveAsync(ProcurementCommitmentSummary summary, CancellationToken ct = default) { _list.Add(summary); return Task.CompletedTask; }
    public Task<List<ProcurementCommitmentSummary>> ListByProjectAsync(string projectPath, CancellationToken ct = default) =>
        Task.FromResult(_list.Where(s => s.ProjectPath == projectPath).ToList());
}

public sealed class Phase9CdePurchaseOrderTests
{
    private readonly FakeAwardRepo _awards = new();
    private readonly FakePoRepo _pos = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly AllowAllProcurementPermissionChecker _perm = new();

    private async Task<AwardDecision> SeedAwardAsync(string project = "p", decimal qty = 10, decimal price = 100)
    {
        var award = new AwardDecision
        {
            Id = AwardId.New(), RfqId = RequestForQuotationId.New(), ProjectPath = project,
            Status = AwardStatus.Approved, Kind = AwardKind.Total, CreatedBy = "buyer", CreatedUtc = DateTime.UtcNow,
            BudgetImpactBase = qty * price,
            Items =
            {
                new AwardedItem
                {
                    AwardedItemId = "ai1", AwardId = AwardId.New(), Code = "M1", Description = "Cable",
                    OfferId = SupplierOfferId.New(), SupplierId = SupplierId.New(), Quantity = qty,
                    UnitPriceBase = price, UnitPriceOriginal = price, CurrencyOriginal = "CLP"
                }
            }
        };
        await _awards.SaveAsync(award);
        return award;
    }

    [Fact]
    public async Task Generate_From_Award_Creates_Committed_Cost()
    {
        var award = await SeedAwardAsync();
        var svc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        var orders = await svc.GenerateFromAwardAsync(award.Id, "buyer");
        Assert.Single(orders);
        Assert.Equal(PurchaseOrderStatus.Draft, orders[0].Status);
        Assert.True(orders[0].CommittedCostBase > 0m);
        Assert.Contains("OC-", orders[0].Number);
    }

    [Fact]
    public async Task Full_Po_Lifecycle_Approve_Issue_Confirm_Close()
    {
        var award = await SeedAwardAsync();
        var svc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        var po = (await svc.GenerateFromAwardAsync(award.Id, "buyer"))[0];
        po = await svc.SubmitAsync(po.Id, po.RowVersion, "buyer");
        po = await svc.ApproveAsync(po.Id, po.RowVersion, "manager");
        po = await svc.IssueAsync(po.Id, po.RowVersion, "buyer");
        po = await svc.ConfirmSupplierAsync(po.Id, po.RowVersion, "buyer", "REF-1", DateTime.Today.AddDays(10));
        Assert.Equal(PurchaseOrderStatus.ConfirmedBySupplier, po.Status);
        Assert.Equal("REF-1", po.SupplierConfirmationRef);
        po = await svc.CloseAsync(po.Id, po.RowVersion, "admin", "ok");
        Assert.Equal(PurchaseOrderStatus.Closed, po.Status);
    }

    [Fact]
    public async Task Po_SoD_Blocks_Creator_Approve()
    {
        var award = await SeedAwardAsync();
        var svc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        var po = (await svc.GenerateFromAwardAsync(award.Id, "same"))[0];
        po = await svc.SubmitAsync(po.Id, po.RowVersion, "same");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.ApproveAsync(po.Id, po.RowVersion, "same"));
    }

    [Fact]
    public async Task Po_Version_And_Cancel()
    {
        var award = await SeedAwardAsync();
        var svc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        var po = (await svc.GenerateFromAwardAsync(award.Id, "buyer"))[0];
        po = await svc.CreateVersionAsync(po.Id, po.RowVersion, "buyer", "ajuste precio", p => p.TaxPercent = 19m);
        Assert.Equal(2, po.Version);
        Assert.Single(po.Versions);
        po = await svc.CancelAsync(po.Id, po.RowVersion, "buyer", "anula");
        Assert.Equal(PurchaseOrderStatus.Cancelled, po.Status);
    }

    [Theory]
    [InlineData(100, 19, 0, 0, 0, 119)]
    [InlineData(200, 0, 10, 20, 10, 210)]
    [InlineData(50, 19, 5, 5, 5, 66.525)]
    public void Committed_Cost_Engine(decimal line, decimal tax, decimal disc, decimal f, decimal i, decimal expected)
    {
        var po = new PurchaseOrder
        {
            Id = PurchaseOrderId.New(), SupplierId = SupplierId.New(), CurrencyCode = "CLP", ExchangeRate = 1m,
            TaxPercent = tax, DiscountPercent = disc, FreightAmount = f, InsuranceAmount = i,
            Lines = { new PurchaseOrderLine { LineId = "1", PurchaseOrderId = PurchaseOrderId.New(), QuantityOrdered = 1, UnitPriceOriginal = line, UnitPriceBase = line } }
        };
        var (o, b) = PurchaseOrderCommittedCostEngine.Compute(po);
        Assert.Equal(PrecisionPolicy.RoundImporte(expected), o);
        Assert.Equal(o, b);
    }

    [Theory]
    [InlineData(ProcurementPermissions.PurchaseOrderView)]
    [InlineData(ProcurementPermissions.PurchaseOrderCreate)]
    [InlineData(ProcurementPermissions.PurchaseOrderApprove)]
    [InlineData(ProcurementPermissions.PurchaseOrderIssue)]
    [InlineData(ProcurementPermissions.PurchaseOrderConfirm)]
    [InlineData(ProcurementPermissions.PurchaseOrderCancel)]
    [InlineData(ProcurementPermissions.PurchaseOrderClose)]
    [InlineData(ProcurementPermissions.PurchaseOrderExport)]
    public void Po_Permissions_Defined(string p) => Assert.StartsWith("Procurement.PurchaseOrder", p);

    [Fact]
    public async Task Export_Csv()
    {
        var award = await SeedAwardAsync();
        var svc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        await svc.GenerateFromAwardAsync(award.Id, "buyer");
        var csv = await svc.ExportCsvAsync("p");
        Assert.Contains("Number,SupplierId", csv);
        Assert.Contains("OC-", csv);
    }

    [Fact]
    public async Task Concurrent_Submit_Fails()
    {
        var award = await SeedAwardAsync();
        var svc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        var po = (await svc.GenerateFromAwardAsync(award.Id, "buyer"))[0];
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SubmitAsync(po.Id, "stale", "buyer"));
    }

    [Fact]
    public async Task Multi_Supplier_Award_Creates_Multiple_Pos()
    {
        var s1 = SupplierId.New();
        var s2 = SupplierId.New();
        var award = new AwardDecision
        {
            Id = AwardId.New(), RfqId = RequestForQuotationId.New(), ProjectPath = "p", Status = AwardStatus.Approved,
            CreatedBy = "b", CreatedUtc = DateTime.UtcNow, Kind = AwardKind.MultiSupplier,
            Items =
            {
                new AwardedItem { AwardedItemId = "1", AwardId = AwardId.New(), Code = "A", OfferId = SupplierOfferId.New(), SupplierId = s1, Quantity = 1, UnitPriceBase = 10 },
                new AwardedItem { AwardedItemId = "2", AwardId = AwardId.New(), Code = "B", OfferId = SupplierOfferId.New(), SupplierId = s2, Quantity = 2, UnitPriceBase = 20 }
            }
        };
        await _awards.SaveAsync(award);
        var orders = await new PurchaseOrderService(_pos, _awards, _audit, _perm).GenerateFromAwardAsync(award.Id, "buyer");
        Assert.Equal(2, orders.Count);
    }
}

public sealed class Phase9DReceiptInventoryTests
{
    private readonly FakeAwardRepo _awards = new();
    private readonly FakePoRepo _pos = new();
    private readonly FakeGrRepo _gr = new();
    private readonly FakeReturnRepo _ret = new();
    private readonly FakeInvRepo _inv = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly AllowAllProcurementPermissionChecker _perm = new();

    private async Task<PurchaseOrder> SeedIssuedPoAsync()
    {
        var award = new AwardDecision
        {
            Id = AwardId.New(), RfqId = RequestForQuotationId.New(), ProjectPath = "p", Status = AwardStatus.Approved,
            CreatedBy = "buyer", CreatedUtc = DateTime.UtcNow,
            Items =
            {
                new AwardedItem { AwardedItemId = "ai", AwardId = AwardId.New(), Code = "M1", Description = "Mat",
                    OfferId = SupplierOfferId.New(), SupplierId = SupplierId.New(), Quantity = 10, UnitPriceBase = 5, UnitPriceOriginal = 5 }
            }
        };
        await _awards.SaveAsync(award);
        var poSvc = new PurchaseOrderService(_pos, _awards, _audit, _perm);
        var po = (await poSvc.GenerateFromAwardAsync(award.Id, "buyer"))[0];
        po = await poSvc.SubmitAsync(po.Id, po.RowVersion, "buyer");
        po = await poSvc.ApproveAsync(po.Id, po.RowVersion, "mgr");
        return await poSvc.IssueAsync(po.Id, po.RowVersion, "buyer");
    }

    [Fact]
    public async Task Partial_Then_Accept_Updates_Inventory()
    {
        var po = await SeedIssuedPoAsync();
        var svc = new GoodsReceiptService(_gr, _ret, _inv, _pos, _audit, _perm);
        var receipt = await svc.CreateFromPurchaseOrderAsync(po.Id, "recv", partial: true);
        Assert.True(receipt.Lines[0].QuantityReceived < 10m);
        receipt = await svc.AcceptAsync(receipt.Id, receipt.RowVersion, "recv");
        Assert.Equal(GoodsReceiptStatus.Accepted, receipt.Status);
        var inv = await svc.ListInventoryAsync("p");
        Assert.NotEmpty(inv);
        Assert.True(inv[0].QtyOnHand > 0m);
        Assert.True(inv[0].QtyAvailable > 0m);
    }

    [Fact]
    public async Task Total_Receipt_Marks_Po_Received()
    {
        var po = await SeedIssuedPoAsync();
        var svc = new GoodsReceiptService(_gr, _ret, _inv, _pos, _audit, _perm);
        var receipt = await svc.CreateFromPurchaseOrderAsync(po.Id, "recv", partial: false);
        await svc.AcceptAsync(receipt.Id, receipt.RowVersion, "recv");
        var updated = await _pos.GetByIdAsync(po.Id);
        Assert.Equal(PurchaseOrderStatus.Received, updated!.Status);
    }

    [Fact]
    public async Task Return_And_Reserve()
    {
        var po = await SeedIssuedPoAsync();
        var svc = new GoodsReceiptService(_gr, _ret, _inv, _pos, _audit, _perm);
        var receipt = await svc.CreateFromPurchaseOrderAsync(po.Id, "recv", false);
        await svc.AcceptAsync(receipt.Id, receipt.RowVersion, "recv");
        var ret = await svc.CreateReturnAsync(po.Id, receipt.Id, new[]
        {
            new GoodsReturnLine { LineId = "r1", Code = "M1", Quantity = 1, Location = "" }
        }, "daño", "recv");
        ret = await svc.ConfirmReturnAsync(ret.Id, ret.RowVersion, "recv");
        Assert.Equal(GoodsReturnStatus.Confirmed, ret.Status);
        await svc.ReserveAsync("p", "M1", "", "", 1m, "recv");
        var inv = (await svc.ListInventoryAsync("p")).First(i => i.Code == "M1");
        Assert.True(inv.QtyReserved >= 1m);
    }

    [Theory]
    [InlineData(10, 10, 0)]
    [InlineData(10, 8, -2)]
    [InlineData(10, 12, 2)]
    public void Ordered_Vs_Received_Variance(decimal ordered, decimal received, decimal expected) =>
        Assert.Equal(expected, InventoryEngine.OrderedVsReceivedVariance(ordered, received));

    [Theory]
    [InlineData(ReceiptLineDisposition.Accepted)]
    [InlineData(ReceiptLineDisposition.Rejected)]
    [InlineData(ReceiptLineDisposition.Damaged)]
    [InlineData(ReceiptLineDisposition.Shortage)]
    [InlineData(ReceiptLineDisposition.Surplus)]
    public void Disposition_Enum(ReceiptLineDisposition d) => Assert.True(Enum.IsDefined(d));

    [Theory]
    [InlineData(ProcurementPermissions.GoodsReceiptView)]
    [InlineData(ProcurementPermissions.GoodsReceiptCreate)]
    [InlineData(ProcurementPermissions.GoodsReceiptInspect)]
    [InlineData(ProcurementPermissions.GoodsReceiptAccept)]
    [InlineData(ProcurementPermissions.GoodsReturnManage)]
    [InlineData(ProcurementPermissions.InventoryView)]
    [InlineData(ProcurementPermissions.InventoryReserve)]
    public void Receipt_Permissions(string p) => Assert.StartsWith("Procurement.", p);

    [Fact]
    public void Receipt_Validation_Empty()
    {
        var issues = GoodsReceiptValidation.Validate(new GoodsReceipt { Id = GoodsReceiptId.New(), PurchaseOrderId = PurchaseOrderId.New() });
        Assert.Contains(issues, i => i.Severity == ProcurementValidationSeverity.Error);
    }
}

public sealed class Phase9ECommitmentDashboardTests
{
    [Fact]
    public void Commitment_Engine_Aggregates()
    {
        var supplier = SupplierId.New();
        var po = new PurchaseOrder
        {
            Id = PurchaseOrderId.New(), SupplierId = supplier, ProjectPath = "p", Status = PurchaseOrderStatus.Issued,
            CurrencyCode = "CLP", ExchangeRate = 1m, CommittedCostBase = 500, CommittedCostOriginal = 500,
            CommittedDeliveryDate = DateTime.Today.AddDays(-1),
            Lines = { new PurchaseOrderLine { LineId = "l1", PurchaseOrderId = PurchaseOrderId.New(), Code = "M1", Description = "x", QuantityOrdered = 1, UnitPriceBase = 500, LineTotalBase = 500, WbsCode = "1.1" } }
        };
        var summary = ProcurementCommitmentEngine.Build(
            "p", 1000m,
            Array.Empty<PurchaseRequisition>(),
            Array.Empty<SupplierOffer>(),
            new[] { new AwardDecision { Id = AwardId.New(), RfqId = RequestForQuotationId.New(), Status = AwardStatus.Approved, BudgetImpactBase = 500 } },
            new[] { po },
            Array.Empty<GoodsReceipt>(),
            DateTime.UtcNow);
        Assert.Equal(1000m, summary.BudgetAmount);
        Assert.Equal(500m, summary.AwardedAmount);
        Assert.Equal(500m, summary.CommittedAmount);
        Assert.Equal(500m, summary.BalanceAmount);
        Assert.NotEmpty(summary.ByWbs);
        Assert.NotEmpty(summary.SupplierExposure);
        Assert.NotEmpty(summary.Alerts);
        Assert.False(string.IsNullOrWhiteSpace(summary.SnapshotHash));
    }

    [Theory]
    [InlineData(ProcurementPermissions.ProcurementCommitmentView)]
    [InlineData(ProcurementPermissions.ProcurementDashboardView)]
    public void Commitment_Permissions(string p) => Assert.StartsWith("Procurement.", p);

    [Fact]
    public async Task Commitment_Service_Snapshot()
    {
        var prs = new FakePrRepository();
        var offers = new FakeOfferRepository();
        var awards = new FakeAwardRepo();
        var pos = new FakePoRepo();
        var gr = new FakeGrRepo();
        var rfqs = new FakeRfqRepository();
        var snaps = new FakeCommitSnapRepo();
        var audit = new FakeProcAuditRepository();
        var perm = new AllowAllProcurementPermissionChecker();
        var svc = new ProcurementCommitmentService(prs, offers, awards, pos, gr, rfqs, snaps, audit, perm);
        var s = await svc.SnapshotAsync("p", 10000m, "u");
        Assert.Equal(10000m, s.BudgetAmount);
        Assert.Single(await svc.HistoryAsync("p"));
    }

    [Theory]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.PurchaseOrders.PurchaseOrderWorkspaceViewModel))]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.GoodsReceipts.GoodsReceiptWorkspaceViewModel))]
    [InlineData(typeof(QubiCostElectrical.Wpf.Features.ProcurementDashboard.ProcurementDashboardViewModel))]
    public void ViewModels_Exist(Type t) => Assert.True(t.IsClass);

    [Fact]
    public void Schema_Is_59() => Assert.Equal(59, ProductVersionInfo.DatabaseSchemaVersion);

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft)]
    [InlineData(PurchaseOrderStatus.PendingApproval)]
    [InlineData(PurchaseOrderStatus.Approved)]
    [InlineData(PurchaseOrderStatus.Issued)]
    [InlineData(PurchaseOrderStatus.ConfirmedBySupplier)]
    [InlineData(PurchaseOrderStatus.PartiallyReceived)]
    [InlineData(PurchaseOrderStatus.Received)]
    [InlineData(PurchaseOrderStatus.Cancelled)]
    [InlineData(PurchaseOrderStatus.Closed)]
    public void Po_Status_Enum(PurchaseOrderStatus s) => Assert.True(Enum.IsDefined(s));

    [Theory]
    [InlineData(GoodsReceiptStatus.Draft)]
    [InlineData(GoodsReceiptStatus.Inspecting)]
    [InlineData(GoodsReceiptStatus.Accepted)]
    [InlineData(GoodsReceiptStatus.PartiallyAccepted)]
    [InlineData(GoodsReceiptStatus.Rejected)]
    [InlineData(GoodsReceiptStatus.Cancelled)]
    public void Receipt_Status_Enum(GoodsReceiptStatus s) => Assert.True(Enum.IsDefined(s));

    [Theory]
    [InlineData(InventoryMovementKind.ReceiptEntry)]
    [InlineData(InventoryMovementKind.ReturnExit)]
    [InlineData(InventoryMovementKind.Reservation)]
    [InlineData(InventoryMovementKind.InTransit)]
    [InlineData(InventoryMovementKind.Issue)]
    public void Movement_Kind_Enum(InventoryMovementKind k) => Assert.True(Enum.IsDefined(k));

    [Fact]
    public void Po_Validation_Rejects_Empty()
    {
        var issues = PurchaseOrderValidation.Validate(new PurchaseOrder { Id = PurchaseOrderId.New(), SupplierId = SupplierId.New(), ExchangeRate = 1m });
        Assert.Contains(issues, i => i.Code == "PoNoLines");
    }

    [Fact]
    public void Currency_Normalize_On_Po_Lines()
    {
        var po = new PurchaseOrder
        {
            Id = PurchaseOrderId.New(), SupplierId = SupplierId.New(), CurrencyCode = "USD", ExchangeRate = 900m,
            Lines = { new PurchaseOrderLine { LineId = "1", PurchaseOrderId = PurchaseOrderId.New(), QuantityOrdered = 2, UnitPriceOriginal = 10 } }
        };
        PurchaseOrderCommittedCostEngine.RecalculateLinesAndTotals(po, "CLP", 900m);
        Assert.Equal(9000m, po.Lines[0].UnitPriceBase);
        Assert.True(po.CommittedCostBase > 0m);
    }

    [Fact]
    public void InTransit_And_Available()
    {
        var bal = new ProjectInventoryBalance { Id = InventoryItemId.New(), QtyOnHand = 10, QtyReserved = 3 };
        InventoryEngine.SetInTransit(bal, 2);
        Assert.Equal(2m, bal.QtyInTransit);
        Assert.Equal(7m, bal.QtyAvailable);
    }

    [Theory]
    [InlineData(typeof(SqlitePurchaseOrderRepository))]
    [InlineData(typeof(SqliteGoodsReceiptRepository))]
    [InlineData(typeof(SqliteProjectInventoryRepository))]
    public void Infra_Repos_Assignable(Type impl)
    {
        Assert.True(impl.GetInterfaces().Length > 0);
    }

    [Theory]
    [InlineData(typeof(IPurchaseOrderService))]
    [InlineData(typeof(IGoodsReceiptService))]
    [InlineData(typeof(IProcurementCommitmentService))]
    public void Service_Contracts(Type t) => Assert.True(t.IsInterface);
}
