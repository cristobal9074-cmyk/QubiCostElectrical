using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteApuVersionRepository : IApuVersionRepository
{
    private readonly string _databasePath;

    public SqliteApuVersionRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException("databasePath");
    }

    public async Task<ApuVersion?> GetByIdAsync(string versionId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_versions WHERE version_id = $id;";
        cmd.Parameters.AddWithValue("$id", versionId);
        return (await cmd.ExecuteScalarAsync() is string json) ? JsonSerializer.Deserialize<ApuVersion>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<IReadOnlyList<ApuVersion>> GetByApuIdAsync(string apuId)
    {
        List<ApuVersion> rows = new List<ApuVersion>();
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_versions WHERE apu_id = $aid ORDER BY version_number DESC;";
        cmd.Parameters.AddWithValue("$aid", apuId);
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ApuVersion? apuVersion = JsonSerializer.Deserialize<ApuVersion>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (apuVersion != null)
            {
                rows.Add(apuVersion);
            }
        }
        return rows.AsReadOnly();
    }

    public async Task<IReadOnlyList<ApuVersion>> GetAllAsync()
    {
        List<ApuVersion> rows = new List<ApuVersion>();
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_versions ORDER BY apu_id, version_number;";
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ApuVersion? apuVersion = JsonSerializer.Deserialize<ApuVersion>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (apuVersion != null)
            {
                rows.Add(apuVersion);
            }
        }
        return rows.AsReadOnly();
    }

    public async Task SaveAsync(ApuVersion version)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteTransaction transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            string json = JsonSerializer.Serialize(version, ProfessionalDbHelpers.JsonOptions);
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO professional_apu_versions (version_id, apu_id, code, name, description, category,\n    unit, status, version_number, previous_version_id, author, change_reason, owner, tags,\n    is_active, compat_metadata, apu_engine_version, economic_engine_version,\n    calculated_direct_cost, calculated_final_price, calculated_currency, effective_date,\n    integrity_hash, notes, audit_ref, created_at, json)\nVALUES ($vid, $aid, $code, $name, $desc, $cat, $unit, $status, $vnum, $prev, $author,\n    $creason, $owner, $tags, $active, $cmeta, $apueng, $ecoeng, $ddcost, $fprice, $ccur,\n    $edate, $ihash, $notes, $aref, $created_at, $json)\nON CONFLICT(version_id) DO UPDATE SET\n     status=excluded.status, change_reason=excluded.change_reason, notes=excluded.notes,\n     calculated_direct_cost=excluded.calculated_direct_cost,\n     calculated_final_price=excluded.calculated_final_price,\n     integrity_hash=excluded.integrity_hash, json=excluded.json;";
            AddVersionParameters(cmd, version, json);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteAsync(string versionId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteTransaction transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM professional_apu_versions WHERE version_id = $id;";
            cmd.Parameters.AddWithValue("$id", versionId);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string versionId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM professional_apu_versions WHERE version_id = $id;";
        cmd.Parameters.AddWithValue("$id", versionId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> GetNextVersionNumberAsync(string apuId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version_number), 0) + 1 FROM professional_apu_versions WHERE apu_id = $aid;";
        cmd.Parameters.AddWithValue("$aid", apuId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static void AddVersionParameters(SqliteCommand cmd, ApuVersion v, string json)
    {
        cmd.Parameters.AddWithValue("$vid", v.VersionId);
        cmd.Parameters.AddWithValue("$aid", v.ApuId);
        cmd.Parameters.AddWithValue("$code", v.Code);
        cmd.Parameters.AddWithValue("$name", v.Name);
        cmd.Parameters.AddWithValue("$desc", v.Description);
        cmd.Parameters.AddWithValue("$cat", v.Category);
        cmd.Parameters.AddWithValue("$unit", v.Unit);
        cmd.Parameters.AddWithValue("$status", v.Status.ToString());
        cmd.Parameters.AddWithValue("$vnum", v.VersionNumber);
        cmd.Parameters.AddWithValue("$prev", v.PreviousVersionId);
        cmd.Parameters.AddWithValue("$author", v.Author);
        cmd.Parameters.AddWithValue("$creason", v.ChangeReason);
        cmd.Parameters.AddWithValue("$owner", v.Owner);
        cmd.Parameters.AddWithValue("$tags", v.Tags);
        cmd.Parameters.AddWithValue("$active", v.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$cmeta", v.CompatMetadata);
        cmd.Parameters.AddWithValue("$apueng", v.ApuEngineVersion);
        cmd.Parameters.AddWithValue("$ecoeng", v.EconomicEngineVersion);
        cmd.Parameters.AddWithValue("$ddcost", v.CalculatedDirectCost);
        cmd.Parameters.AddWithValue("$fprice", v.CalculatedFinalPrice);
        cmd.Parameters.AddWithValue("$ccur", v.CalculatedCurrency);
        cmd.Parameters.AddWithValue("$edate", v.EffectiveDate.ToString("O"));
        cmd.Parameters.AddWithValue("$ihash", v.IntegrityHash);
        cmd.Parameters.AddWithValue("$notes", v.Notes);
        cmd.Parameters.AddWithValue("$aref", v.AuditRef);
        cmd.Parameters.AddWithValue("$created_at", v.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }
}
