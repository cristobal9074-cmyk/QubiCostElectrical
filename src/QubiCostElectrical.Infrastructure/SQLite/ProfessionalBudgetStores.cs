using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteResourceTable
{
    internal const string TableName = "professional_resources";
    internal const string CreateSql = """
        CREATE TABLE IF NOT EXISTS professional_resources (
            id TEXT PRIMARY KEY,
            code TEXT NOT NULL,
            description TEXT NOT NULL DEFAULT '',
            type TEXT NOT NULL DEFAULT 'Material',
            unit TEXT NOT NULL DEFAULT 'UN',
            base_price REAL NOT NULL DEFAULT 0,
            currency TEXT NOT NULL DEFAULT 'CLP',
            price_date TEXT,
            supplier TEXT NOT NULL DEFAULT '',
            yield REAL NOT NULL DEFAULT 1,
            waste REAL NOT NULL DEFAULT 0,
            adjustment_factor REAL NOT NULL DEFAULT 1,
            is_active INTEGER NOT NULL DEFAULT 1,
            notes TEXT NOT NULL DEFAULT '',
            price_source TEXT NOT NULL DEFAULT '',
            created_by TEXT NOT NULL DEFAULT '',
            modified_by TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            modified_at TEXT NOT NULL,
            json TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_professional_resources_code ON professional_resources(code);
        CREATE INDEX IF NOT EXISTS idx_professional_resources_type ON professional_resources(type);
        CREATE INDEX IF NOT EXISTS idx_professional_resources_active ON professional_resources(is_active);
        """;
}

internal static class SqlitePriceHistoryTable
{
    internal const string TableName = "professional_price_history";
    internal const string CreateSql = """
        CREATE TABLE IF NOT EXISTS professional_price_history (
            id TEXT PRIMARY KEY,
            resource_id TEXT NOT NULL,
            price REAL NOT NULL,
            currency TEXT NOT NULL DEFAULT 'CLP',
            effective_date TEXT NOT NULL,
            supplier TEXT NOT NULL DEFAULT '',
            source TEXT NOT NULL DEFAULT '',
            user_id TEXT NOT NULL DEFAULT '',
            comment TEXT NOT NULL DEFAULT '',
            origin_project TEXT NOT NULL DEFAULT '',
            status TEXT NOT NULL DEFAULT 'Vigente',
            created_at TEXT NOT NULL,
            json TEXT NOT NULL,
            FOREIGN KEY (resource_id) REFERENCES professional_resources(id)
        );
        CREATE INDEX IF NOT EXISTS idx_price_history_resource ON professional_price_history(resource_id);
        CREATE INDEX IF NOT EXISTS idx_price_history_date ON professional_price_history(effective_date);
        """;
}

internal static class SqliteExchangeRateTable
{
    internal const string TableName = "professional_exchange_rates";
    internal const string CreateSql = """
        CREATE TABLE IF NOT EXISTS professional_exchange_rates (
            id TEXT PRIMARY KEY,
            source_currency TEXT NOT NULL,
            target_currency TEXT NOT NULL,
            rate REAL NOT NULL,
            effective_date TEXT NOT NULL,
            source TEXT NOT NULL DEFAULT '',
            user_id TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            json TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_exchange_rates_pair ON professional_exchange_rates(source_currency, target_currency);
        CREATE INDEX IF NOT EXISTS idx_exchange_rates_date ON professional_exchange_rates(effective_date);
        """;
}

internal static class SqliteApuVersionTable
{
    internal const string TableName = "professional_apu_versions";
    internal const string CreateSql = """
        CREATE TABLE IF NOT EXISTS professional_apu_versions (
            version_id TEXT PRIMARY KEY,
            apu_id TEXT NOT NULL,
            code TEXT NOT NULL DEFAULT '',
            name TEXT NOT NULL DEFAULT '',
            description TEXT NOT NULL DEFAULT '',
            category TEXT NOT NULL DEFAULT '',
            unit TEXT NOT NULL DEFAULT 'UN',
            status TEXT NOT NULL DEFAULT 'Draft',
            version_number INTEGER NOT NULL DEFAULT 1,
            previous_version_id TEXT NOT NULL DEFAULT '',
            author TEXT NOT NULL DEFAULT '',
            change_reason TEXT NOT NULL DEFAULT '',
            owner TEXT NOT NULL DEFAULT '',
            tags TEXT NOT NULL DEFAULT '',
            is_active INTEGER NOT NULL DEFAULT 1,
            compat_metadata TEXT NOT NULL DEFAULT '',
            apu_engine_version TEXT NOT NULL DEFAULT '1.0.0',
            economic_engine_version TEXT NOT NULL DEFAULT '1.0.0',
            calculated_direct_cost REAL NOT NULL DEFAULT 0,
            calculated_final_price REAL NOT NULL DEFAULT 0,
            calculated_currency TEXT NOT NULL DEFAULT 'CLP',
            effective_date TEXT NOT NULL,
            integrity_hash TEXT NOT NULL DEFAULT '',
            notes TEXT NOT NULL DEFAULT '',
            audit_ref TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL,
            json TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_apu_versions_apu_id ON professional_apu_versions(apu_id);
        CREATE INDEX IF NOT EXISTS idx_apu_versions_status ON professional_apu_versions(status);
        CREATE INDEX IF NOT EXISTS idx_apu_versions_active ON professional_apu_versions(is_active);
        """;
}

