using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteContractVersionRepository : IContractVersionRepository
{
    private readonly string _db;

    public SqliteContractVersionRepository(string databasePath)
    {
        _db = databasePath ?? throw new ArgumentNullException("databasePath");
    }

    public async Task SaveAsync(ContractVersion version, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "\r\n            INSERT INTO contract_versions (id, contract_id, version_number, is_approved, hash, json_data)\r\n            VALUES ($id, $cid, $vnum, $app, $hash, $json)\r\n            ON CONFLICT(id) DO UPDATE SET contract_id=excluded.contract_id, version_number=excluded.version_number, is_approved=excluded.is_approved, hash=excluded.hash, json_data=excluded.json_data;";
        cmd.Parameters.AddWithValue("$id", version.Id.Value);
        cmd.Parameters.AddWithValue("$cid", version.ContractId.Value);
        cmd.Parameters.AddWithValue("$vnum", version.VersionNumber);
        cmd.Parameters.AddWithValue("$app", version.IsApproved ? 1 : 0);
        cmd.Parameters.AddWithValue("$hash", version.Hash);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(version, ProfessionalDbHelpers.JsonOptions));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContractVersion?> GetByIdAsync(ContractVersionId id, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_versions WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.Value);
        return (await cmd.ExecuteScalarAsync(ct) is string json) ? JsonSerializer.Deserialize<ContractVersion>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<List<ContractVersion>> GetByContractAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT json_data FROM contract_versions WHERE contract_id=$cid ORDER BY version_number ASC;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        List<ContractVersion> list = new List<ContractVersion>();
        using SqliteDataReader r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            ContractVersion? contractVersion = JsonSerializer.Deserialize<ContractVersion>(r.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (contractVersion != null)
            {
                list.Add(contractVersion);
            }
        }
        return list;
    }

    public async Task<int> GetLatestVersionNumberAsync(ContractId contractId, CancellationToken ct = default(CancellationToken))
    {
        using SqliteConnection c = ProfessionalDbHelpers.Open(_db);
        using SqliteCommand cmd = c.CreateCommand();
        cmd.CommandText = "SELECT MAX(version_number) FROM contract_versions WHERE contract_id=$cid;";
        cmd.Parameters.AddWithValue("$cid", contractId.Value);
        object? obj = await cmd.ExecuteScalarAsync(ct);
        return (obj != DBNull.Value && obj != null) ? Convert.ToInt32(obj) : 0;
    }
}
