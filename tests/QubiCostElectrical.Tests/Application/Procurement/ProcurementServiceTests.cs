using System;
using System.Linq;
using System.Threading.Tasks;
using QubiCostElectrical.Tests.Application.Procurement;
using Xunit;

namespace QubiCostElectrical.Tests.Application.Procurement;

public sealed class SupplierServiceTests
{
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeContactRepository _contacts = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly SupplierService _svc;

    public SupplierServiceTests() =>
        _svc = new SupplierService(_suppliers, _contacts, _audit, new AllowAllProcurementPermissionChecker());

    [Fact]
    public async Task Create_Edit_Search_Disable()
    {
        var created = await _svc.CreateAsync(new Supplier { Code = "S1", LegalName = "Acme", Status = SupplierStatus.Active }, "u1");
        Assert.Equal("S1", created.Code);
        created.TradeName = "Acme Trade";
        var updated = await _svc.UpdateAsync(created, created.RowVersion, "u1");
        Assert.Equal("Acme Trade", updated.TradeName);
        Assert.Single(await _svc.SearchAsync("Acme", null, 0, 10));
        await _svc.DisableAsync(created.Id, "u1");
        Assert.Equal(SupplierStatus.Inactive, (await _svc.GetAsync(created.Id))!.Status);
        Assert.Contains(_audit.Events, e => e.Action == "Create");
        Assert.Contains(_audit.Events, e => e.Action == "Disable");
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        await _svc.CreateAsync(new Supplier { Code = "DUP", LegalName = "A" }, "u");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.CreateAsync(new Supplier { Code = "dup", LegalName = "B" }, "u"));
    }

    [Fact]
    public async Task Contact_Primary_And_Deactivate()
    {
        var s = await _svc.CreateAsync(new Supplier { Code = "C1", LegalName = "C" }, "u");
        var c1 = await _svc.UpsertContactAsync(new SupplierContact
        {
            ContactId = "c1", SupplierId = s.Id, Name = "Ana", Email = "ana@x.com", IsPrimary = true, IsActive = true
        }, "u");
        Assert.True(c1.IsPrimary);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.UpsertContactAsync(new SupplierContact
        {
            ContactId = "c2", SupplierId = s.Id, Name = "Bob", Email = "bob@x.com", IsPrimary = true, IsActive = true
        }, "u"));
        await _svc.DeactivateContactAsync("c1", "u");
        Assert.False((await _svc.ListContactsAsync(s.Id)).Single().IsActive);
    }

    [Fact]
    public async Task Contact_SupplierMissing()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.UpsertContactAsync(new SupplierContact
        {
            ContactId = "x", SupplierId = SupplierId.New(), Name = "N", Email = "n@x.com"
        }, "u"));
    }

    [Fact]
    public async Task Concurrency_Conflict()
    {
        var s = await _svc.CreateAsync(new Supplier { Code = "CC", LegalName = "C" }, "u");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _svc.UpdateAsync(s, "stale", "u"));
    }

    [Fact]
    public async Task Pagination_SkipTake()
    {
        for (var i = 0; i < 5; i++)
            await _svc.CreateAsync(new Supplier { Code = $"P{i}", LegalName = $"L{i}" }, "u");
        Assert.Equal(2, (await _svc.SearchAsync(null, null, 0, 2)).Count);
        Assert.Equal(2, (await _svc.SearchAsync(null, null, 2, 2)).Count);
    }

    [Fact]
    public async Task Inactive_Supplier_Still_Retrievable()
    {
        var s = await _svc.CreateAsync(new Supplier { Code = "IN", LegalName = "In" }, "u");
        await _svc.DisableAsync(s.Id, "u");
        Assert.NotNull(await _svc.GetAsync(s.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyCode_Fails(string code)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.CreateAsync(new Supplier { Code = code, LegalName = "X" }, "u"));
    }
}

public sealed class PurchaseRequisitionServiceTests
{
    private readonly FakePrRepository _repo = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly PurchaseRequisitionService _svc;

    public PurchaseRequisitionServiceTests() =>
        _svc = new PurchaseRequisitionService(_repo, _audit, new AllowAllProcurementPermissionChecker());

