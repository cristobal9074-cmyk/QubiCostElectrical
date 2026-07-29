using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqliteContractClaimRepository : IContractClaimRepository
{
    private readonly string _db;
    public SqliteContractClaimRepository(string databasePath) => _db = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

    public async Task SaveAsync(ContractClaim claim, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var json = JsonSerializer.Serialize(claim, ProfessionalDbHelpers.JsonOptions);
        cmd.CommandText = $@"
            INSERT INTO {SqliteContractChangeTable.Claims} (
                id, contract_id, visible_number, type, event_description,
                event_date, notification_date, cause, description,
                amount_claimed, days_claimed, amount_awarded, days_awarded,
                currency_code, affected_activities, wbs_code, supporting_documents,
                responsible, response, status, row_version,
                created_utc, created_by, modified_utc, modified_by, json_data)
            VALUES (
                $id, $cid, $vis, $typ, $evd,
                $evdt, $ndt, $cau, $desc,
                $ac, $dc, $aa, $da,
                $cur, $aa2, $wbs, $sd,
                $resp, $rsp, $st, $rv,
                $cu, $cb, $mu, $mb, $json)
            ON CONFLICT(id) DO UPDATE SET
                contract_id=excluded.contract_id, visible_number=excluded.visible_number,
                type=excluded.type, event_description=excluded.event_description,
                event_date=excluded.event_date, notification_date=excluded.notification_date,
                cause=excluded.cause, description=excluded.description,
                amount_claimed=excluded.amount_claimed, days_claimed=excluded.days_claimed,
                amount_awarded=excluded.amount_awarded, days_awarded=excluded.days_awarded,
                currency_code=excluded.currency_code, affected_activities=excluded.affected_activities,
                wbs_code=excluded.wbs_code, supporting_documents=excluded.supporting_documents,
                responsible=excluded.responsible, response=excluded.response,
                status=excluded.status, row_version=excluded.row_version,
                modified_utc=excluded.modified_utc, modified_by=excluded.modified_by,
                json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", claim.Id.Value);
        cmd.Parameters.AddWithValue("$cid", claim.ContractId.Value);
        cmd.Parameters.AddWithValue("$vis", claim.VisibleNumber);
        cmd.Parameters.AddWithValue("$typ", (int)claim.Type);
        cmd.Parameters.AddWithValue("$evd", claim.EventDescription);
        cmd.Parameters.AddWithValue("$evdt", claim.EventDate.ToString("O"));
        cmd.Parameters.AddWithValue("$ndt", claim.NotificationDate.ToString("O"));
        cmd.Parameters.AddWithValue("$cau", claim.Cause);
        cmd.Parameters.AddWithValue("$desc", claim.Description);
        cmd.Parameters.AddWithValue("$ac", claim.AmountClaimed);
        cmd.Parameters.AddWithValue("$dc", claim.DaysClaimed);
        cmd.Parameters.AddWithValue("$aa", claim.AmountAwarded);
        cmd.Parameters.AddWithValue("$da", claim.DaysAwarded);
        cmd.Parameters.AddWithValue("$cur", claim.CurrencyCode);
        cmd.Parameters.AddWithValue("$aa2", claim.AffectedActivities);
        cmd.Parameters.AddWithValue("$wbs", claim.WbsCode);
        cmd.Parameters.AddWithValue("$sd", claim.SupportingDocuments);
        cmd.Parameters.AddWithValue("$resp", claim.Responsible);
        cmd.Parameters.AddWithValue("$rsp", claim.Response);
        cmd.Parameters.AddWithValue("$st", (int)claim.Status);
        cmd.Parameters.AddWithValue("$rv", claim.RowVersion);
        cmd.Parameters.AddWithValue("$cu", claim.CreatedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$cb", claim.CreatedBy);
        cmd.Parameters.AddWithValue("$mu", claim.ModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$mb", claim.ModifiedBy);
        cmd.Parameters.AddWithValue("$json", json);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractClaim?> GetByIdAsync(ClaimId id, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.Claims} WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is string j ? JsonSerializer.Deserialize<ContractClaim>(j, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractClaim>> GetByContractAsync(ContractId contractId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT json_data FROM {SqliteContractChangeTable.Claims} WHERE contract_id=$cid ORDER BY created_utc DESC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        return await ReadClaimListAsync(cmd, ct);
    }

    public async Task<List<ContractClaim>> SearchAsync(string projectPath, string? text, ClaimStatus? status, int skip, int take, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        var sql = $"SELECT json_data FROM {SqliteContractChangeTable.Claims} WHERE project_path=$pp";
        if (!string.IsNullOrEmpty(text)) sql += " AND (description LIKE $t OR visible_number LIKE $t)";
        if (status.HasValue) sql += " AND status=$st";
        sql += " ORDER BY created_utc DESC LIMIT $take OFFSET $skip;";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$pp", projectPath);
        if (!string.IsNullOrEmpty(text)) cmd.Parameters.AddWithValue("$t", $"%{text}%");
        if (status.HasValue) cmd.Parameters.AddWithValue("$st", (int)status.Value);
        cmd.Parameters.AddWithValue("$take", take);
        cmd.Parameters.AddWithValue("$skip", skip);
        return await ReadClaimListAsync(cmd, ct);
    }

    public async Task<int> GetNextNumberAsync(string projectPath, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT COALESCE(MAX(CAST(visible_number AS INTEGER)), 0) + 1 FROM {SqliteContractChangeTable.Claims} WHERE project_path=$pp;";
        cmd.Parameters.AddWithValue("$pp", projectPath);
        var o = await cmd.ExecuteScalarAsync(ct);
        return o is long l ? (int)l : 1;
    }

    public async Task SaveEventAsync(ClaimEvent claimEvent, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT OR REPLACE INTO {SqliteContractChangeTable.ClaimEvents}
            (id, claim_id, event_type, description, event_date, actor, details_json)
            VALUES ($id, $clid, $et, $desc, $ed, $act, $dj);";
        cmd.Parameters.AddWithValue("$id", claimEvent.Id.Value);
        cmd.Parameters.AddWithValue("$clid", claimEvent.ClaimId.Value);
        cmd.Parameters.AddWithValue("$et", claimEvent.EventType);
        cmd.Parameters.AddWithValue("$desc", claimEvent.Description);
        cmd.Parameters.AddWithValue("$ed", claimEvent.EventDate.ToString("O"));
        cmd.Parameters.AddWithValue("$act", claimEvent.Actor);
        cmd.Parameters.AddWithValue("$dj", claimEvent.DetailsJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ClaimEvent>> GetEventsAsync(ClaimId claimId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ClaimEvents} WHERE claim_id=$clid ORDER BY event_date ASC;";
        cmd.Parameters.AddWithValue("$clid", claimId.Value);
        var list = new List<ClaimEvent>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ClaimEvent
            {
                Id = ClaimEventId.From(reader.GetString(0)),
                ClaimId = ClaimId.From(reader.GetString(1)),
                EventType = reader.GetString(2),
                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                EventDate = DateTime.Parse(reader.GetString(4)),
                Actor = reader.IsDBNull(5) ? "" : reader.GetString(5),
                DetailsJson = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }
        return list;
    }

    public async Task SaveEvidenceAsync(ClaimEvidence evidence, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT OR REPLACE INTO {SqliteContractChangeTable.ClaimEvidence}
            (id, claim_id, document_reference, description, file_name, uploaded_utc, uploaded_by)
            VALUES ($id, $clid, $dr, $desc, $fn, $uu, $ub);";
        cmd.Parameters.AddWithValue("$id", evidence.Id.Value);
        cmd.Parameters.AddWithValue("$clid", evidence.ClaimId.Value);
        cmd.Parameters.AddWithValue("$dr", evidence.DocumentReference);
        cmd.Parameters.AddWithValue("$desc", evidence.Description);
        cmd.Parameters.AddWithValue("$fn", evidence.FileName);
        cmd.Parameters.AddWithValue("$uu", evidence.UploadedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$ub", evidence.UploadedBy);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<ClaimEvidence>> GetEvidenceAsync(ClaimId claimId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ClaimEvidence} WHERE claim_id=$clid ORDER BY uploaded_utc DESC;";
        cmd.Parameters.AddWithValue("$clid", claimId.Value);
        var list = new List<ClaimEvidence>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ClaimEvidence
            {
                Id = ClaimEvidenceId.From(reader.GetString(0)),
                ClaimId = ClaimId.From(reader.GetString(1)),
                DocumentReference = reader.IsDBNull(2) ? "" : reader.GetString(2),
                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                FileName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                UploadedUtc = DateTime.Parse(reader.GetString(5)),
                UploadedBy = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }
        return list;
    }

    public async Task SaveEvaluationAsync(ClaimEvaluation evaluation, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT OR REPLACE INTO {SqliteContractChangeTable.ClaimEvaluations}
            (id, claim_id, evaluator, evaluation_date, evaluated_amount, evaluated_days, justification, recommendation)
            VALUES ($id, $clid, $ev, $ed, $ea, $eday, $jus, $rec);";
        cmd.Parameters.AddWithValue("$id", evaluation.Id.Value);
        cmd.Parameters.AddWithValue("$clid", evaluation.ClaimId.Value);
        cmd.Parameters.AddWithValue("$ev", evaluation.Evaluator);
        cmd.Parameters.AddWithValue("$ed", evaluation.EvaluationDate.ToString("O"));
        cmd.Parameters.AddWithValue("$ea", evaluation.EvaluatedAmount);
        cmd.Parameters.AddWithValue("$eday", evaluation.EvaluatedDays);
        cmd.Parameters.AddWithValue("$jus", evaluation.Justification);
        cmd.Parameters.AddWithValue("$rec", evaluation.Recommendation);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ClaimEvaluation?> GetEvaluationAsync(ClaimId claimId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ClaimEvaluations} WHERE claim_id=$clid ORDER BY evaluation_date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$clid", claimId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new ClaimEvaluation
            {
                Id = ClaimEvaluationId.From(reader.GetString(0)),
                ClaimId = ClaimId.From(reader.GetString(1)),
                Evaluator = reader.IsDBNull(2) ? "" : reader.GetString(2),
                EvaluationDate = DateTime.Parse(reader.GetString(3)),
                EvaluatedAmount = reader.GetDecimal(4),
                EvaluatedDays = reader.GetInt32(5),
                Justification = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Recommendation = reader.IsDBNull(7) ? "" : reader.GetString(7)
            };
        }
        return null;
    }

    public async Task SaveResponseAsync(ClaimResponse response, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $@"INSERT OR REPLACE INTO {SqliteContractChangeTable.ClaimResponses}
            (id, claim_id, responder, response_date, response_type, response_detail, offered_amount, offered_days, comment)
            VALUES ($id, $clid, $resp, $rd, $rt, $rdet, $oa, $od, $com);";
        cmd.Parameters.AddWithValue("$id", response.Id);
        cmd.Parameters.AddWithValue("$clid", response.ClaimId.Value);
        cmd.Parameters.AddWithValue("$resp", response.Responder);
        cmd.Parameters.AddWithValue("$rd", response.ResponseDate.ToString("O"));
        cmd.Parameters.AddWithValue("$rt", response.ResponseType);
        cmd.Parameters.AddWithValue("$rdet", response.ResponseDetail);
        cmd.Parameters.AddWithValue("$oa", response.OfferedAmount);
        cmd.Parameters.AddWithValue("$od", response.OfferedDays);
        cmd.Parameters.AddWithValue("$com", response.Comment);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ClaimResponse?> GetResponseAsync(ClaimId claimId, CancellationToken ct = default)
    {
        using var c = ProfessionalDbHelpers.Open(_db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {SqliteContractChangeTable.ClaimResponses} WHERE claim_id=$clid ORDER BY response_date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$clid", claimId.Value);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new ClaimResponse
            {
                Id = reader.GetString(0),
                ClaimId = ClaimId.From(reader.GetString(1)),
                Responder = reader.IsDBNull(2) ? "" : reader.GetString(2),
                ResponseDate = DateTime.Parse(reader.GetString(3)),
                ResponseType = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ResponseDetail = reader.IsDBNull(5) ? "" : reader.GetString(5),
                OfferedAmount = reader.GetDecimal(6),
                OfferedDays = reader.GetInt32(7),
                Comment = reader.IsDBNull(8) ? "" : reader.GetString(8)
            };
        }
        return null;
    }

    private static async Task<List<ContractClaim>> ReadClaimListAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var list = new List<ContractClaim>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var j = reader.GetString(0);
            var item = JsonSerializer.Deserialize<ContractClaim>(j, ProfessionalDbHelpers.JsonOptions);
            if (item != null) list.Add(item);
        }
        return list;
    }
}
