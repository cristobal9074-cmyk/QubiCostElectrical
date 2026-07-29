using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteContractChangeTable
{
    internal const string ChangeRequests = "contract_change_requests";
    internal const string ChangeOrders = "contract_change_orders";
    internal const string ChangeOrderLines = "contract_change_order_lines";
    internal const string ChangeOrderVersions = "contract_change_order_versions";
    internal const string ChangeOrderSnapshots = "contract_change_order_snapshots";
    internal const string ChangeOrderAudit = "contract_change_order_audit";
    internal const string ExtraWorks = "contract_extra_works";
    internal const string Claims = "contract_claims";
    internal const string ClaimEvents = "contract_claim_events";
    internal const string ClaimEvidence = "contract_claim_evidence";
    internal const string ClaimEvaluations = "contract_claim_evaluations";
    internal const string ClaimResponses = "contract_claim_responses";
    internal const string ChangeAudit = "contract_change_audit";
}

public sealed class SqliteChangeRequestRepository : IChangeRequestRepository
{
    private readonly string _db;
    public SqliteChangeRequestRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ChangeRequest request, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractChangeTable.ChangeRequests} (
                id, contract_id, visible_number, source, title, description, cause,
                requester, request_date, urgency, wbs_code, affected_activities, affected_items,
                supporting_documents, preliminary_cost_impact, preliminary_days_impact,
                currency_code, status, responsible, comment, row_version,
                created_utc, created_by, modified_utc, modified_by, json_data)
            VALUES (
                $id, $cid, $vis, $src, $title, $desc, $cause,
                $req, $rdate, $urg, $wbs, $aa, $ai,
                $sd, $pci, $pdi,
                $cur, $st, $resp, $com, $rv,
                $cu, $cb, $mu, $mb, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, visible_number=excluded.visible_number,
                source=excluded.source, title=excluded.title, description=excluded.description,
                cause=excluded.cause, requester=excluded.requester,
                request_date=excluded.request_date, urgency=excluded.urgency,
                wbs_code=excluded.wbs_code, affected_activities=excluded.affected_activities,
                affected_items=excluded.affected_items,
                supporting_documents=excluded.supporting_documents,
                preliminary_cost_impact=excluded.preliminary_cost_impact,
                preliminary_days_impact=excluded.preliminary_days_impact,
                currency_code=excluded.currency_code, status=excluded.status,
                responsible=excluded.responsible, comment=excluded.comment,
                row_version=excluded.row_version,
                modified_utc=excluded.modified_utc, modified_by=excluded.modified_by,
                json_data=excluded.json_data;";
        BindParams(cmd, request);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ChangeRequest?> GetByIdAsync(ChangeRequestId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeRequests} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ChangeRequest>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ChangeRequest>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeRequests} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<List<ChangeRequest>> SearchAsync(string projectPath, string? text, ChangeRequestStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var sql = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeRequests} WHERE project_path=$pp";
        if (!string.IsNullOrEmpty(text)) sql += " AND (title LIKE $t OR description LIKE $t OR visible_number LIKE $t)";
        if (status.HasValue) sql += " AND status=$st";
        sql += " ORDER BY created_utc DESC LIMIT $take OFFSET $skip;";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$pp", projectPath);
        if (!string.IsNullOrEmpty(text)) cmd.Parameters.AddWithValue("$t", $"%{text}%");
        if (status.HasValue) cmd.Parameters.AddWithValue("$st", (int)status.Value);
        cmd.Parameters.AddWithValue("$take", take);
        cmd.Parameters.AddWithValue("$skip", skip);
        return await ReadListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractChangeTable.ChangeRequests} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", projectPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    private static void BindParams(SqliteCommand cmd, ChangeRequest r)
    {
        var json = JsonSerializer.Serialize(r, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", r.Id.Value);
        cmd.Parameters.AddWithValue("$cid", r.ContractId.Value);
        cmd.Parameters.AddWithValue("$vis", r.VisibleNumber);
        cmd.Parameters.AddWithValue("$src", (int)r.Source);
        cmd.Parameters.AddWithValue("$title", r.Title);
        cmd.Parameters.AddWithValue("$desc", r.Description);
        cmd.Parameters.AddWithValue("$cause", r.Cause);
        cmd.Parameters.AddWithValue("$req", r.Requester);
        cmd.Parameters.AddWithValue("$rdate", r.RequestDate.ToString("O"));
        cmd.Parameters.AddWithValue("$urg", (int)r.Urgency);
        cmd.Parameters.AddWithValue("$wbs", r.WbsCode);
        cmd.Parameters.AddWithValue("$aa", r.AffectedActivities);
        cmd.Parameters.AddWithValue("$ai", r.AffectedItems);
        cmd.Parameters.AddWithValue("$sd", r.SupportingDocuments);
        cmd.Parameters.AddWithValue("$pci", r.PreliminaryCostImpact);
        cmd.Parameters.AddWithValue("$pdi", r.PreliminaryDaysImpact);
        cmd.Parameters.AddWithValue("$cur", r.CurrencyCode);
        cmd.Parameters.AddWithValue("$st", (int)r.Status);
        cmd.Parameters.AddWithValue("$resp", r.Responsible);
        cmd.Parameters.AddWithValue("$com", r.Comment);
        cmd.Parameters.AddWithValue("$rv", r.RowVersion);
        cmd.Parameters.AddWithValue("$cu", r.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cb", r.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", r.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", r.ModifiedBy);
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<ChangeRequest>> ReadListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ChangeRequest>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ChangeRequest>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}

public sealed class SqliteChangeOrderRepository : IChangeOrderRepository
{
    private readonly string _db;
    public SqliteChangeOrderRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ChangeOrder order, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractChangeTable.ChangeOrders} (
                id, contract_id, change_request_id, visible_number, version, effective_date,
                currency_code, change_amount, added_days, added_items, modified_items, removed_items,
                affected_activities, affected_milestones, cost_impact, schedule_impact_days,
                forecast_impact, evm_impact, status, approvers, comment, hash_sha256, row_version,
                created_utc, created_by, modified_utc, modified_by, json_data)
            VALUES (
                $id, $cid, $crid, $vis, $ver, $ed,
                $cur, $ca, $ad, $ai, $mi, $ri,
                $aa, $am, $ci, $sid,
                $fi, $ei, $st, $apr, $com, $hash, $rv,
                $cu, $cb, $mu, $mb, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, change_request_id=excluded.change_request_id,
                visible_number=excluded.visible_number, version=excluded.version,
                effective_date=excluded.effective_date, currency_code=excluded.currency_code,
                change_amount=excluded.change_amount, added_days=excluded.added_days,
                added_items=excluded.added_items, modified_items=excluded.modified_items,
                removed_items=excluded.removed_items, affected_activities=excluded.affected_activities,
                affected_milestones=excluded.affected_milestones, cost_impact=excluded.cost_impact,
                schedule_impact_days=excluded.schedule_impact_days,
                forecast_impact=excluded.forecast_impact, evm_impact=excluded.evm_impact,
                status=excluded.status, approvers=excluded.approvers, comment=excluded.comment,
                hash_sha256=excluded.hash_sha256, row_version=excluded.row_version,
                modified_utc=excluded.modified_utc, modified_by=excluded.modified_by,
                json_data=excluded.json_data;";
        BindOrderParams(cmd, order);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ChangeOrder?> GetByIdAsync(ChangeOrderId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeOrders} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ChangeOrder>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ChangeOrder>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeOrders} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadOrderListAsync(cmd, ct);
    }

    public async Task<List<ChangeOrder>> GetByChangeRequestAsync(ChangeRequestId changeRequestId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeOrders} WHERE change_request_id=$crid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$crid", changeRequestId.Value);
        return await ReadOrderListAsync(cmd, ct);
    }

    public async Task<List<ChangeOrder>> SearchAsync(string projectPath, string? text, ChangeOrderStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var sql = $"SELECT json_data FROM {SqliteContractChangeTable.ChangeOrders} WHERE project_path=$pp";
        if (!string.IsNullOrEmpty(text)) sql += " AND (visible_number LIKE $t)";
        if (status.HasValue) sql += " AND status=$st";
        sql += " ORDER BY created_utc DESC LIMIT $take OFFSET $skip;";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$pp", projectPath);
        if (!string.IsNullOrEmpty(text)) cmd.Parameters.AddWithValue("$t", $"%{text}%");
        if (status.HasValue) cmd.Parameters.AddWithValue("$st", (int)status.Value);
        cmd.Parameters.AddWithValue("$take", take);
        cmd.Parameters.AddWithValue("$skip", skip);
        return await ReadOrderListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractChangeTable.ChangeOrders} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", projectPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    public async Task SaveVersionAsync(ChangeOrderVersion version, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT OR REPLACE INTO {SqliteContractChangeTable.ChangeOrderVersions}
            (id, change_order_id, version_number, snapshot_json, hash_sha256, changed_by, change_reason, created_utc)
            VALUES ($id, $coid, $vn, $sj, $hash, $cb, $cr, $cu);";
        cmd.Parameters.AddWithValue("$id", version.Id.Value);
        cmd.Parameters.AddWithValue("$coid", version.ChangeOrderId.Value);
        cmd.Parameters.AddWithValue("$vn", version.VersionNumber);
        cmd.Parameters.AddWithValue("$sj", version.SnapshotJson);
        cmd.Parameters.AddWithValue("$hash", version.HashSha256);
        cmd.Parameters.AddWithValue("$cb", version.ChangedBy);
        cmd.Parameters.AddWithValue("$cr", version.ChangeReason);
        cmd.Parameters.AddWithValue("$cu", version.CreatedUtc.ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ChangeOrderVersion>> GetVersionsAsync(ChangeOrderId orderId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ChangeOrderVersions} WHERE change_order_id=$coid ORDER BY version_number ASC;";
        cmd.Parameters.AddWithValue("$coid", orderId.Value);
        var list = new List<ChangeOrderVersion>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ChangeOrderVersion
            {
                Id = ChangeOrderVersionId.From(reader.GetString(0)),
                ChangeOrderId = ChangeOrderId.From(reader.GetString(1)),
                VersionNumber = reader.GetInt32(2),
                SnapshotJson = reader.GetString(3),
                HashSha256 = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ChangedBy = reader.IsDBNull(5) ? "" : reader.GetString(5),
                ChangeReason = reader.IsDBNull(6) ? "" : reader.GetString(6),
                CreatedUtc = DateTime.Parse(reader.GetString(7))
            });
        }
        return list;
    }

    public async Task SaveSnapshotAsync(ChangeOrderSnapshot snapshot, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT OR REPLACE INTO {SqliteContractChangeTable.ChangeOrderSnapshots}
            (id, change_order_id, contract_id, project_path, asof_utc, snapshot_json, hash, version_label)
            VALUES ($id, $coid, $cid, $pp, $au, $sj, $hash, $vl);";
        cmd.Parameters.AddWithValue("$id", snapshot.Id.Value);
        cmd.Parameters.AddWithValue("$coid", snapshot.ChangeOrderId.Value);
        cmd.Parameters.AddWithValue("$cid", snapshot.ContractId.Value);
        cmd.Parameters.AddWithValue("$pp", snapshot.ProjectPath);
        cmd.Parameters.AddWithValue("$au", snapshot.AsOfUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$sj", snapshot.SnapshotJson);
        cmd.Parameters.AddWithValue("$hash", snapshot.Hash);
        cmd.Parameters.AddWithValue("$vl", snapshot.VersionLabel);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ChangeOrderSnapshot>> GetSnapshotsAsync(ChangeOrderId orderId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ChangeOrderSnapshots} WHERE change_order_id=$coid ORDER BY asof_utc DESC;";
        cmd.Parameters.AddWithValue("$coid", orderId.Value);
        var list = new List<ChangeOrderSnapshot>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ChangeOrderSnapshot
            {
                Id = ChangeOrderSnapshotId.From(reader.GetString(0)),
                ChangeOrderId = ChangeOrderId.From(reader.GetString(1)),
                ContractId = ContractId.From(reader.GetString(2)),
                ProjectPath = reader.IsDBNull(3) ? "" : reader.GetString(3),
                AsOfUtc = DateTime.Parse(reader.GetString(4)),
                SnapshotJson = reader.GetString(5),
                Hash = reader.IsDBNull(6) ? "" : reader.GetString(6),
                VersionLabel = reader.IsDBNull(7) ? "" : reader.GetString(7)
            });
        }
        return list;
    }

    private static void BindOrderParams(SqliteCommand cmd, ChangeOrder o)
    {
        var json = JsonSerializer.Serialize(o, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", o.Id.Value);
        cmd.Parameters.AddWithValue("$cid", o.ContractId.Value);
        cmd.Parameters.AddWithValue("$crid", o.ChangeRequestId?.Value ?? "");
        cmd.Parameters.AddWithValue("$vis", o.VisibleNumber);
        cmd.Parameters.AddWithValue("$ver", o.Version);
        cmd.Parameters.AddWithValue("$ed", o.EffectiveDate.ToString("O"));
        cmd.Parameters.AddWithValue("$cur", o.CurrencyCode);
        cmd.Parameters.AddWithValue("$ca", o.ChangeAmount);
        cmd.Parameters.AddWithValue("$ad", o.AddedDays);
        cmd.Parameters.AddWithValue("$ai", o.AddedItems);
        cmd.Parameters.AddWithValue("$mi", o.ModifiedItems);
        cmd.Parameters.AddWithValue("$ri", o.RemovedItems);
        cmd.Parameters.AddWithValue("$aa", o.AffectedActivities);
        cmd.Parameters.AddWithValue("$am", o.AffectedMilestones);
        cmd.Parameters.AddWithValue("$ci", o.CostImpact);
        cmd.Parameters.AddWithValue("$sid", o.ScheduleImpactDays);
        cmd.Parameters.AddWithValue("$fi", o.ForecastImpact);
        cmd.Parameters.AddWithValue("$ei", o.EvmImpact);
        cmd.Parameters.AddWithValue("$st", (int)o.Status);
        cmd.Parameters.AddWithValue("$apr", o.Approvers);
        cmd.Parameters.AddWithValue("$com", o.Comment);
        cmd.Parameters.AddWithValue("$hash", o.HashSha256);
        cmd.Parameters.AddWithValue("$rv", o.RowVersion);
        cmd.Parameters.AddWithValue("$cu", o.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cb", o.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", o.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", o.ModifiedBy);
        cmd.Parameters.AddWithValue("$json", json);
    }

    private static async Task<List<ChangeOrder>> ReadOrderListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ChangeOrder>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ChangeOrder>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