    [Fact]
    public async Task Create_AddItem_Submit_Approve_WithOtherUser()
    {
        var pr = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10) }, "creator");
        pr = await _svc.AddItemAsync(pr.Id, NewItem(pr, "M1", 2), pr.RowVersion, "creator");
        pr = await _svc.SubmitAsync(pr.Id, pr.RowVersion, "creator");
        Assert.Equal(PurchaseRequisitionStatus.PendingReview, pr.Status);
        pr = await _svc.ApproveAsync(pr.Id, pr.RowVersion, "approver", "ok");
        Assert.Equal(PurchaseRequisitionStatus.Approved, pr.Status);
        Assert.Equal("approver", pr.Approver);
    }

    [Fact]
    public async Task Creator_Cannot_Approve_SoD()
    {
        var pr = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10) }, "same");
        pr = await _svc.AddItemAsync(pr.Id, NewItem(pr, "M1", 1), pr.RowVersion, "same");
        pr = await _svc.SubmitAsync(pr.Id, pr.RowVersion, "same");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _svc.ApproveAsync(pr.Id, pr.RowVersion, "same", null));
    }

    [Fact]
    public async Task Reject_And_Cancel()
    {
        var pr = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10) }, "a");
        pr = await _svc.AddItemAsync(pr.Id, NewItem(pr, "M1", 1), pr.RowVersion, "a");
        pr = await _svc.SubmitAsync(pr.Id, pr.RowVersion, "a");
        pr = await _svc.RejectAsync(pr.Id, pr.RowVersion, "b", "faltan datos");
        Assert.Equal(PurchaseRequisitionStatus.Rejected, pr.Status);
        var pr2 = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10) }, "a");
        pr2 = await _svc.CancelAsync(pr2.Id, pr2.RowVersion, "a", "ya no");
        Assert.Equal(PurchaseRequisitionStatus.Cancelled, pr2.Status);
    }

    [Fact]
    public async Task Invalid_Quantity_And_Duplicate_Item()
    {
        var pr = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10) }, "a");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.AddItemAsync(pr.Id, NewItem(pr, "M1", 0), pr.RowVersion, "a"));
        pr = await _svc.AddItemAsync(pr.Id, NewItem(pr, "M1", 1), pr.RowVersion, "a");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.AddItemAsync(pr.Id, NewItem(pr, "M1", 2), pr.RowVersion, "a"));
    }

    [Fact]
    public async Task Consolidate_And_Generate_Budget_Schedule()
    {
        var pr = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(20) }, "a");
        pr = await _svc.AddItemAsync(pr.Id, NewItem(pr, "X", 1), pr.RowVersion, "a");
        var cons = await _svc.ConsolidateAsync(pr.Id, "a");
        Assert.NotEmpty(cons.Lines);

        var previewBudget = _svc.PreviewFromBudget(new[]
        {
            new NeedGenerationPreviewItem { Code = "B1", Description = "b", Quantity = 3, RequiredDate = DateTime.Today.AddDays(15), OriginId = "b", Selected = true }
        });
        Assert.Single(previewBudget);
        var genB = await _svc.ConfirmGenerationAsync("p", previewBudget, "a", NeedOriginKind.Budget);
        Assert.Equal(NeedOriginKind.Budget, genB.PrimaryOrigin);

        var previewSched = _svc.PreviewFromSchedule(new[]
        {
            new NeedGenerationPreviewItem { Code = "A1", Description = "a", Quantity = 2, RequiredDate = DateTime.Today.AddDays(40), OriginId = "act", Selected = true }
        }, new ProcurementNeedDatePolicy { DefaultLeadTimeDays = 10, SafetyMarginDays = 5 });
        Assert.True(previewSched[0].SuggestedRequestDate < previewSched[0].RequiredDate);
        var genS = await _svc.ConfirmGenerationAsync("p", previewSched, "a", NeedOriginKind.Schedule);
        Assert.Equal(NeedOriginKind.Schedule, genS.PrimaryOrigin);
        Assert.Contains(_audit.Events, e => e.Action == "Generate");
    }

    [Fact]
    public async Task Search_And_Get()
    {
        var pr = await _svc.CreateAsync(new PurchaseRequisition { ProjectPath = "proj", RequiredDate = DateTime.Today.AddDays(5) }, "a");
        Assert.NotNull(await _svc.GetAsync(pr.Id));
        Assert.Single(await _svc.SearchAsync("proj", PurchaseRequisitionStatus.Draft, 0, 10));
    }

    [Fact]
    public void PreviewFromBudget_Dedupes()
    {
        var list = _svc.PreviewFromBudget(new[]
        {
            new NeedGenerationPreviewItem { Code = "C", Unit = "UN", BudgetItemId = "1", Quantity = 1, RequiredDate = DateTime.Today.AddDays(5) },
            new NeedGenerationPreviewItem { Code = "C", Unit = "UN", BudgetItemId = "1", Quantity = 4, RequiredDate = DateTime.Today.AddDays(8) }
        });
        Assert.Single(list);
        Assert.Equal(5m, list[0].Quantity);
    }

    private static PurchaseRequisitionItem NewItem(PurchaseRequisition pr, string code, decimal qty) => new()
    {
        ItemId = Guid.NewGuid().ToString("N"),
        RequisitionId = pr.Id,
        Code = code,
        Description = code,
        RequiredQuantity = qty,
        RequiredDate = pr.RequiredDate,
        Unit = "UN"
    };
}