internal static class SqliteApuSnapshotTable
{
    internal const string TableName = "professional_apu_snapshots";
    internal const string CreateSql = """
        CREATE TABLE IF NOT EXISTS professional_apu_snapshots (
            snapshot_id TEXT PRIMARY KEY,
            source_version_id TEXT NOT NULL,
            captured_at TEXT NOT NULL,
            version_number INTEGER NOT NULL,
            author TEXT NOT NULL DEFAULT '',
            captured_by TEXT NOT NULL DEFAULT '',
            change_reason TEXT NOT NULL DEFAULT '',
            reason TEXT NOT NULL DEFAULT '',
            total_direct_cost REAL NOT NULL DEFAULT 0,
            total_final_price REAL NOT NULL DEFAULT 0,
            currency TEXT NOT NULL DEFAULT 'CLP',
            component_count INTEGER NOT NULL DEFAULT 0,
            resource_ref_count INTEGER NOT NULL DEFAULT 0,
            economic_concept_count INTEGER NOT NULL DEFAULT 0,
            scenario_count INTEGER NOT NULL DEFAULT 0,
            integrity_hash TEXT NOT NULL DEFAULT '',
            json TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_apu_snapshots_source ON professional_apu_snapshots(source_version_id);
        """;
}

internal static class SqliteApuCatalogTable
{
    internal const string TableName = "professional_apu_catalog";
    internal const string CreateSql = """
        CREATE TABLE IF NOT EXISTS professional_apu_catalog (
            apu_id TEXT PRIMARY KEY,
            code TEXT NOT NULL DEFAULT '',
            name TEXT NOT NULL DEFAULT '',
            description TEXT NOT NULL DEFAULT '',
            category TEXT NOT NULL DEFAULT '',
            unit TEXT NOT NULL DEFAULT 'UN',
            version_count INTEGER NOT NULL DEFAULT 0,
            active_version_number INTEGER NOT NULL DEFAULT 0,
            active_version_id TEXT NOT NULL DEFAULT '',
            active_version_status TEXT NOT NULL DEFAULT 'Draft',
            last_modified_at TEXT NOT NULL,
            last_price REAL NOT NULL DEFAULT 0,
            approved_version_id TEXT NOT NULL DEFAULT '',
            approved_version_number INTEGER NOT NULL DEFAULT 0,
            owner TEXT NOT NULL DEFAULT '',
            tags TEXT NOT NULL DEFAULT '',
            is_active INTEGER NOT NULL DEFAULT 1,
            json TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS idx_apu_catalog_code ON professional_apu_catalog(code);
        CREATE INDEX IF NOT EXISTS idx_apu_catalog_active ON professional_apu_catalog(is_active);
        CREATE INDEX IF NOT EXISTS idx_apu_catalog_category ON professional_apu_catalog(category);
        """;
}

internal static class ProfessionalDbHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    internal static SqliteConnection Open(string databasePath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using (var cmd = connection.CreateCommand()) { cmd.CommandText = "PRAGMA journal_mode=WAL;"; cmd.ExecuteNonQuery(); }
        using (var cmd = connection.CreateCommand()) { cmd.CommandText = "PRAGMA synchronous=NORMAL;"; cmd.ExecuteNonQuery(); }
        using (var cmd = connection.CreateCommand()) { cmd.CommandText = "PRAGMA foreign_keys=ON;"; cmd.ExecuteNonQuery(); }

        return connection;
    }

    internal static SqliteTransaction BeginWriteTransaction(SqliteConnection connection)
        => connection.BeginTransaction(IsolationLevel.Serializable);

    internal static void EnsureTables(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = SqliteResourceTable.CreateSql + SqlitePriceHistoryTable.CreateSql
            + SqliteExchangeRateTable.CreateSql + SqliteApuVersionTable.CreateSql
            + SqliteApuSnapshotTable.CreateSql + SqliteApuCatalogTable.CreateSql;
        cmd.ExecuteNonQuery();
    }

    internal static async Task EnsureTablesAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = SqliteResourceTable.CreateSql + SqlitePriceHistoryTable.CreateSql
            + SqliteExchangeRateTable.CreateSql + SqliteApuVersionTable.CreateSql
            + SqliteApuSnapshotTable.CreateSql + SqliteApuCatalogTable.CreateSql;
        await cmd.ExecuteNonQueryAsync();
    }
}

