using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqlitePurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly string _db;
    public SqlitePurchaseOrderRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(PurchaseOrder order, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_purchase_orders (id, project_path, award_id, supplier_id, status, version, updated_utc, json_data)
            VALUES ($id, $proj, $aid, $sid, $st, $ver, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status, version=excluded.version, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", order.Id.Value);
        cmd.Parameters.AddWithValue("$proj", order.ProjectPath);
        cmd.Parameters.AddWithValue("$aid", (object?)order.AwardId?.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sid", order.SupplierId.Value);
        cmd.Parameters.AddWithValue("$st", (int)order.Status);
        cmd.Parameters.AddWithValue("$ver", order.Version);
        cmd.Parameters.AddWithValue("$utc", order.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(order, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<PurchaseOrder?> GetByIdAsync(PurchaseOrderId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_purchase_orders WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<PurchaseOrder>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<PurchaseOrder>> GetByProjectAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_purchase_orders WHERE project_path=$p ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        return await ReadOrders(cmd, ct);
    }

    public async Task<List<PurchaseOrder>> GetByAwardAsync(AwardId awardId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_purchase_orders WHERE award_id=$a ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$a", awardId.Value);
        return await ReadOrders(cmd, ct);
    }

    private static async Task<List<PurchaseOrder>> ReadOrders(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<PurchaseOrder>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<PurchaseOrder>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteGoodsReceiptRepository : IGoodsReceiptRepository
{
    private readonly string _db;
    public SqliteGoodsReceiptRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(GoodsReceipt receipt, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_goods_receipts (id, po_id, project_path, status, updated_utc, json_data)
            VALUES ($id, $po, $proj, $st, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", receipt.Id.Value);
        cmd.Parameters.AddWithValue("$po", receipt.PurchaseOrderId.Value);
        cmd.Parameters.AddWithValue("$proj", receipt.ProjectPath);
        cmd.Parameters.AddWithValue("$st", (int)receipt.Status);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(receipt, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<GoodsReceipt?> GetByIdAsync(GoodsReceiptId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_goods_receipts WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<GoodsReceipt>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<GoodsReceipt>> GetByPurchaseOrderAsync(PurchaseOrderId poId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_goods_receipts WHERE po_id=$po ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$po", poId.Value);
        return await Read(cmd, ct);
    }

    public async Task<List<GoodsReceipt>> GetByProjectAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_goods_receipts WHERE project_path=$p ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        return await Read(cmd, ct);
    }

    private static async Task<List<GoodsReceipt>> Read(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<GoodsReceipt>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<GoodsReceipt>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteGoodsReturnRepository : IGoodsReturnRepository
{
    private readonly string _db;
    public SqliteGoodsReturnRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(GoodsReturn ret, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_goods_returns (id, po_id, project_path, status, updated_utc, json_data)
            VALUES ($id, $po, $proj, $st, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET status=excluded.status, updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", ret.Id.Value);
        cmd.Parameters.AddWithValue("$po", ret.PurchaseOrderId.Value);
        cmd.Parameters.AddWithValue("$proj", ret.ProjectPath);
        cmd.Parameters.AddWithValue("$st", (int)ret.Status);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(ret, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<GoodsReturn?> GetByIdAsync(GoodsReturnId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_goods_returns WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<GoodsReturn>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<GoodsReturn>> GetByProjectAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_goods_returns WHERE project_path=$p ORDER BY updated_utc DESC;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        var list = new List<GoodsReturn>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<GoodsReturn>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteProjectInventoryRepository : IProjectInventoryRepository
{
    private readonly string _db;
    public SqliteProjectInventoryRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ProjectInventoryBalance balance, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_inventory (id, project_path, code, lot, location, updated_utc, json_data)
            VALUES ($id, $proj, $code, $lot, $loc, $utc, $json)
            ON CONFLICT(id) DO UPDATE SET updated_utc=excluded.updated_utc, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", balance.Id.Value);
        cmd.Parameters.AddWithValue("$proj", balance.ProjectPath);
        cmd.Parameters.AddWithValue("$code", balance.Code);
        cmd.Parameters.AddWithValue("$lot", balance.Lot);
        cmd.Parameters.AddWithValue("$loc", balance.Location);
        cmd.Parameters.AddWithValue("$utc", balance.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(balance, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ProjectInventoryBalance?> GetAsync(string projectPath, string code, string lot, string location, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_inventory WHERE project_path=$p AND code=$c AND lot=$lot AND location=$loc LIMIT 1;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        cmd.Parameters.AddWithValue("$c", code);
        cmd.Parameters.AddWithValue("$lot", lot ?? "");
        cmd.Parameters.AddWithValue("$loc", location ?? "");
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ProjectInventoryBalance>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ProjectInventoryBalance>> ListByProjectAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_inventory WHERE project_path=$p ORDER BY code;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        var list = new List<ProjectInventoryBalance>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProjectInventoryBalance>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }

    public async Task SaveMovementAsync(InventoryMovement movement, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_inventory_movements (id, project_path, code, kind, utc_ts, json_data)
            VALUES ($id, $proj, $code, $kind, $utc, $json);";
        cmd.Parameters.AddWithValue("$id", movement.MovementId);
        cmd.Parameters.AddWithValue("$proj", movement.ProjectPath);
        cmd.Parameters.AddWithValue("$code", movement.Code);
        cmd.Parameters.AddWithValue("$kind", (int)movement.Kind);
        cmd.Parameters.AddWithValue("$utc", movement.UtcTimestamp.ToString("O"));
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(movement, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<InventoryMovement>> ListMovementsAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_inventory_movements WHERE project_path=$p ORDER BY utc_ts DESC;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        var list = new List<InventoryMovement>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<InventoryMovement>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteProcurementCommitmentSnapshotRepository : IProcurementCommitmentSnapshotRepository
{
    private readonly string _db;
    public SqliteProcurementCommitmentSnapshotRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ProcurementCommitmentSummary summary, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO procurement_commitment_snapshots (id, project_path, asof_utc, hash, json_data)
            VALUES ($id, $proj, $asof, $hash, $json);";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("$proj", summary.ProjectPath);
        cmd.Parameters.AddWithValue("$asof", summary.AsOfUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$hash", summary.SnapshotHash);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(summary, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ProcurementCommitmentSummary>> ListByProjectAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM procurement_commitment_snapshots WHERE project_path=$p ORDER BY asof_utc DESC;";
        cmd.Parameters.AddWithValue("$p", projectPath);
        var list = new List<ProcurementCommitmentSummary>();
        using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var item = JsonSerializer.Deserialize<ProcurementCommitmentSummary>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (item is not null) list.Add(item);
        }
        return list;
    }
}