public sealed class RfqServiceTests
{
    private readonly FakeSupplierRepository _suppliers = new();
    private readonly FakeContactRepository _contacts = new();
    private readonly FakePrRepository _prs = new();
    private readonly FakeRfqRepository _rfqs = new();
    private readonly FakeQrRepository _qrs = new();
    private readonly FakeProcAuditRepository _audit = new();
    private readonly SupplierService _supplierSvc;
    private readonly PurchaseRequisitionService _prSvc;
    private readonly RfqService _rfqSvc;

    public RfqServiceTests()
    {
        var perm = new AllowAllProcurementPermissionChecker();
        _supplierSvc = new SupplierService(_suppliers, _contacts, _audit, perm);
        _prSvc = new PurchaseRequisitionService(_prs, _audit, perm);
        _rfqSvc = new RfqService(_rfqs, _prs, _suppliers, _contacts, _qrs, _audit, perm);
    }

    private async Task<PurchaseRequisition> ApprovedPr()
    {
        var pr = await _prSvc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(20) }, "req");
        pr = await _prSvc.AddItemAsync(pr.Id, new PurchaseRequisitionItem
        {
            ItemId = Guid.NewGuid().ToString("N"), RequisitionId = pr.Id, Code = "M", Description = "M",
            RequiredQuantity = 5, RequiredDate = pr.RequiredDate, Unit = "UN"
        }, pr.RowVersion, "req");
        pr = await _prSvc.SubmitAsync(pr.Id, pr.RowVersion, "req");
        return await _prSvc.ApproveAsync(pr.Id, pr.RowVersion, "apr", null);
    }

    [Fact]
    public async Task Full_Rfq_Flow()
    {
        var pr = await ApprovedPr();
        var supplier = await _supplierSvc.CreateAsync(new Supplier { Code = "SUP", LegalName = "Prov", Status = SupplierStatus.Active }, "u");
        var rfq = await _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "RFQ test", "buyer");
        Assert.NotEmpty(rfq.Items);
        await _rfqSvc.InviteSupplierAsync(rfq.Id, supplier.Id, null, rfq.RowVersion, "buyer");
        rfq = (await _rfqSvc.GetAsync(rfq.Id))!;
        rfq = await _rfqSvc.ApproveAsync(rfq.Id, rfq.RowVersion, "mgr");
        rfq = await _rfqSvc.IssueAsync(rfq.Id, rfq.RowVersion, "buyer");
        Assert.Equal(RfqStatus.Open, rfq.Status);
        var resp = await _rfqSvc.RegisterResponseAsync(new QuotationResponse
        {
            RfqId = rfq.Id, SupplierId = supplier.Id, CurrencyCode = "CLP", DeliveryLeadDays = 7
        }, "buyer");
        Assert.Equal(QuotationResponseStatus.Received, resp.Status);
        Assert.NotEmpty(resp.IntegrityHash);
    }

    [Fact]
    public async Task Issue_Without_Suppliers_Fails()
    {
        var pr = await ApprovedPr();
        var rfq = await _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "t", "u");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _rfqSvc.IssueAsync(rfq.Id, rfq.RowVersion, "u"));
    }

    [Fact]
    public async Task Inactive_Supplier_Not_Eligible()
    {
        var pr = await ApprovedPr();
        var supplier = await _supplierSvc.CreateAsync(new Supplier { Code = "X", LegalName = "X", Status = SupplierStatus.Active }, "u");
        await _supplierSvc.DisableAsync(supplier.Id, "u");
        var rfq = await _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "t", "u");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _rfqSvc.InviteSupplierAsync(rfq.Id, supplier.Id, null, rfq.RowVersion, "u"));
    }

    [Fact]
    public async Task Duplicate_Invitation_Fails()
    {
        var pr = await ApprovedPr();
        var supplier = await _supplierSvc.CreateAsync(new Supplier { Code = "D", LegalName = "D", Status = SupplierStatus.Active }, "u");
        var rfq = await _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "t", "u");
        await _rfqSvc.InviteSupplierAsync(rfq.Id, supplier.Id, null, rfq.RowVersion, "u");
        rfq = (await _rfqSvc.GetAsync(rfq.Id))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _rfqSvc.InviteSupplierAsync(rfq.Id, supplier.Id, null, rfq.RowVersion, "u"));
    }

    [Fact]
    public async Task Unapproved_Pr_Cannot_Create_Rfq()
    {
        var pr = await _prSvc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10) }, "a");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "t", "u"));
    }

    [Fact]
    public async Task Cancel_Rfq()
    {
        var pr = await ApprovedPr();
        var rfq = await _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "t", "u");
        await _rfqSvc.CancelAsync(rfq.Id, rfq.RowVersion, "u", "stop");
        Assert.Equal(RfqStatus.Cancelled, (await _rfqSvc.GetAsync(rfq.Id))!.Status);
    }

    [Fact]
    public async Task Inactive_Contact_Rejected()
    {
        var pr = await ApprovedPr();
        var supplier = await _supplierSvc.CreateAsync(new Supplier { Code = "CT", LegalName = "CT", Status = SupplierStatus.Active }, "u");
        var contact = await _supplierSvc.UpsertContactAsync(new SupplierContact
        {
            ContactId = "cid", SupplierId = supplier.Id, Name = "N", Email = "n@x.com", IsActive = true
        }, "u");
        await _supplierSvc.DeactivateContactAsync(contact.ContactId, "u");
        var rfq = await _rfqSvc.CreateFromRequisitionsAsync("p", new[] { pr.Id }, "t", "u");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _rfqSvc.InviteSupplierAsync(rfq.Id, supplier.Id, contact.ContactId, rfq.RowVersion, "u"));
    }
}