public sealed class SqliteResourceRepository : IResourceRepository
{
    private readonly string _databasePath;

    public SqliteResourceRepository(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    public async Task<List<Resource>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = new List<Resource>();
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_resources ORDER BY code;";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var row = JsonSerializer.Deserialize<Resource>(reader.GetString(0), ProfessionalDbHelpers.JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    public async Task<Resource?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_resources WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<Resource>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task<Resource?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM professional_resources WHERE code = $code;";
        cmd.Parameters.AddWithValue("$code", code);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string json ? JsonSerializer.Deserialize<Resource>(json, ProfessionalDbHelpers.JsonOptions) : null;
    }

    public async Task SaveAsync(Resource resource, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO professional_resources (id, code, description, type, unit, base_price, currency,
                    price_date, supplier, yield, waste, adjustment_factor, is_active, notes, price_source,
                    created_by, modified_by, created_at, modified_at, json)
                VALUES ($id, $code, $desc, $type, $unit, $price, $currency, $pdate, $supplier, $yield,
                    $waste, $adj, $active, $notes, $psource, $cby, $mby, $cat, $mat, $json)
                ON CONFLICT(id) DO UPDATE SET
                    code=excluded.code, description=excluded.description, type=excluded.type,
                    unit=excluded.unit, base_price=excluded.base_price, currency=excluded.currency,
                    price_date=excluded.price_date, supplier=excluded.supplier, yield=excluded.yield,
                    waste=excluded.waste, adjustment_factor=excluded.adjustment_factor,
                    is_active=excluded.is_active, notes=excluded.notes, price_source=excluded.price_source,
                    modified_by=excluded.modified_by, modified_at=excluded.modified_at, json=excluded.json;
                """;
            AddResourceParameters(cmd, resource);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var transaction = ProfessionalDbHelpers.BeginWriteTransaction(connection);
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "DELETE FROM professional_resources WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(ct);
            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> ExistsByCodeAsync(string code, string? excludeId = null, CancellationToken ct = default)
    {
        using var connection = ProfessionalDbHelpers.Open(_databasePath);
        ProfessionalDbHelpers.EnsureTables(connection);
        using var cmd = connection.CreateCommand();
        if (excludeId != null)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM professional_resources WHERE code = $code AND id != $exclude;";
            cmd.Parameters.AddWithValue("$exclude", excludeId);
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM professional_resources WHERE code = $code;";
        }
        cmd.Parameters.AddWithValue("$code", code);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
    }

    private static void AddResourceParameters(SqliteCommand cmd, Resource r)
    {
        var json = JsonSerializer.Serialize(r, ProfessionalDbHelpers.JsonOptions);
        cmd.Parameters.AddWithValue("$id", r.Id);
        cmd.Parameters.AddWithValue("$code", r.Code);
        cmd.Parameters.AddWithValue("$desc", r.Description);
        cmd.Parameters.AddWithValue("$type", r.Type.ToString());
        cmd.Parameters.AddWithValue("$unit", r.Unit);
        cmd.Parameters.AddWithValue("$price", r.BasePrice);
        cmd.Parameters.AddWithValue("$currency", r.Currency);
        cmd.Parameters.AddWithValue("$pdate", r.PriceDate?.ToString("O") ?? "");
        cmd.Parameters.AddWithValue("$supplier", r.Supplier);
        cmd.Parameters.AddWithValue("$yield", r.Yield);
        cmd.Parameters.AddWithValue("$waste", r.Waste);
        cmd.Parameters.AddWithValue("$adj", r.AdjustmentFactor);
        cmd.Parameters.AddWithValue("$active", r.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$notes", r.Notes);
        cmd.Parameters.AddWithValue("$psource", r.PriceSource);
        cmd.Parameters.AddWithValue("$cby", r.CreatedBy);
        cmd.Parameters.AddWithValue("$mby", r.ModifiedBy);
        cmd.Parameters.AddWithValue("$cat", r.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$mat", r.ModifiedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$json", json);
    }
}
