using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed class SqlitePriceHistoryRepository : IPriceHistoryRepository
{
    private readonly string _databasePath;

    public SqlitePriceHistoryRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<List<ResourcePriceHistoryEntry>> GetByResourceIdAsync(string resourceId, CancellationToken ct = default)
    {
        var rows = new List<ResourcePriceHistoryEntry>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_price_history WHERE resource_id = $rid ORDER BY effective_date DESC;";
        cmd.Parameters.AddWithValue("$rid", resourceId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<ResourcePriceHistoryEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<ResourcePriceHistoryEntry?> GetLatestAsync(string resourceId, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_price_history WHERE resource_id = $rid ORDER BY effective_date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$rid", resourceId);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ResourcePriceHistoryEntry>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task AddAsync(ResourcePriceHistoryEntry entry, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(entry, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_price_history (id, resource_id, price, currency, effective_date,
                    supplier, source, user_id, comment, origin_project, status, created_at, json)
                VALUES ($id, $rid, $price, $cur, $edate, $supp, $src, $uid, $cmt, $oproj, $status, $cat, $json);
                """;
            cmd.Parameters.AddWithValue("$id", entry.Id);
            cmd.Parameters.AddWithValue("$rid", entry.ResourceId);
            cmd.Parameters.AddWithValue("$price", entry.Price);
            cmd.Parameters.AddWithValue("$cur", entry.Currency);
            cmd.Parameters.AddWithValue("$edate", entry.EffectiveDate.ToString("O"));
            cmd.Parameters.AddWithValue("$supp", entry.Supplier);
            cmd.Parameters.AddWithValue("$src", entry.Source);
            cmd.Parameters.AddWithValue("$uid", entry.UserId);
            cmd.Parameters.AddWithValue("$cmt", entry.Comment);
            cmd.Parameters.AddWithValue("$oproj", entry.OriginProject);
            cmd.Parameters.AddWithValue("$status", entry.Status);
            cmd.Parameters.AddWithValue("$cat", entry.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<List<ResourcePriceHistoryEntry>> GetByDateRangeAsync(string resourceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var rows = new List<ResourcePriceHistoryEntry>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_price_history WHERE resource_id = $rid AND effective_date >= $from AND effective_date <= $to ORDER BY effective_date;";
        cmd.Parameters.AddWithValue("$rid", resourceId);
        cmd.Parameters.AddWithValue("$from", from.ToString("O"));
        cmd.Parameters.AddWithValue("$to", to.ToString("O"));
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<ResourcePriceHistoryEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }
}

public sealed class SqliteExchangeRateRepository : IExchangeRateRepository
{
    private readonly string _databasePath;

    public SqliteExchangeRateRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<List<ExchangeRate>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = new List<ExchangeRate>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_exchange_rates ORDER BY source_currency, target_currency, effective_date DESC;";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<ExchangeRate>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<ExchangeRate?> GetLatestAsync(string source, string target, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_exchange_rates WHERE source_currency = $src AND target_currency = $tgt ORDER BY effective_date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$src", source);
        cmd.Parameters.AddWithValue("$tgt", target);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ExchangeRate>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<ExchangeRate?> GetByDateAsync(string source, string target, DateTime date, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_exchange_rates WHERE source_currency = $src AND target_currency = $tgt AND effective_date <= $dt ORDER BY effective_date DESC LIMIT 1;";
        cmd.Parameters.AddWithValue("$src", source);
        cmd.Parameters.AddWithValue("$tgt", target);
        cmd.Parameters.AddWithValue("$dt", date.ToString("O"));
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<ExchangeRate>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task AddAsync(ExchangeRate rate, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(rate, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_exchange_rates (id, source_currency, target_currency, rate,
                    effective_date, source, user_id, created_at, json)
                VALUES ($id, $src, $tgt, $rate, $edate, $source, $uid, $cat, $json);
                """;
            cmd.Parameters.AddWithValue("$id", rate.Id);
            cmd.Parameters.AddWithValue("$src", rate.SourceCurrency);
            cmd.Parameters.AddWithValue("$tgt", rate.TargetCurrency);
            cmd.Parameters.AddWithValue("$rate", rate.Rate);
            cmd.Parameters.AddWithValue("$edate", rate.Date.ToString("O"));
            cmd.Parameters.AddWithValue("$source", rate.Source);
            cmd.Parameters.AddWithValue("$uid", rate.UserId);
            cmd.Parameters.AddWithValue("$cat", rate.CreatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }
}

public sealed class SqliteApuVersionRepository : IApuVersionRepository
{
    private readonly string _databasePath;

    public SqliteApuVersionRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<ApuVersion?> GetByIdAsync(string versionId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_versions WHERE version_id = $id;";
        cmd.Parameters.AddWithValue("$id", versionId);
        var result = await cmd.ExecuteScalarAsync();
        return result is string json ? JsonSerializer.Deserialize<ApuVersion>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<IReadOnlyList<ApuVersion>> GetByApuIdAsync(string apuId)
    {
        var rows = new List<ApuVersion>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_versions WHERE apu_id = $aid ORDER BY version_number DESC;";
        cmd.Parameters.AddWithValue("$aid", apuId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = JsonSerializer.Deserialize<ApuVersion>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows.AsReadOnly();
    }

    public async Task<IReadOnlyList<ApuVersion>> GetAllAsync()
    {
        var rows = new List<ApuVersion>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_versions ORDER BY apu_id, version_number;";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = JsonSerializer.Deserialize<ApuVersion>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows.AsReadOnly();
    }

    public async Task SaveAsync(ApuVersion version)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(version, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_apu_versions (version_id, apu_id, code, name, description, category,
                    unit, status, version_number, previous_version_id, author, change_reason, owner, tags,
                    is_active, compat_metadata, apu_engine_version, economic_engine_version,
                    calculated_direct_cost, calculated_final_price, calculated_currency, effective_date,
                    integrity_hash, notes, audit_ref, created_at, json)
                VALUES ($vid, $aid, $code, $name, $desc, $cat, $unit, $status, $vnum, $prev, $author,
                    $creason, $owner, $tags, $active, $cmeta, $apueng, $ecoeng, $ddcost, $fprice, $ccur,
                    $edate, $ihash, $notes, $aref, $created_at, $json)
                ON CONFLICT(version_id) DO UPDATE SET
                     status=excluded.status, change_reason=excluded.change_reason, notes=excluded.notes,
                     calculated_direct_cost=excluded.calculated_direct_cost,
                     calculated_final_price=excluded.calculated_final_price,
                     integrity_hash=excluded.integrity_hash, json=excluded.json;
                """;
            AddVersionParameters(cmd, version, json);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(string versionId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM professional_apu_versions WHERE version_id = $id;";
            cmd.Parameters.AddWithValue("$id", versionId);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsAsync(string versionId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM professional_apu_versions WHERE version_id = $id;";
        cmd.Parameters.AddWithValue("$id", versionId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> GetNextVersionNumberAsync(string apuId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
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

public sealed class SqliteApuSnapshotRepository : IApuSnapshotRepository
{
    private readonly string _databasePath;

    public SqliteApuSnapshotRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<ApuVersionSnapshot?> GetByIdAsync(string snapshotId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_snapshots WHERE snapshot_id = $id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        var result = await cmd.ExecuteScalarAsync();
        return result is string json ? JsonSerializer.Deserialize<ApuVersionSnapshot>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<IReadOnlyList<ApuVersionSnapshot>> GetByVersionIdAsync(string versionId)
    {
        var rows = new List<ApuVersionSnapshot>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_snapshots WHERE source_version_id = $vid ORDER BY captured_at DESC;";
        cmd.Parameters.AddWithValue("$vid", versionId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = JsonSerializer.Deserialize<ApuVersionSnapshot>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows.AsReadOnly();
    }

    public async Task SaveAsync(ApuVersionSnapshot snapshot)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(snapshot, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_apu_snapshots (snapshot_id, source_version_id, captured_at,
                    version_number, author, captured_by, change_reason, reason, total_direct_cost,
                    total_final_price, currency, component_count, resource_ref_count,
                    economic_concept_count, scenario_count, integrity_hash, json)
                VALUES ($sid, $svid, $cap, $vnum, $author, $cby, $creason, $reason, $tdc, $tfp,
                    $cur, $cc, $rrc, $ecc, $sc, $ihash, $json)
                ON CONFLICT(snapshot_id) DO NOTHING;
                """;
            cmd.Parameters.AddWithValue("$sid", snapshot.SnapshotId);
            cmd.Parameters.AddWithValue("$svid", snapshot.SourceVersionId);
            cmd.Parameters.AddWithValue("$cap", snapshot.CapturedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$vnum", snapshot.VersionNumber);
            cmd.Parameters.AddWithValue("$author", snapshot.Author);
            cmd.Parameters.AddWithValue("$cby", snapshot.CapturedBy);
            cmd.Parameters.AddWithValue("$creason", snapshot.ChangeReason ?? "");
            cmd.Parameters.AddWithValue("$reason", snapshot.Reason ?? "");
            cmd.Parameters.AddWithValue("$tdc", snapshot.TotalDirectCost);
            cmd.Parameters.AddWithValue("$tfp", snapshot.TotalFinalPrice);
            cmd.Parameters.AddWithValue("$cur", snapshot.Currency);
            cmd.Parameters.AddWithValue("$cc", snapshot.ComponentCount);
            cmd.Parameters.AddWithValue("$rrc", snapshot.ResourceRefCount);
            cmd.Parameters.AddWithValue("$ecc", snapshot.EconomicConceptCount);
            cmd.Parameters.AddWithValue("$sc", snapshot.ScenarioCount);
            cmd.Parameters.AddWithValue("$ihash", snapshot.IntegrityHash ?? "");
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsAsync(string snapshotId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM professional_apu_snapshots WHERE snapshot_id = $id;";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<ApuVersionSnapshot?> GetLatestByApuIdAsync(string apuId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT s.json FROM professional_apu_snapshots s
            INNER JOIN professional_apu_versions v ON s.source_version_id = v.version_id
            WHERE v.apu_id = $aid
            ORDER BY s.captured_at DESC LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$aid", apuId);
        var result = await cmd.ExecuteScalarAsync();
        return result is string json ? JsonSerializer.Deserialize<ApuVersionSnapshot>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }
}

public sealed class SqliteApuCatalogRepository : IApuCatalogRepository
{
    private readonly string _databasePath;

    public SqliteApuCatalogRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<ApuCatalogEntry?> GetByApuIdAsync(string apuId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_apu_catalog WHERE apu_id = $id;";
        cmd.Parameters.AddWithValue("$id", apuId);
        var result = await cmd.ExecuteScalarAsync();
        return result is string json ? JsonSerializer.Deserialize<ApuCatalogEntry>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<ApuCatalogSearchResult> SearchAsync(ApuCatalogSearchRequest request)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);

        var conditions = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (request.Filter == ApuCatalogFilter.ActiveOnly)
            conditions.Add("is_active = 1");
        else if (request.Filter == ApuCatalogFilter.OnlyWithApprovedVersion)
            conditions.Add("approved_version_id != ''");
        else if (request.Filter == ApuCatalogFilter.OnlyWithoutApprovedVersion)
            conditions.Add("approved_version_id = ''");

        if (!string.IsNullOrEmpty(request.Category))
        { conditions.Add("category = $cat"); parameters["$cat"] = request.Category; }
        if (!string.IsNullOrEmpty(request.Owner))
        { conditions.Add("owner = $owner"); parameters["$owner"] = request.Owner; }
        if (!string.IsNullOrEmpty(request.Query))
        {
            conditions.Add("(name LIKE $q OR code LIKE $q)");
            parameters["$q"] = $"%{request.Query}%";
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $"SELECT COUNT(*) FROM professional_apu_catalog {where};";
        using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = countSql;
            foreach (var (k, v) in parameters) countCmd.Parameters.AddWithValue(k, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            var querySql = $"SELECT json FROM professional_apu_catalog {where} ORDER BY code LIMIT {request.MaxResults} OFFSET {request.Offset};";
            using var cmd = connection.CreateCommand();
            cmd.CommandText = querySql;
            foreach (var (k, v) in parameters) cmd.Parameters.AddWithValue(k, v);

            var entries = new List<ApuCatalogEntry>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = JsonSerializer.Deserialize<ApuCatalogEntry>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
                if (entry != null) entries.Add(entry);
            }

            var allCountSql = "SELECT COUNT(*) FROM professional_apu_catalog;";
            using var allCmd = connection.CreateCommand();
            allCmd.CommandText = allCountSql;
            var totalAll = Convert.ToInt32(await allCmd.ExecuteScalarAsync());

            return new ApuCatalogSearchResult
            {
                Entries = entries.AsReadOnly(),
                TotalCount = totalAll,
                FilteredCount = total,
                Offset = request.Offset,
                MaxResults = request.MaxResults
            };
        }
    }

    public async Task<ApuCatalogStats> GetStatsAsync()
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                COUNT(*) AS total,
                SUM(CASE WHEN is_active = 1 THEN 1 ELSE 0 END) AS active,
                SUM(CASE WHEN approved_version_id != '' THEN 1 ELSE 0 END) AS approved,
                SUM(CASE WHEN active_version_status = 'Draft' THEN 1 ELSE 0 END) AS draft,
                COALESCE(SUM(version_count), 0) AS versions
            FROM professional_apu_catalog;
            """;
        using var reader = await cmd.ExecuteReaderAsync();
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
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            var json = JsonSerializer.Serialize(entry, ProfessionalDbHelpers.JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_apu_catalog (apu_id, code, name, description, category, unit,
                    version_count, active_version_number, active_version_id, active_version_status,
                    last_modified_at, last_price, approved_version_id, approved_version_number,
                    owner, tags, is_active, json)
                VALUES ($aid, $code, $name, $desc, $cat, $unit, $vc, $avn, $avid, $avs,
                    $lma, $lp, $appid, $appn, $owner, $tags, $active, $json)
                ON CONFLICT(apu_id) DO UPDATE SET
                    code=excluded.code, name=excluded.name, description=excluded.description,
                    category=excluded.category, unit=excluded.unit,
                    version_count=excluded.version_count, active_version_number=excluded.active_version_number,
                    active_version_id=excluded.active_version_id, active_version_status=excluded.active_version_status,
                    last_modified_at=excluded.last_modified_at, last_price=excluded.last_price,
                    approved_version_id=excluded.approved_version_id,
                    approved_version_number=excluded.approved_version_number,
                    owner=excluded.owner, tags=excluded.tags, is_active=excluded.is_active,
                    json=excluded.json;
                """;
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
            cmd.Parameters.AddWithValue("$json", json);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(string apuId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM professional_apu_catalog WHERE apu_id = $id;";
            cmd.Parameters.AddWithValue("$id", apuId);
            await cmd.ExecuteNonQueryAsync();
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsAsync(string apuId)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM professional_apu_catalog WHERE apu_id = $id;";
        cmd.Parameters.AddWithValue("$id", apuId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }
}
