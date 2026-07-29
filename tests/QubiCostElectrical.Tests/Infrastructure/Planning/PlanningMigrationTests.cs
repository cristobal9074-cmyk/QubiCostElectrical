using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure.Planning;

public sealed class PlanningMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public PlanningMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"planning_mig_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadWriteCreate;");
        conn.Open();
        return conn;
    }

    private void SetSchemaVersion(int version)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private int ReadSchemaVersion()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private bool TableExists(string name)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", name);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    [Fact]
    public void MigrationFrom30_CreatesPlanningTables()
    {
        SetSchemaVersion(30);
        using var conn = OpenConnection();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        Assert.True(TableExists("project_schedules"));
        Assert.True(TableExists("planning_wbs_nodes"));
        Assert.True(TableExists("planning_activities"));
    }

    [Fact]
    public void MigrationFrom30_CreatesCalendarTables()
    {
        SetSchemaVersion(30);
        using var conn = OpenConnection();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        Assert.True(TableExists("planning_calendars"));
    }

    [Fact]
    public void MigrationFrom30_CreatesLinksAndAuditTables()
    {
        SetSchemaVersion(30);
        using var conn = OpenConnection();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        Assert.True(TableExists("planning_audit"));
    }

    [Fact]
    public void SchemaVersion_Is34_AfterMigration()
    {
        SetSchemaVersion(30);
        using var conn = OpenConnection();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, ReadSchemaVersion());
    }

    [Fact]
    public void Migration_Idempotent_RunningTwiceKeepsVersion34()
    {
        SetSchemaVersion(30);
        using var conn = OpenConnection();
        SQLiteSchemaMigrator.MigrateToLatest(conn);
        using var conn2 = OpenConnection();
        SQLiteSchemaMigrator.MigrateToLatest(conn2);
        conn2.Close();
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, ReadSchemaVersion());
    }

    [Fact]
    public void DataSurvives_AcrossMigration()
    {
        SetSchemaVersion(30);
        using (var conn = OpenConnection())
        {
            SQLiteSchemaMigrator.MigrateToLatest(conn);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO project_schedules (id, name, description, time_zone_id, default_calendar_id, status, version, created_at, modified_at, row_version, json) VALUES ($id, $n, '', 'UTC', '', 'Draft', 1, '2026-01-01T00:00:00', '2026-01-01T00:00:00', 'v1', '{}');";
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("$n", "Survived");
            cmd.ExecuteNonQuery();
        }
        using var conn2 = OpenConnection();
        using var cmd2 = conn2.CreateCommand();
        cmd2.CommandText = "SELECT name FROM project_schedules WHERE name = 'Survived';";
        var result = cmd2.ExecuteScalar();
        Assert.Equal("Survived", result);
    }
}
