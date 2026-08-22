using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Verifica que las migraciones SQLite no rompen proyectos existentes.
/// </summary>
public sealed class SQLiteMigrationTests : IDisposable
{
    private readonly string _dbPath;

    public SQLiteMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pe_migration_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDeleteWithRetries(_dbPath);
    }

    [Fact]
    public void LoadSnapshot_AfterMigration_PreservesProjectData()
    {
        // Guardar un proyecto en versión actual
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        // Retroceder el user_version para forzar re-migración en el siguiente Load
        RollbackUserVersionTo(14);

        // LoadSnapshot debe aplicar la migración pendiente y cargar los datos intactos
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
        Assert.Equal(original.Cubicacion.Count, loaded.Cubicacion.Count);
        Assert.Equal(original.Materiales.Count, loaded.Materiales.Count);
        Assert.Equal(original.Proyecto?.Nombre, loaded.Proyecto?.Nombre);
    }

    [Fact]
    public void Migration015_DropsUnusedTables_WithoutRemovingCoreData()
    {
        // Guardar datos en estado actual
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);

        // Retroceder a v14 y agregar las tablas "antiguas" que v15 va a borrar
        RollbackUserVersionTo(14);
        CreateLegacyTables();

        // LoadSnapshot ejecuta Migration015: DROP TABLE IF EXISTS esas tablas
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        // Los datos del proyecto deben estar intactos
        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
        Assert.Equal(original.Cubicacion.Count, loaded.Cubicacion.Count);

        // Las tablas eliminadas no deben existir
        Assert.False(TableExists("workspace_layouts"));
        Assert.False(TableExists("partida_schedules"));
        Assert.False(TableExists("cubicacion_attachments"));
        Assert.False(TableExists("offer_digital_signatures"));
    }

    [Fact]
    public void IsDatabaseHealthy_AfterMigration_ReturnsTrue()
    {
        SQLiteProjectStore.SaveSnapshot(_dbPath, BuildTestData());
        RollbackUserVersionTo(13);
        SQLiteProjectStore.LoadSnapshot(_dbPath); // dispara migración
        Assert.True(SQLiteProjectBackup.IsDatabaseHealthy(_dbPath));
    }

    [Fact]
    public void LoadSnapshot_FromForeignHigherSchemaVersion_StillCreatesOwnTables()
    {
        // Simula un proyecto guardado por OTRA rama de la app que subió su propio contador de
        // esquema por encima del de esta build (p.ej. v59) para features que esta build no tiene.
        // Antes del fix, MigrateToLatest se saltaba TODAS las migraciones propias al ver un
        // número más alto, dejando tablas nuevas (mo_projection_tasks, etc.) sin crear.
        var original = BuildTestData();
        SQLiteProjectStore.SaveSnapshot(_dbPath, original);
        SetUserVersionDirect(59);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(original.Proyecto?.Nombre, loaded.Proyecto?.Nombre);
        Assert.Equal(original.Itemizado.Count, loaded.Itemizado.Count);
        Assert.True(TableExists("mo_projection_tasks"));
        Assert.True(TableExists("negotiated_discounts"));
        Assert.True(TableExists("closing_policy"));
        Assert.True(TableExists("staffing_plan"));

        // El marcador de versión ajeno no se toca: sigue en 59, no se rebaja a 31.
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        Assert.Equal(59L, Convert.ToInt64(cmd.ExecuteScalar()));
    }

    [Fact]
    public void Schema_LatestVersion_MatchesProductVersionInfo()
    {
        Assert.Equal(ProductVersionInfo.DatabaseSchemaVersion, SQLiteSchemaMigrator.LatestSchemaVersion);
    }

    [Fact]
    public void Migration022_BackfillsCubicacionRowId_FromLegacyVisualRow()
    {
        var data = BuildTestData();
        data.Proyecto!.CubicacionCellComments = new List<CubicacionCellCommentRow>
        {
            // Comentario "legacy": solo tiene VisualRow, sin CubicacionRowId (como antes de v22).
            new() { Id = "COM001", ItemId = "P001", VisualRow = 0, ColumnKey = "row", Comment = "Revisar cantidad" }
        };
        SQLiteProjectStore.SaveSnapshot(_dbPath, data);

        RollbackUserVersionTo(21);

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        var comment = Assert.Single(loaded.Proyecto!.CubicacionCellComments);
        Assert.Equal("C001", comment.CubicacionRowId);
        Assert.Equal("Revisar cantidad", comment.Comment);
    }

    [Fact]
    public void SaveAndLoad_PersistsMatClpAndMoClp_OnMaterialesAndCubicacion()
    {
        var data = BuildTestData();
        data.Materiales[0].MatClp = 12500m;
        data.Materiales[0].MoClp = 3800m;
        data.Cubicacion[0].MatClp = 12500m;
        data.Cubicacion[0].MoClp = 3800m;

        SQLiteProjectStore.SaveSnapshot(_dbPath, data);
        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);

        Assert.Equal(12500m, loaded.Materiales[0].MatClp);
        Assert.Equal(3800m, loaded.Materiales[0].MoClp);
        Assert.Equal(12500m, loaded.Cubicacion[0].MatClp);
        Assert.Equal(3800m, loaded.Cubicacion[0].MoClp);
        Assert.True(ColumnExists("materiales", "mat_clp"));
        Assert.True(ColumnExists("materiales", "mo_clp"));
        Assert.True(ColumnExists("cubicacion", "mat_clp"));
        Assert.True(ColumnExists("cubicacion", "mo_clp"));
    }

    [Fact]
    public void Migration025_BackfillsMatClpFromJson_WhenColumnsWereMissing()
    {
        var data = BuildTestData();
        data.Materiales[0].MatClp = 9900m;
        data.Materiales[0].MoClp = 2100m;
        data.Cubicacion[0].MatClp = 9900m;
        data.Cubicacion[0].MoClp = 2100m;
        SQLiteProjectStore.SaveSnapshot(_dbPath, data);

        // Simula base previa a v25: columnas CLP a 0 pero JSON con MatClp/MoClp.
        RollbackUserVersionTo(24);
        using (var conn = new SqliteConnection($"Data Source={_dbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE materiales SET mat_clp = 0, mo_clp = 0;
                UPDATE cubicacion SET mat_clp = 0, mo_clp = 0;
                """;
            cmd.ExecuteNonQuery();
        }

        var loaded = SQLiteProjectStore.LoadSnapshot(_dbPath);
        Assert.Equal(9900m, loaded.Materiales[0].MatClp);
        Assert.Equal(2100m, loaded.Materiales[0].MoClp);
        Assert.Equal(9900m, loaded.Cubicacion[0].MatClp);
        Assert.Equal(2100m, loaded.Cubicacion[0].MoClp);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private void RollbackUserVersionTo(int version)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        // PRAGMA user_version no acepta parámetros — usar interpolación literal (valor entero, seguro)
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private void SetUserVersionDirect(int version)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private void CreateLegacyTables()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS workspace_layouts (id TEXT PRIMARY KEY, json TEXT);
            CREATE TABLE IF NOT EXISTS partida_schedules (id TEXT PRIMARY KEY, json TEXT);
            CREATE TABLE IF NOT EXISTS cubicacion_attachments (id TEXT PRIMARY KEY, json TEXT);
            CREATE TABLE IF NOT EXISTS offer_digital_signatures (id TEXT PRIMARY KEY, json TEXT);
            """;
        cmd.ExecuteNonQuery();
    }

    private bool TableExists(string tableName)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
    }

    private bool ColumnExists(string tableName, string columnName)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static ProjectFileData BuildTestData() => new()
    {
        Proyecto = new ProjectInfoRow { Nombre = "Migración Test", Cliente = "Cliente Test" },
        Itemizado = new List<ItemizadoRow>
        {
            new() { Id = "P001", Item = "1.1", Descripcion = "Partida de prueba", Unidad = "GL", Cantidad = 1m, EsTitulo = false }
        },
        Cubicacion = new List<CubicacionRow>
        {
            new() { Id = "C001", ItemId = "P001", Tipo = "MAT", Codigo = "MAT001", Descripcion = "Material prueba", Unidad = "UN", Cantidad = 5m, MatUf = 1m }
        },
        Materiales = new List<MaterialRow>
        {
            new() { Codigo = "MAT001", Descripcion = "Material prueba", Unidad = "UN", MatUf = 1m }
        },
        QtyHeadersByItem = new Dictionary<string, string[]>()
    };

    private static void TryDeleteWithRetries(string path)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                System.Threading.Thread.Sleep(100);
            }
            catch
            {
                return;
            }
        }
    }
}