public sealed class ProcurementPermissionAndSoDTests
{
    [Fact]
    public async Task DenyAll_Blocks_Create()
    {
        var svc = new SupplierService(new FakeSupplierRepository(), new FakeContactRepository(), new FakeProcAuditRepository(), new DenyAllProcurementPermissionChecker());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(new Supplier { Code = "A", LegalName = "A" }, "u"));
    }

    [Fact]
    public async Task HashSet_Allows_Only_Granted()
    {
        var checker = new HashSetProcurementPermissionChecker(new[] { ProcurementPermissions.SupplierView });
        var svc = new SupplierService(new FakeSupplierRepository(), new FakeContactRepository(), new FakeProcAuditRepository(), checker);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(new Supplier { Code = "A", LegalName = "A" }, "u"));
        Assert.Empty(await svc.SearchAsync(null, null, 0, 10));
    }

    [Fact]
    public void SoD_Policy_Default_True() =>
        Assert.True(new ProcurementSoDPolicy().CreatorCannotApprove);

    [Fact]
    public async Task Direct_Call_Protected_By_Permission()
    {
        var audit = new FakeProcAuditRepository();
        var svc = new PurchaseRequisitionService(new FakePrRepository(), audit, new DenyAllProcurementPermissionChecker());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateAsync(new PurchaseRequisition { ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(1) }, "u"));
    }
}

public sealed class ProcurementExportHelperTests
{
    [Fact]
    public void Export_Supplier_And_Lists()
    {
        var s = new Supplier { Id = SupplierId.New(), Code = "S", LegalName = "L", Status = SupplierStatus.Active };
        Assert.Contains("Codigo", ProcurementExportHelper.SupplierToCsv(s));
        Assert.Contains("S", ProcurementExportHelper.SuppliersToCsv(new[] { s }));
    }

    [Fact]
    public void Export_Requisition_And_Rfq()
    {
        var pr = new PurchaseRequisition
        {
            Id = PurchaseRequisitionId.New(), Number = "PR-1", RequiredDate = DateTime.Today,
            Items = { new PurchaseRequisitionItem { ItemId = "1", RequisitionId = PurchaseRequisitionId.New(), Code = "C", Description = "D", RequiredQuantity = 2, BudgetedUnitPrice = 10, RequiredDate = DateTime.Today } }
        };
        Assert.Contains("PR-1", ProcurementExportHelper.RequisitionToCsv(pr));
        Assert.Contains("20", ProcurementExportHelper.RequisitionsToCsv(new[] { pr }));
        var rfq = new RequestForQuotation
        {
            Id = RequestForQuotationId.New(), Number = "RFQ-1", Title = "T", DeadlineUtc = DateTime.UtcNow.AddDays(3),
            Items = { new RfqItem { ItemId = "1", RfqId = RequestForQuotationId.New(), Code = "C", Description = "D", Quantity = 1, RequiredDate = DateTime.Today } },
            Invitations = { new RfqSupplierInvitation { InvitationId = "i", RfqId = RequestForQuotationId.New(), SupplierId = SupplierId.New() } }
        };
        Assert.Contains("RFQ-1", ProcurementExportHelper.RfqToCsv(rfq));
        Assert.Contains("InvitationId", ProcurementExportHelper.InvitationsToCsv(rfq.Invitations));
        Assert.Contains("ResponseId", ProcurementExportHelper.ResponsesToCsv(new[]
        {
            new QuotationResponse { ResponseId = "r", RfqId = rfq.Id, SupplierId = SupplierId.New(), ReceivedUtc = DateTime.UtcNow }
        }));
    }
}
