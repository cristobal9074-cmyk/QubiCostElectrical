using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteWbsPlanningRepository : IWbsPlanningRepository
{
    private readonly string _databasePath;

    public SqliteWbsPlanningRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<WbsPlanningNode?> GetByIdAsync(WbsPlanningNodeId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_wbs_nodes WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<WbsPlanningNode>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<WbsPlanningNode>> GetByScheduleAsync(ScheduleId scheduleId, CancellationToken ct = default)
    {
        var rows = new List<WbsPlanningNode>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM planning_wbs_nodes WHERE schedule_id = $sid ORDER BY sort_order;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<WbsPlanningNode>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task SaveAsync(WbsPlanningNode node, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(node, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_wbs_nodes (id, schedule_id, wbs_code, name, description, node_type,
                    parent_id, level, sort_order, status, responsible, budget_item_id, apu_id,
                    created_at, modified_at, modified_by, tags, remarks, is_deleted, json)
                VALUES ($id, $sid, $code, $name, $desc, $ntype, $pid, $level, $sort, $status, $resp,
                    $bitem, $apu, $cat, $mat, $mby, $tags, $rem, $del, $json)
                ON CONFLICT(id) DO UPDATE SET
                    wbs_code=excluded.wbs_code, name=excluded.name, description=excluded.description,
                    node_type=excluded.node_type, parent_id=excluded.parent_id, level=excluded.level,
                    sort_order=excluded.sort_order, status=excluded.status, responsible=excluded.responsible,
                    budget_item_id=excluded.budget_item_id, apu_id=excluded.apu_id,
                    modified_at=excluded.modified_at, modified_by=excluded.modified_by,
                    tags=excluded.tags, remarks=excluded.remarks, is_deleted=excluded.is_deleted,
                    json=excluded.json;
                """;
            AddWbsParameters(cmd, node, json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task SaveBatchAsync(List<WbsPlanningNode> nodes, CancellationToken ct = default)
    {
        if (nodes.Count == 0) return;
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO planning_wbs_nodes (id, schedule_id, wbs_code, name, description, node_type,
                    parent_id, level, sort_order, status, responsible, budget_item_id, apu_id,
                    created_at, modified_at, modified_by, tags, remarks, is_deleted, json)
                VALUES ($id, $sid, $code, $name, $desc, $ntype, $pid, $level, $sort, $status, $resp,
                    $bitem, $apu, $cat, $mat, $mby, $tags, $rem, $del, $json)
                ON CONFLICT(id) DO UPDATE SET
                    wbs_code=excluded.wbs_code, name=excluded.name, description=excluded.description,
                    node_type=excluded.node_type, parent_id=excluded.parent_id, level=excluded.level,
                    sort_order=excluded.sort_order, status=excluded.status, responsible=excluded.responsible,
                    budget_item_id=excluded.budget_item_id, apu_id=excluded.apu_id,
                    modified_at=excluded.modified_at, modified_by=excluded.modified_by,
                    tags=excluded.tags, remarks=excluded.remarks, is_deleted=excluded.is_deleted,
                    json=excluded.json;
                """;
            AddWbsParameters(cmd, nodes[0], "");
            cmd.Prepare();
            foreach (var node in nodes)
            {
                var json = JsonSerializer.Serialize(node, ProfessionalDbHelpers.JsonOptions);
                cmd.Parameters["$id"].Value = node.Id.Value;
                cmd.Parameters["$sid"].Value = node.ScheduleId.Value;
                cmd.Parameters["$code"].Value = node.WbsCode;
                cmd.Parameters["$name"].Value = node.Name;
                cmd.Parameters["$desc"].Value = node.Description;
                cmd.Parameters["$ntype"].Value = node.NodeType.ToString();
                cmd.Parameters["$pid"].Value = node.ParentId?.Value ?? "";
                cmd.Parameters["$level"].Value = node.Level;
                cmd.Parameters["$sort"].Value = node.SortOrder;
                cmd.Parameters["$status"].Value = node.Status.ToString();
                cmd.Parameters["$resp"].Value = node.Responsible ?? "";
                cmd.Parameters["$bitem"].Value = node.BudgetItemId ?? "";
                cmd.Parameters["$apu"].Value = node.ApuId ?? "";
                cmd.Parameters["$cat"].Value = node.CreatedAt.ToString("O");
                cmd.Parameters["$mat"].Value = node.ModifiedAt.ToString("O");
                cmd.Parameters["$mby"].Value = node.ModifiedBy;
                cmd.Parameters["$tags"].Value = node.Tags;
                cmd.Parameters["$rem"].Value = node.Remarks;
                cmd.Parameters["$del"].Value = node.IsDeleted ? 1 : 0;
                cmd.Parameters["$json"].Value = json;
                await cmd.ExecuteNonQueryAsync(ct);
            }
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(WbsPlanningNodeId id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM planning_wbs_nodes WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> CodeExistsAsync(ScheduleId scheduleId, string wbsCode, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM planning_wbs_nodes WHERE schedule_id = $sid AND wbs_code = $code AND is_deleted = 0;";
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        cmd.Parameters.AddWithValue("$code", wbsCode);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<int> GetMaxSortOrderAsync(ScheduleId scheduleId, WbsPlanningNodeId? parentId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        using var cmd = connection.CreateCommand();
        if (parentId != null)
        {
            cmd.CommandText = "SELECT COALESCE(MAX(sort_order), 0) FROM planning_wbs_nodes WHERE schedule_id = $sid AND parent_id = $pid;";
            cmd.Parameters.AddWithValue("$pid", parentId.Value);
        }
        else
        {
            cmd.CommandText = "SELECT COALESCE(MAX(sort_order), 0) FROM planning_wbs_nodes WHERE schedule_id = $sid AND parent_id IS NULL;";
        }
        cmd.Parameters.AddWithValue("$sid", scheduleId.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static void AddWbsParameters(SqliteCommand cmd, WbsPlanningNode n, string json)
    {
        cmd.Parameters.AddWithValue("$id", n.Id.Value);
        cmd.Parameters.AddWithValue("$sid", n.ScheduleId.Value);
        cmd.Parameters.AddWithValue("$code", n.WbsCode);
        cmd.Parameters.AddWithValue("$name", n.Name);
        cmd.Parameters.AddWithValue("$desc", n.Description);
        cmd.Parameters.AddWithValue("$ntype", n.NodeType.ToString());
        cmd.Parameters.AddWithValue("$pid", n.ParentId?.Value ?? "");
        cmd.Parameters.AddWithValue("$level", n.Level);
        cmd.Parameters.AddWithValue("$sort", n.SortOrder);
        cmd.Parameters.AddWithValue("$status", n.Status.ToString());
        cmd.Parameters.AddWithValue("$resp", n.Responsible ?? "");
        cmd.Parameters.AddWithValue("$bitem", n.BudgetItemId ?? "");
        cmd.Parameters.AddWithValue("$apu", n.ApuId ?? "");
        cmd.Parameters.AddWithValue("$cat", n.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$mat", n.ModifiedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$mby", n.ModifiedBy);
        cmd.Parameters.AddWithValue("$tags", n.Tags);
        cmd.Parameters.AddWithValue("$rem", n.Remarks);
        cmd.Parameters.AddWithValue("$del", n.IsDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("$json", json);
    }
}
