using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Procurement;

public sealed class Phase9CdePersistenceTests : IDisposable
{
    private readonly string _dbPath;

    public Phase9CdePersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "qce-9cde-" + Guid.NewGuid().ToString("N") + ".db");
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
    }

    public void Dispose()
    {
        try { SqliteConnection.ClearAllPools(); if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void Schema_Is_59()
    {
        Assert.Equal(59, ProductVersionInfo.DatabaseSchemaVersion);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        Assert.Equal(59, SQLiteSchemaMigrator.GetCurrentUserVersion(conn));
    }

    [Theory]
    [InlineData("procurement_purchase_orders")]
    [InlineData("procurement_goods_receipts")]
    [InlineData("procurement_goods_returns")]
    [InlineData("procurement_inventory")]
    [InlineData("procurement_inventory_movements")]
    [InlineData("procurement_commitment_snapshots")]
    public void Migration_Creates_Table(string table)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", table);
        Assert.Equal(1L, (long)cmd.ExecuteScalar()!);
    }

    [Fact]
    public async Task Po_Receipt_Inventory_Roundtrip()
    {
        var perm = new AllowAllProcurementPermissionChecker();
        var audit = new SqliteProcurementAuditRepository(_dbPath);
        var awards = new SqliteAwardRepository(_dbPath);
        var pos = new SqlitePurchaseOrderRepository(_dbPath);
        var gr = new SqliteGoodsReceiptRepository(_dbPath);
        var ret = new SqliteGoodsReturnRepository(_dbPath);
        var inv = new SqliteProjectInventoryRepository(_dbPath);
        var award = new AwardDecision
        {
            Id = AwardId.New(), RfqId = RequestForQuotationId.New(), ProjectPath = "proj", Status = AwardStatus.Approved,
            CreatedBy = "u", CreatedUtc = DateTime.UtcNow,
            Items =
            {
                new AwardedItem
                {
                    AwardedItemId = "a1", AwardId = AwardId.New(), Code = "C1", Description = "D",
                    OfferId = SupplierOfferId.New(), SupplierId = SupplierId.New(), Quantity = 4, UnitPriceBase = 25, UnitPriceOriginal = 25
                }
            }
        };
        await awards.SaveAsync(award);
        var poSvc = new PurchaseOrderService(pos, awards, audit, perm);
        var po = (await poSvc.GenerateFromAwardAsync(award.Id, "u"))[0];
        po = await poSvc.SubmitAsync(po.Id, po.RowVersion, "u");
        po = await poSvc.ApproveAsync(po.Id, po.RowVersion, "mgr");
        po = await poSvc.IssueAsync(po.Id, po.RowVersion, "u");
        var grSvc = new GoodsReceiptService(gr, ret, inv, pos, audit, perm);
        var receipt = await grSvc.CreateFromPurchaseOrderAsync(po.Id, "u", false);
        await grSvc.AcceptAsync(receipt.Id, receipt.RowVersion, "u");
        Assert.NotEmpty(await grSvc.ListInventoryAsync("proj"));
        Assert.NotNull(await pos.GetByIdAsync(po.Id));
    }
}
