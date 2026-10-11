using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;

namespace QubiCostElectrical.Infrastructure;

public sealed class SqliteApuCatalogRepository : IApuCatalogRepository
{
    private readonly string _databasePath;

    public SqliteApuCatalogRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException("databasePath");
    }

    public async Task<ApuCatalogEntry?> GetByApuIdAsync(string apuId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_catalog WHERE apu_id = $id;";
        cmd.Parameters.AddWithValue("$id", apuId);
        return (await cmd.ExecuteScalarAsync() is string json) ? JsonSerializer.Deserialize<ApuCatalogEntry>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<ApuCatalogSearchResult> SearchAsync(ApuCatalogSearchRequest request)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        List<string> list = new List<string>();
        Dictionary<string, object> parameters = new Dictionary<string, object>();
        if (request.Filter == ApuCatalogFilter.ActiveOnly)
        {
            list.Add("is_active = 1");
        }
        else if (request.Filter == ApuCatalogFilter.OnlyWithApprovedVersion)
        {
            list.Add("approved_version_id != ''");
        }
        else if (request.Filter == ApuCatalogFilter.OnlyWithoutApprovedVersion)
        {
            list.Add("approved_version_id = ''");
        }
        if (!string.IsNullOrEmpty(request.Category))
        {
            list.Add("category = $cat");
            parameters["$cat"] = request.Category;
        }
        if (!string.IsNullOrEmpty(request.Owner))
        {
            list.Add("owner = $owner");
            parameters["$owner"] = request.Owner;
        }
        if (!string.IsNullOrEmpty(request.Query))
        {
            list.Add("(name LIKE $q OR code LIKE $q)");
            parameters["$q"] = "%" + request.Query + "%";
        }
        string where = ((list.Count > 0) ? ("WHERE " + string.Join(" AND ", list)) : "");
        string commandText = "SELECT COUNT(*) FROM professional_apu_catalog " + where + ";";
        using SqliteCommand countCmd = connection.CreateCommand();
        countCmd.CommandText = commandText;
        foreach (KeyValuePair<string, object> item in parameters)
        {
            item.Deconstruct(out string parameterName, out object value2);
            countCmd.Parameters.AddWithValue(parameterName, value2);
        }
        int total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        string commandText2 = $"SELECT json FROM professional_apu_catalog {where} ORDER BY code LIMIT {request.MaxResults} OFFSET {request.Offset};";
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = commandText2;
        foreach (KeyValuePair<string, object> item2 in parameters)
        {
            item2.Deconstruct(out string parameterName2, out object value3);
            cmd.Parameters.AddWithValue(parameterName2, value3);
        }
        List<ApuCatalogEntry> entries = new List<ApuCatalogEntry>();
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ApuCatalogEntry? apuCatalogEntry = JsonSerializer.Deserialize<ApuCatalogEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (apuCatalogEntry != null)
            {
                entries.Add(apuCatalogEntry);
            }
        }
        string commandText3 = "SELECT COUNT(*) FROM professional_apu_catalog;";
        using SqliteCommand allCmd = connection.CreateCommand();
        allCmd.CommandText = commandText3;
        int totalCount = Convert.ToInt32(await allCmd.ExecuteScalarAsync());
        return new ApuCatalogSearchResult
        {
            Entries = entries.AsReadOnly(),
            TotalCount = totalCount,
            FilteredCount = total,
            Offset = request.Offset,
            MaxResults = request.MaxResults
        };
    }

    public async Task<ApuCatalogStats> GetStatsAsync()
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT\n    COUNT(*) AS total,\n    SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END) AS active,\n    SUM(CASE WHEN approved_version_id != '' THEN 1 ELSE 0 END) AS approved,\n    SUM(CASE WHEN active_version_status = 'Draft' THEN 1 ELSE 0 END) AS draft,\n    COALESCE(SUM(version_count), 0) AS versions\nFROM professional_apu_catalog;";
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ApuCatalogStats
            {
                TotalEntries = Convert.ToInt32(reader["total"]),
                ActiveEntries = Convert.ToInt32(reader["active"]),
                WithApprovedVersion = Convert.ToInt32(reader["approved"]),
                WithDraftVersion = Convert.ToInt32(reader["draft"]),
                TotalVersions = Convert.ToInt32(reader["versions"]),
                TotalSnapshots = 0
            };
        }
        return new ApuCatalogStats();
    }

    public async Task SaveAsync(ApuCatalogEntry entry)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteTransaction transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            string value = JsonSerializer.Serialize(entry, ProfessionalDbHelpers.JsonOptions);
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "INSERT INTO professional_apu_catalog (apu_id, code, name, description, category, unit,\n    version_count, active_version_number, active_version_id, active_version_status,\n    last_modified_at, last_price, approved_version_id, approved_version_number,\n    owner, tags, is_active, json)\nVALUES ($aid, $code, $name, $desc, $cat, $unit, $vc, $avn, $avid, $avs,\n    $lma, $lp, $appid, $appn, $owner, $tags, $active, $json)\nON CONFLICT(apu_id) DO UPDATE SET\n    code=excluded.code, name=excluded.name, description=excluded.description,\n    category=excluded.category, unit=excluded.unit,\n    version_count=excluded.version_count, active_version_number=excluded.active_version_number,\n    active_version_id=excluded.active_version_id, active_version_status=excluded.active_version_status,\n    last_modified_at=excluded.last_modified_at, last_price=excluded.last_price,\n    approved_version_id=excluded.approved_version_id,\n    approved_version_number=excluded.approved_version_number,\n    owner=excluded.owner, tags=excluded.tags, is_active=excluded.is_active,\n    json=excluded.json;";
            cmd.Parameters.AddWithValue("$aid", entry.ApuId);
            cmd.Parameters.AddWithValue("$code", entry.Code);
            cmd.Parameters.AddWithValue("$name", entry.Name);
            cmd.Parameters.AddWithValue("$desc", entry.Description);
            cmd.Parameters.AddWithValue("$cat", entry.Category);
            cmd.Parameters.AddWithValue("$unit", entry.Unit);
            cmd.Parameters.AddWithValue("$vc", entry.VersionCount);
            cmd.Parameters.AddWithValue("$avn", entry.ActiveVersionNumber);
            cmd.Parameters.AddWithValue("$avid", entry.ActiveVersionId);
            cmd.Parameters.AddWithValue("$avs", entry.ActiveVersionStatus.ToString());
            cmd.Parameters.AddWithValue("$lma", entry.LastModifiedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$lp", entry.LastPrice);
            cmd.Parameters.AddWithValue("$appid", entry.ApprovedVersionId);
            cmd.Parameters.AddWithValue("$appn", entry.ApprovedVersionNumber);
            cmd.Parameters.AddWithValue("$owner", entry.Owner);
            cmd.Parameters.AddWithValue("$tags", entry.Tags);
            cmd.Parameters.AddWithValue("$active", entry.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("$json", value);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task DeleteAsync(string apuId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteTransaction transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM professional_apu_catalog WHERE apu_id = $id;";
            cmd.Parameters.AddWithValue("$id", apuId);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string apuId)
    {
        using SqliteConnection connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM professional_apu_catalog WHERE apu_id = $id;";
        cmd.Parameters.AddWithValue("$id", apuId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }
}
