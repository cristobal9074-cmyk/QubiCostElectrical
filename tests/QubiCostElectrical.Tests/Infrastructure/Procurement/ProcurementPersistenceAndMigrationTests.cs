using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Procurement;

public sealed class ProcurementPersistenceAndMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public ProcurementPersistenceAndMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qce-proc-" + Guid.NewGuid().ToString("N") + ".db");
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }
        catch { /* ignore */ }
    }

    [Fact]
    public void Schema_Is_At_Least_44()
    {
        Assert.True(ProductVersionInfo.DatabaseSchemaVersion >= 44);
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, SQLiteSchemaMigrator.LatestSchemaVersion);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, SQLiteSchemaMigrator.GetCurrentUserVersion(conn));
    }

    [Fact]
    public async Task Supplier_Contact_Roundtrip()
    {
        var suppliers = new SqliteSupplierRepository(_dbPath);
        var contacts = new SqliteSupplierContactRepository(_dbPath);
        var s = new Supplier
        {
            Id = SupplierId.New(), Code = "SUP1", LegalName = "Proveedor Uno", Status = SupplierStatus.Active,
            CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow, CreatedBy = "t", ModifiedBy = "t"
        };
        await suppliers.SaveAsync(s);
        Assert.Equal("SUP1", (await suppliers.GetByCodeAsync("sup1"))!.Code);
        var c = new SupplierContact { ContactId = Guid.NewGuid().ToString("N"), SupplierId = s.Id, Name = "Ana", Email = "a@x.com", IsPrimary = true };
        await contacts.SaveAsync(c);
        Assert.Single(await contacts.GetBySupplierAsync(s.Id));
        await contacts.DeleteAsync(c.ContactId);
        Assert.Empty(await contacts.GetBySupplierAsync(s.Id));
    }

    [Fact]
    public async Task Requisition_Items_Reopen()
    {
        var repo = new SqlitePurchaseRequisitionRepository(_dbPath);
        var id = PurchaseRequisitionId.New();
        var pr = new PurchaseRequisition
        {
            Id = id, Number = "PR-00001", ProjectPath = "p", RequiredDate = DateTime.Today.AddDays(10),
            CreatedUtc = DateTime.UtcNow, Status = PurchaseRequisitionStatus.Draft,
            Items =
            {
                new PurchaseRequisitionItem
                {
                    ItemId = "i1", RequisitionId = id, Code = "M", Description = "Mat", RequiredQuantity = 3,
                    RequiredDate = DateTime.Today.AddDays(10), Unit = "UN",
                    Origin = new NeedOrigin { Kind = NeedOriginKind.Budget, OriginId = "b1", OriginalQuantity = 3, User = "t", Utc = DateTime.UtcNow }
                }
            }
        };
        await repo.SaveAsync(pr);
        var loaded = await repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Items);
        Assert.Equal(NeedOriginKind.Budget, loaded.Items[0].Origin.Kind);
        Assert.Equal(2, await repo.GetNextNumberAsync("p"));
    }

    [Fact]
    public async Task Rfq_Invitations_Responses_Audit_Documents()
    {
        var rfqRepo = new SqliteRequestForQuotationRepository(_dbPath);
        var qrRepo = new SqliteQuotationResponseRepository(_dbPath);
        var docs = new SqliteProcurementDocumentRepository(_dbPath);
        var audit = new SqliteProcurementAuditRepository(_dbPath);
        var id = RequestForQuotationId.New();
        var sid = SupplierId.New();
        var rfq = new RequestForQuotation
        {
            Id = id, Number = "RFQ-00001", ProjectPath = "p", Title = "T", DeadlineUtc = DateTime.UtcNow.AddDays(7),
            CreatedUtc = DateTime.UtcNow, CreatedBy = "t",
            Items = { new RfqItem { ItemId = "1", RfqId = id, Code = "C", Description = "D", Quantity = 1, RequiredDate = DateTime.Today } },
            Invitations = { new RfqSupplierInvitation { InvitationId = "inv", RfqId = id, SupplierId = sid, Status = RfqInvitationStatus.Sent } }
        };
        await rfqRepo.SaveAsync(rfq);
        Assert.Equal("RFQ-00001", (await rfqRepo.GetByIdAsync(id))!.Number);
        await qrRepo.SaveAsync(new QuotationResponse
        {
            ResponseId = "r1", RfqId = id, SupplierId = sid, ReceivedUtc = DateTime.UtcNow, CurrencyCode = "CLP", RecordedBy = "t"
        });
        Assert.Single(await qrRepo.GetByRfqAsync(id));
        await docs.SaveAsync(new ProcurementDocumentRef
        {
            DocumentId = "d1", EntityType = "Rfq", EntityId = id.Value, Kind = ProcurementDocumentKind.Offer,
            Title = "Oferta", PathOrUri = "file://offer.pdf", AttachedUtc = DateTime.UtcNow, AttachedBy = "t"
        });
        Assert.Single(await docs.GetByEntityAsync("Rfq", id.Value));
        await audit.RecordAsync(new ProcurementAuditEvent
        {
            Id = Guid.NewGuid().ToString("N"), EntityType = "Rfq", EntityId = id.Value, Action = "Issue",
            Actor = "t", UtcTimestamp = DateTime.UtcNow, Result = "Ok"
        });
        Assert.Single(await audit.GetByEntityAsync("Rfq", id.Value));
    }

    [Fact]
    public async Task Transactional_Services_Against_Sqlite()
    {
        var perm = new AllowAllProcurementPermissionChecker();
        var audit = new SqliteProcurementAuditRepository(_dbPath);
        var suppliers = new SqliteSupplierRepository(_dbPath);
        var contacts = new SqliteSupplierContactRepository(_dbPath);
        var prs = new SqlitePurchaseRequisitionRepository(_dbPath);
        var rfqs = new SqliteRequestForQuotationRepository(_dbPath);
        var qrs = new SqliteQuotationResponseRepository(_dbPath);
        var supplierSvc = new SupplierService(suppliers, contacts, audit, perm);
        var prSvc = new PurchaseRequisitionService(prs, audit, perm);
        var rfqSvc = new RfqService(rfqs, prs, suppliers, contacts, qrs, audit, perm);

        var supplier = await supplierSvc.CreateAsync(new Supplier { Code = "LIVE", LegalName = "Live", Status = SupplierStatus.Active }, "u");
        var pr = await prSvc.CreateAsync(new PurchaseRequisition { ProjectPath = "live", RequiredDate = DateTime.Today.AddDays(12) }, "req");
        pr = await prSvc.AddItemAsync(pr.Id, new PurchaseRequisitionItem
        {
            ItemId = Guid.NewGuid().ToString("N"), RequisitionId = pr.Id, Code = "X", Description = "X",
            RequiredQuantity = 1, RequiredDate = pr.RequiredDate
        }, pr.RowVersion, "req");
        pr = await prSvc.SubmitAsync(pr.Id, pr.RowVersion, "req");
        pr = await prSvc.ApproveAsync(pr.Id, pr.RowVersion, "apr", null);
        var rfq = await rfqSvc.CreateFromRequisitionsAsync("live", new[] { pr.Id }, "Live RFQ", "buyer");
        await rfqSvc.InviteSupplierAsync(rfq.Id, supplier.Id, null, rfq.RowVersion, "buyer");
        rfq = (await rfqSvc.GetAsync(rfq.Id))!;
        rfq = await rfqSvc.IssueAsync(rfq.Id, rfq.RowVersion, "buyer");
        await rfqSvc.RegisterResponseAsync(new QuotationResponse { RfqId = rfq.Id, SupplierId = supplier.Id }, "buyer");

        // reopen
        Assert.NotNull(await suppliers.GetByIdAsync(supplier.Id));
        Assert.NotNull(await prs.GetByIdAsync(pr.Id));
        Assert.Equal(RfqStatus.Closed, (await rfqs.GetByIdAsync(rfq.Id))!.Status);
    }

    [Theory]
    [InlineData("procurement_suppliers")]
    [InlineData("procurement_supplier_contacts")]
    [InlineData("procurement_requisitions")]
    [InlineData("procurement_requisition_items")]
    [InlineData("procurement_requisition_origins")]
    [InlineData("procurement_rfqs")]
    [InlineData("procurement_rfq_items")]
    [InlineData("procurement_rfq_suppliers")]
    [InlineData("procurement_quotation_responses")]
    [InlineData("procurement_documents")]
    [InlineData("procurement_audit")]
    public void Migration_Creates_Table(string table)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", table);
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }
}

