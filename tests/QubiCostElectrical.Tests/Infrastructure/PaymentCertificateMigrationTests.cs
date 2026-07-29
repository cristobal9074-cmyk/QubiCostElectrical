using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Verifica que las migraciones v52-v54 crean las tablas de PaymentCertificate
/// y que los datos persisten correctamente después de la migración.
/// </summary>
public sealed class PaymentCertificateMigrationTests : IDisposable
{
    private readonly string _dbPath;
    private const int LatestSchemaVersion = 54;

    public PaymentCertificateMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_pc_mig_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDeleteWithRetries(_dbPath);
    }

    [Fact]
    public void Migration052_CreatesPaymentCertificateTables()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration052_PaymentCertificates(c, t);
        t.Commit();

        Assert.True(TableExists("payment_certificates"));
        Assert.True(TableExists("payment_certificate_lines"));
        Assert.True(TableExists("payment_certificate_revisions"));
        Assert.True(TableExists("payment_certificate_audit"));
    }

    [Fact]
    public void Migration052_TableSchemas_HaveRequiredColumns()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration052_PaymentCertificates(c, t);
        t.Commit();

        Assert.True(ColumnExists("payment_certificates", "id"));
        Assert.True(ColumnExists("payment_certificates", "contract_id"));
        Assert.True(ColumnExists("payment_certificates", "visible_number"));
        Assert.True(ColumnExists("payment_certificates", "status"));
        Assert.True(ColumnExists("payment_certificates", "json_data"));
        Assert.True(ColumnExists("payment_certificates", "row_version"));
        Assert.True(ColumnExists("payment_certificates", "hash"));

        Assert.True(ColumnExists("payment_certificate_lines", "id"));
        Assert.True(ColumnExists("payment_certificate_lines", "payment_certificate_id"));
        Assert.True(ColumnExists("payment_certificate_lines", "description"));
        Assert.True(ColumnExists("payment_certificate_lines", "json_data"));

        Assert.True(ColumnExists("payment_certificate_revisions", "id"));
        Assert.True(ColumnExists("payment_certificate_revisions", "payment_certificate_id"));
        Assert.True(ColumnExists("payment_certificate_revisions", "snapshot_json"));

        Assert.True(ColumnExists("payment_certificate_audit", "id"));
        Assert.True(ColumnExists("payment_certificate_audit", "action"));
        Assert.True(ColumnExists("payment_certificate_audit", "actor"));
    }

    [Fact]
    public void Migration052_IsIdempotent()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration052_PaymentCertificates(c, t);
        SqliteMigrationSteps.Migration052_PaymentCertificates(c, t); // segunda ejecución
        t.Commit();

        Assert.True(TableExists("payment_certificates"));
        Assert.True(TableExists("payment_certificate_lines"));
        Assert.True(TableExists("payment_certificate_revisions"));
        Assert.True(TableExists("payment_certificate_audit"));
    }

    [Fact]
    public void Migration053_CreatesAdvancesAndRetentionsTables()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration053_ContractAdvancesRetentions(c, t);
        t.Commit();

        Assert.True(TableExists("contract_advances"));
        Assert.True(TableExists("contract_advance_amortizations"));
        Assert.True(TableExists("contract_retentions"));
        Assert.True(TableExists("contract_retention_applications"));
        Assert.True(TableExists("contract_retention_releases"));
    }

    [Fact]
    public void Migration053_AdvancesTable_HasRequiredColumns()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration053_ContractAdvancesRetentions(c, t);
        t.Commit();

        Assert.True(ColumnExists("contract_advances", "id"));
        Assert.True(ColumnExists("contract_advances", "contract_id"));
        Assert.True(ColumnExists("contract_advances", "amount"));
        Assert.True(ColumnExists("contract_advances", "status"));
        Assert.True(ColumnExists("contract_advances", "json_data"));

        Assert.True(ColumnExists("contract_retentions", "id"));
        Assert.True(ColumnExists("contract_retentions", "contract_id"));
        Assert.True(ColumnExists("contract_retentions", "percentage"));
        Assert.True(ColumnExists("contract_retentions", "status"));
        Assert.True(ColumnExists("contract_retentions", "json_data"));
    }

    [Fact]
    public void Migration053_IsIdempotent()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration053_ContractAdvancesRetentions(c, t);
        SqliteMigrationSteps.Migration053_ContractAdvancesRetentions(c, t);
        t.Commit();

        Assert.True(TableExists("contract_advances"));
        Assert.True(TableExists("contract_advance_amortizations"));
        Assert.True(TableExists("contract_retentions"));
        Assert.True(TableExists("contract_retention_applications"));
        Assert.True(TableExists("contract_retention_releases"));
    }

    [Fact]
    public void Migration054_CreatesObservationsAndSnapshotsTables()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration054_PaymentObservationsSnapshots(c, t);
        t.Commit();

        Assert.True(TableExists("contract_payment_observations"));
        Assert.True(TableExists("contract_payment_snapshots"));
    }

    [Fact]
    public void Migration054_ObservationsTable_HasRequiredColumns()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration054_PaymentObservationsSnapshots(c, t);
        t.Commit();

        Assert.True(ColumnExists("contract_payment_observations", "id"));
        Assert.True(ColumnExists("contract_payment_observations", "payment_certificate_id"));
        Assert.True(ColumnExists("contract_payment_observations", "status"));
        Assert.True(ColumnExists("contract_payment_observations", "comment"));
        Assert.True(ColumnExists("contract_payment_observations", "json_data"));

        Assert.True(ColumnExists("contract_payment_snapshots", "id"));
        Assert.True(ColumnExists("contract_payment_snapshots", "payment_certificate_id"));
        Assert.True(ColumnExists("contract_payment_snapshots", "snapshot_json"));
        Assert.True(ColumnExists("contract_payment_snapshots", "hash"));
    }

    [Fact]
    public void Migration054_IsIdempotent()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration054_PaymentObservationsSnapshots(c, t);
        SqliteMigrationSteps.Migration054_PaymentObservationsSnapshots(c, t);
        t.Commit();

        Assert.True(TableExists("contract_payment_observations"));
        Assert.True(TableExists("contract_payment_snapshots"));
    }

    [Fact]
    public void AllMigrations_ApplySequentially_WithoutError()
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var t = c.BeginTransaction();

        SqliteMigrationSteps.Migration052_PaymentCertificates(c, t);
        SqliteMigrationSteps.Migration053_ContractAdvancesRetentions(c, t);
        SqliteMigrationSteps.Migration054_PaymentObservationsSnapshots(c, t);
        t.Commit();

        Assert.True(TableExists("payment_certificates"));
        Assert.True(TableExists("payment_certificate_lines"));
        Assert.True(TableExists("payment_certificate_revisions"));
        Assert.True(TableExists("payment_certificate_audit"));
        Assert.True(TableExists("contract_advances"));
        Assert.True(TableExists("contract_advance_amortizations"));
        Assert.True(TableExists("contract_retentions"));
        Assert.True(TableExists("contract_retention_applications"));
        Assert.True(TableExists("contract_retention_releases"));
        Assert.True(TableExists("contract_payment_observations"));
        Assert.True(TableExists("contract_payment_snapshots"));
    }

    [Fact]
    public void LatestSchemaVersion_MatchesProductVersionInfo()
    {
        Assert.True(SQLiteSchemaMigrator.LatestSchemaVersion >= 54,
            "La versión del esquema debe ser al menos 54 (PaymentCertificates).");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private bool TableExists(string tableName)
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    private bool ColumnExists(string tableName, string columnName)
    {
        using var c = ProfessionalDbHelpers.Open(_dbPath);
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void TryDeleteWithRetries(string path)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!File.Exists(path)) return;
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch { return; }
        }
    }
}
