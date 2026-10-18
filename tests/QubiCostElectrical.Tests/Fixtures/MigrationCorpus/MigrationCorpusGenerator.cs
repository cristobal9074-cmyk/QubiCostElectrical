using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using QubiCostElectrical.Infrastructure;

namespace QubiCostElectrical.Tests.Fixtures.MigrationCorpus;

/// <summary>
/// Generador del corpus de bases de datos SQLite históricas para validación de migraciones.
/// Crea archivos SQLite reales en el disco con el DDL y datos exactos de cada hito histórico,
/// permitiendo probar transiciones reales de esquema en CI sin requerir trucos de PRAGMA.
/// </summary>
public static class MigrationCorpusGenerator
{
    public static void CreateSchemaV1LegacyDatabase(string dbPath, ProjectFileData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using (var transaction = connection.BeginTransaction())
        {
            // DDL exacto de versión 1
            SqliteMigrationSteps.Migration001_BaseInicial(connection, transaction);

            // En v1 no existía PRAGMA user_version, sino app_meta schema_version = 1
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO app_meta (key, value) VALUES ('schema_version', '1');";
                cmd.ExecuteNonQuery();
            }

            // Insertar datos reales de v1
            InsertProjectInfoV1(connection, transaction, data);
            InsertItemizadoV1(connection, transaction, data);
            InsertMaterialesV1(connection, transaction, data);
            InsertCubicacionV1(connection, transaction, data);

            transaction.Commit();
        }
    }

    public static void CreateSchemaV14Database(string dbPath, ProjectFileData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // Aplicar migraciones 1..14 en orden
        for (int v = 1; v <= 14; v++)
        {
            using var tx = connection.BeginTransaction();
            ApplyStep(connection, tx, v);
            SqliteMigratorHelpers.SetUserVersion(connection, v, tx);
            tx.Commit();
        }

        // Insertar datos en tablas legacy que v15 debe eliminar
        using (var tx = connection.BeginTransaction())
        {
            SqliteMigratorHelpers.Execute(connection, tx, """
                INSERT INTO workspace_layouts (nombre, json, updated_at) VALUES ('layout-1', '{"panel":"left"}', '2026-01-01T00:00:00Z');
                INSERT INTO partida_schedules (item_id, json, updated_at) VALUES ('sch-1', '{"days":5}', '2026-01-01T00:00:00Z');
                """);

            InsertProjectInfoV1(connection, tx, data);
            InsertItemizadoV1(connection, tx, data);
            InsertMaterialesV1(connection, tx, data);
            InsertCubicacionV1(connection, tx, data);
            tx.Commit();
        }
    }

    public static void CreateHistoricalDatabase(string dbPath, int targetVersion, ProjectFileData data)
    {
        if (targetVersion < 1 || targetVersion > 33)
            throw new ArgumentOutOfRangeException(nameof(targetVersion));

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        for (int v = 1; v <= targetVersion; v++)
        {
            using var tx = connection.BeginTransaction();
            ApplyStep(connection, tx, v);
            SqliteMigratorHelpers.SetUserVersion(connection, v, tx);
            tx.Commit();
        }

        using (var tx = connection.BeginTransaction())
        {
            InsertProjectInfoV1(connection, tx, data);
            InsertItemizadoV1(connection, tx, data);
            InsertMaterialesV1(connection, tx, data);
            InsertCubicacionV1(connection, tx, data);
            tx.Commit();
        }
    }

    private static void ApplyStep(SqliteConnection connection, SqliteTransaction transaction, int version)
    {
        switch (version)
        {
            case 1: SqliteMigrationSteps.Migration001_BaseInicial(connection, transaction); break;
            case 2: SqliteMigrationSteps.Migration002_Versiones(connection, transaction); break;
            case 3: SqliteMigrationSteps.Migration003_Apu(connection, transaction); break;
            case 4: SqliteMigrationSteps.Migration004_OrdenesCambio(connection, transaction); break;
            case 5: SqliteMigrationSteps.Migration005_CompatibilidadVersionada(connection, transaction); break;
            case 6: SqliteMigrationSteps.Migration006_CartaPresentacion(connection, transaction); break;
            case 7: SqliteMigrationSteps.Migration007_MotorCostos(connection, transaction); break;
            case 8: SqliteMigrationSteps.Migration008_ApuCostingMode(connection, transaction); break;
            case 9: SqliteMigrationSteps.Migration009_ApuProfesional(connection, transaction); break;
            case 10: SqliteMigrationSteps.Migration010_TrazabilidadPrecios(connection, transaction); break;
            case 11: SqliteMigrationSteps.Migration011_CatalogoMaterialesPro(connection, transaction); break;
            case 12: SqliteMigrationSteps.Migration012_PerformanceIndexes(connection, transaction); break;
            case 13: SqliteMigrationSteps.Migration013_ProfessionalSearchIndexes(connection, transaction); break;
            case 14: SqliteMigrationSteps.Migration014_SeparateCollections(connection, transaction); break;
            case 15: SqliteMigrationSteps.Migration015_DropUnusedCollections(connection, transaction); break;
            case 16: SqliteMigrationSteps.Migration016_ZonasYMediciones(connection, transaction); break;
            case 17: SqliteMigrationSteps.Migration017_CubicacionSC(connection, transaction); break;
            case 18: SqliteMigrationSteps.Migration018_AntecedentesTecnicos(connection, transaction); break;
            case 19: SqliteMigrationSteps.Migration019_ConjuntosElectricos(connection, transaction); break;
            case 20: SqliteMigrationSteps.Migration020_CubicacionComposicion(connection, transaction); break;
            case 21: SqliteMigrationSteps.Migration021_ProjectAuditLog(connection, transaction); break;
            case 22: SqliteMigrationSteps.Migration022_CubicacionCellCommentsRowId(connection, transaction); break;
            case 23: SqliteMigrationSteps.Migration023_HojaCubicacion(connection, transaction); break;
            case 24: SqliteMigrationSteps.Migration024_Solicitudes(connection, transaction); break;
            case 25: SqliteMigrationSteps.Migration025_MatClpMoClpColumns(connection, transaction); break;
            case 26: SqliteMigrationSteps.Migration026_MoDotacionGastosOperacionales(connection, transaction); break;
            case 27: SqliteMigrationSteps.Migration027_EppBudget(connection, transaction); break;
            case 28: SqliteMigrationSteps.Migration028_MoProjectionTasks(connection, transaction); break;
            case 29: SqliteMigrationSteps.Migration029_NegotiatedDiscounts(connection, transaction); break;
            case 30: SqliteMigrationSteps.Migration030_ClosingPolicy(connection, transaction); break;
            case 31: SqliteMigrationSteps.Migration031_StaffingPlan(connection, transaction); break;
            case 32: SqliteMigrationSteps.Migration032_Contracts(connection, transaction); break;
            case 33: SqliteMigrationSteps.Migration033_EstadosPago(connection, transaction); break;
        }
    }

    private static void InsertProjectInfoV1(SqliteConnection connection, SqliteTransaction transaction, ProjectFileData data)
    {
        var p = data.Proyecto ?? new ProjectInfoRow { Nombre = "Proyecto Test" };
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO project_info (id, nombre, cliente, moneda, json, updated_at)
            VALUES (1, $nombre, $cliente, $moneda, $json, $updated_at);
            """;
        cmd.Parameters.AddWithValue("$nombre", p.Nombre);
        cmd.Parameters.AddWithValue("$cliente", p.Cliente);
        cmd.Parameters.AddWithValue("$moneda", p.MonedaPresupuesto);
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(p, SqliteStoreHelpers.JsonOptions));
        cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private static void InsertItemizadoV1(SqliteConnection connection, SqliteTransaction transaction, ProjectFileData data)
    {
        int sort = 0;
        foreach (var item in data.Itemizado)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO itemizado (id, sort_order, item, descripcion, unidad, cantidad, es_titulo, json, updated_at)
                VALUES ($id, $sort_order, $item, $descripcion, $unidad, $cantidad, $es_titulo, $json, $updated_at);
                """;
            cmd.Parameters.AddWithValue("$id", item.Id);
            cmd.Parameters.AddWithValue("$sort_order", sort++);
            cmd.Parameters.AddWithValue("$item", item.Item ?? "");
            cmd.Parameters.AddWithValue("$descripcion", item.Descripcion ?? "");
            cmd.Parameters.AddWithValue("$unidad", item.Unidad ?? "");
            cmd.Parameters.AddWithValue("$cantidad", (double)item.Cantidad);
            cmd.Parameters.AddWithValue("$es_titulo", item.EsTitulo ? 1 : 0);
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(item, SqliteStoreHelpers.JsonOptions));
            cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertMaterialesV1(SqliteConnection connection, SqliteTransaction transaction, ProjectFileData data)
    {
        int sort = 0;
        foreach (var mat in data.Materiales)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO materiales (codigo, sort_order, descripcion, unidad, categoria, subcategoria, subcategoria2, tipo_instalacion, especificacion_tecnica, mat_uf, mo_uf, json, updated_at)
                VALUES ($codigo, $sort_order, $descripcion, $unidad, $categoria, '', '', '', '', $mat_uf, $mo_uf, $json, $updated_at);
                """;
            cmd.Parameters.AddWithValue("$codigo", mat.Codigo);
            cmd.Parameters.AddWithValue("$sort_order", sort++);
            cmd.Parameters.AddWithValue("$descripcion", mat.Descripcion ?? "");
            cmd.Parameters.AddWithValue("$unidad", mat.Unidad ?? "");
            cmd.Parameters.AddWithValue("$categoria", mat.Categoria ?? "");
            cmd.Parameters.AddWithValue("$mat_uf", (double)mat.MatUf);
            cmd.Parameters.AddWithValue("$mo_uf", (double)mat.MoUf);
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(mat, SqliteStoreHelpers.JsonOptions));
            cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }

    private static void InsertCubicacionV1(SqliteConnection connection, SqliteTransaction transaction, ProjectFileData data)
    {
        int sort = 0;
        foreach (var cub in data.Cubicacion)
        {
            using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO cubicacion (id, sort_order, item_id, codigo, descripcion, unidad, cantidad, mat_uf, mo_uf, area, json, updated_at)
                VALUES ($id, $sort_order, $item_id, $codigo, $descripcion, $unidad, $cantidad, $mat_uf, $mo_uf, $area, $json, $updated_at);
                """;
            cmd.Parameters.AddWithValue("$id", cub.Id);
            cmd.Parameters.AddWithValue("$sort_order", sort++);
            cmd.Parameters.AddWithValue("$item_id", cub.ItemId ?? "");
            cmd.Parameters.AddWithValue("$codigo", cub.Codigo ?? "");
            cmd.Parameters.AddWithValue("$descripcion", cub.Descripcion ?? "");
            cmd.Parameters.AddWithValue("$unidad", cub.Unidad ?? "");
            cmd.Parameters.AddWithValue("$cantidad", (double)cub.Cantidad);
            cmd.Parameters.AddWithValue("$mat_uf", (double)cub.MatUf);
            cmd.Parameters.AddWithValue("$mo_uf", (double)cub.MoUf);
            cmd.Parameters.AddWithValue("$area", cub.Area ?? "");
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(cub, SqliteStoreHelpers.JsonOptions));
            cmd.Parameters.AddWithValue("$updated_at", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }
}