public sealed class ProcurementArchitectureGuardTests
{
    [Fact]
    public void Domain_Has_No_Wpf_Reference()
    {
        var refs = typeof(Supplier).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(refs, n => n.Contains("Wpf", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_Has_No_Wpf_Reference()
    {
        var refs = typeof(SupplierService).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(refs, n => n.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Infrastructure_Implements_Supplier_Repo() =>
        Assert.True(typeof(ISupplierRepository).IsAssignableFrom(typeof(SqliteSupplierRepository)));

    [Fact]
    public void Export_Helper_Lives_In_Application() =>
        Assert.Equal("QubiCostElectrical.Application", typeof(ProcurementExportHelper).Assembly.GetName().Name);

    [Theory]
    [InlineData(typeof(ISupplierRepository))]
    [InlineData(typeof(ISupplierContactRepository))]
    [InlineData(typeof(IPurchaseRequisitionRepository))]
    [InlineData(typeof(IPurchaseRequisitionItemRepository))]
    [InlineData(typeof(IRequestForQuotationRepository))]
    [InlineData(typeof(IRfqSupplierInvitationRepository))]
    [InlineData(typeof(IQuotationResponseRepository))]
    [InlineData(typeof(IProcurementAuditRepository))]
    public void Repository_Interfaces_Exist(Type t) => Assert.True(t.IsInterface);

    [Theory]
    [InlineData(ProcurementPermissions.SupplierView)]
    [InlineData(ProcurementPermissions.SupplierCreate)]
    [InlineData(ProcurementPermissions.SupplierEdit)]
    [InlineData(ProcurementPermissions.SupplierDisable)]
    [InlineData(ProcurementPermissions.RequisitionView)]
    [InlineData(ProcurementPermissions.RequisitionCreate)]
    [InlineData(ProcurementPermissions.RequisitionEdit)]
    [InlineData(ProcurementPermissions.RequisitionSubmit)]
    [InlineData(ProcurementPermissions.RequisitionApprove)]
    [InlineData(ProcurementPermissions.RequisitionReject)]
    [InlineData(ProcurementPermissions.RfqView)]
    [InlineData(ProcurementPermissions.RfqCreate)]
    [InlineData(ProcurementPermissions.RfqEdit)]
    [InlineData(ProcurementPermissions.RfqApprove)]
    [InlineData(ProcurementPermissions.RfqIssue)]
    [InlineData(ProcurementPermissions.QuotationResponseRegister)]
    [InlineData(ProcurementPermissions.ProcurementAuditView)]
    [InlineData(ProcurementPermissions.ProcurementDocumentManage)]
    public void Permission_Constants_Defined(string permission) =>
        Assert.StartsWith("Procurement.", permission);
}
