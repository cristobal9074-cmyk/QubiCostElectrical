using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static partial class SqliteCubicacionStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, List<CubicacionRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO cubicacion (id, sort_order, item_id, codigo, descripcion, unidad, cantidad, mat_uf, mo_uf, mat_clp, mo_clp, area, json, updated_at)
            VALUES ($id, $sort_order, $item_id, $codigo, $descripcion, $unidad, $cantidad, $mat_uf, $mo_uf, $mat_clp, $mo_clp, $area, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                item_id = excluded.item_id,
                codigo = excluded.codigo,
                descripcion = excluded.descripcion,
                unidad = excluded.unidad,
                cantidad = excluded.cantidad,
                mat_uf = excluded.mat_uf,
                mo_uf = excluded.mo_uf,
                mat_clp = excluded.mat_clp,
                mo_clp = excluded.mo_clp,
                area = excluded.area,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$codigo", SqliteType.Text);
        cmd.Parameters.Add("$descripcion", SqliteType.Text);
        cmd.Parameters.Add("$unidad", SqliteType.Text);
        cmd.Parameters.Add("$cantidad", SqliteType.Real);
        cmd.Parameters.Add("$mat_uf", SqliteType.Real);
        cmd.Parameters.Add("$mo_uf", SqliteType.Real);
        cmd.Parameters.Add("$mat_clp", SqliteType.Real);
        cmd.Parameters.Add("$mo_clp", SqliteType.Real);
        cmd.Parameters.Add("$area", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$item_id"].Value = row.ItemId ?? "";
            cmd.Parameters["$codigo"].Value = row.Codigo ?? "";
            cmd.Parameters["$descripcion"].Value = row.Descripcion ?? "";
            cmd.Parameters["$unidad"].Value = row.Unidad ?? "";
            cmd.Parameters["$cantidad"].Value = (double)row.Cantidad;
            cmd.Parameters["$mat_uf"].Value = (double)row.MatUf;
            cmd.Parameters["$mo_uf"].Value = (double)row.MoUf;
            cmd.Parameters["$mat_clp"].Value = (double)row.MatClp;
            cmd.Parameters["$mo_clp"].Value = (double)row.MoClp;
            cmd.Parameters["$area"].Value = row.Area ?? "General";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SavePlantillas(SqliteConnection connection, SqliteTransaction transaction, List<TemplateRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO plantillas (nombre, sort_order, descripcion, unidad_base, area, json, updated_at)
            VALUES ($nombre, $sort_order, $descripcion, $unidad_base, $area, $json, $updated_at)
            ON CONFLICT(nombre) DO UPDATE SET
                sort_order = excluded.sort_order,
                descripcion = excluded.descripcion,
                unidad_base = excluded.unidad_base,
                area = excluded.area,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$nombre", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$descripcion", SqliteType.Text);
        cmd.Parameters.Add("$unidad_base", SqliteType.Text);
        cmd.Parameters.Add("$area", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            string name = string.IsNullOrWhiteSpace(row.Nombre) ? $"Plantilla {i + 1}" : row.Nombre.Trim();
            while (!used.Add(name)) name = name + "_" + used.Count.ToString("000");
            row.Nombre = name;

            cmd.Parameters["$nombre"].Value = row.Nombre;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$descripcion"].Value = row.Descripcion ?? "";
            cmd.Parameters["$unidad_base"].Value = row.UnidadBase ?? "";
            cmd.Parameters["$area"].Value = row.Area ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveConjuntos(SqliteConnection connection, SqliteTransaction transaction, List<ConjuntoElectricoRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO conjuntos_electricos (id, sort_order, codigo, nombre, tipo, json, updated_at)
            VALUES ($id, $sort_order, $codigo, $nombre, $tipo, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                codigo = excluded.codigo,
                nombre = excluded.nombre,
                tipo = excluded.tipo,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$codigo", SqliteType.Text);
        cmd.Parameters.Add("$nombre", SqliteType.Text);
        cmd.Parameters.Add("$tipo", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$codigo"].Value = row.Codigo ?? "";
            cmd.Parameters["$nombre"].Value = row.Nombre ?? "";
            cmd.Parameters["$tipo"].Value = row.TipoConjunto ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveRecursos(SqliteConnection connection, SqliteTransaction transaction, List<CubicacionRecursoRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO cubicacion_recursos (id, item_id, sort_order, json, updated_at)
            VALUES ($id, $item_id, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                item_id = excluded.item_id,
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$item_id"].Value = row.ItemId ?? "";
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveZonaCantidades(SqliteConnection connection, SqliteTransaction transaction, List<CubicacionZonaCantidadRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO cubicacion_zona_cantidades (id, recurso_id, item_id, sort_order, json, updated_at)
            VALUES ($id, $recurso_id, $item_id, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                recurso_id = excluded.recurso_id,
                item_id = excluded.item_id,
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$recurso_id", SqliteType.Text);
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$recurso_id"].Value = row.RecursoId ?? "";
            cmd.Parameters["$item_id"].Value = row.ItemId ?? "";
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveValorizaciones(SqliteConnection connection, SqliteTransaction transaction, List<CubicacionValorizacionRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO cubicacion_valorizaciones (id, recurso_id, item_id, json, updated_at)
            VALUES ($id, $recurso_id, $item_id, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                recurso_id = excluded.recurso_id,
                item_id = excluded.item_id,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$recurso_id", SqliteType.Text);
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$recurso_id"].Value = row.RecursoId ?? "";
            cmd.Parameters["$item_id"].Value = row.ItemId ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveQtyHeaders(SqliteConnection connection, SqliteTransaction transaction, Dictionary<string, string[]> headers, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO qty_headers (item_id, json, updated_at)
            VALUES ($item_id, $json, $updated_at)
            ON CONFLICT(item_id) DO UPDATE SET
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        foreach (var kv in headers)
        {
            if (string.IsNullOrWhiteSpace(kv.Key)) continue;
            cmd.Parameters["$item_id"].Value = kv.Key;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(kv.Value ?? Array.Empty<string>(), SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveZonas(SqliteConnection connection, SqliteTransaction transaction, List<ZonaProyecto> rows, string now)
    {
        // Purgar todos los registros anteriores para evitar dejar zonas huérfanas en la base de datos
        // cuando el usuario reduce el número de zonas del proyecto.
        using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM zonas;";
            deleteCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO zonas (id, sort_order, json, updated_at)
            VALUES ($id, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveHojaCubicacion(SqliteConnection connection, SqliteTransaction transaction, List<HojaCubicacionItem> rows, string now)
    {
        // Purgar y re-insertar todo, igual que Zonas: evita dejar ítems huérfanos cuando el
        // usuario borra un ítem de la Hoja de Cubicación.
        using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM hoja_cubicacion;";
            deleteCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO hoja_cubicacion (id, sort_order, json, updated_at)
            VALUES ($id, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveSolicitudes(SqliteConnection connection, SqliteTransaction transaction, List<SolicitudItemRow> rows, string now)
    {
        using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM solicitudes;";
            deleteCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO solicitudes (id, sort_order, json, updated_at)
            VALUES ($id, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveMediciones(SqliteConnection connection, SqliteTransaction transaction, List<MeasurementLineRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO mediciones (id, item_id, cubicacion_row_id, zona_offset, sort_order, json, updated_at)
            VALUES ($id, $item_id, $cubicacion_row_id, $zona_offset, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                item_id = excluded.item_id,
                cubicacion_row_id = excluded.cubicacion_row_id,
                zona_offset = excluded.zona_offset,
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$cubicacion_row_id", SqliteType.Text);
        cmd.Parameters.Add("$zona_offset", SqliteType.Integer);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$item_id"].Value = row.ItemId ?? "";
            cmd.Parameters["$cubicacion_row_id"].Value = row.CubicacionRowId ?? "";
            cmd.Parameters["$zona_offset"].Value = row.ZonaOffset;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveSC(SqliteConnection connection, SqliteTransaction transaction, List<CuadroCargaSCRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO cubicacion_sc (id, sort_order, nombre, json, updated_at)
            VALUES ($id, $sort_order, $nombre, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                nombre = excluded.nombre,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$nombre", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$nombre"].Value = row.Nombre ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveScConfig(SqliteConnection connection, SqliteTransaction transaction, CubicacionScConfig config, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO cubicacion_sc_config (id, json, updated_at)
            VALUES (1, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(config, SqliteStoreHelpers.JsonOptions));
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.ExecuteNonQuery();
    }

    internal static Dictionary<string, string[]> LoadQtyHeaders(SqliteConnection connection)
    {
        var result = new Dictionary<string, string[]>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT item_id, json FROM qty_headers ORDER BY item_id;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string itemId = reader.GetString(0);
            result[itemId] = JsonSerializer.Deserialize<string[]>(reader.GetString(1), SqliteStoreHelpers.JsonOptions) ?? Array.Empty<string>();
        }
        return result;
    }

    internal static CubicacionScConfig LoadScConfig(SqliteConnection connection)
    {
        if (!SqliteStoreHelpers.TableExists(connection, "cubicacion_sc_config")) return CubicacionScConfig.Default();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM cubicacion_sc_config WHERE id = 1;";
        object? value = cmd.ExecuteScalar();
        if (value == null || value == DBNull.Value) return CubicacionScConfig.Default();

        return JsonSerializer.Deserialize<CubicacionScConfig>(Convert.ToString(value) ?? "{}", SqliteStoreHelpers.JsonOptions)
            ?? CubicacionScConfig.Default();
    }
}
