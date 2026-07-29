using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteApuStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, List<ApuRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO apu (id, sort_order, item_id, descripcion, unidad, precio_unit_uf, estado, fuente_precio, proveedor, fecha_precio, json, updated_at)
            VALUES ($id, $sort_order, $item_id, $descripcion, $unidad, $precio_unit_uf, $estado, $fuente_precio, $proveedor, $fecha_precio, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                item_id = excluded.item_id,
                descripcion = excluded.descripcion,
                unidad = excluded.unidad,
                precio_unit_uf = excluded.precio_unit_uf,
                estado = excluded.estado,
                fuente_precio = excluded.fuente_precio,
                proveedor = excluded.proveedor,
                fecha_precio = excluded.fecha_precio,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$descripcion", SqliteType.Text);
        cmd.Parameters.Add("$unidad", SqliteType.Text);
        cmd.Parameters.Add("$precio_unit_uf", SqliteType.Real);
        cmd.Parameters.Add("$estado", SqliteType.Text);
        cmd.Parameters.Add("$fuente_precio", SqliteType.Text);
        cmd.Parameters.Add("$proveedor", SqliteType.Text);
        cmd.Parameters.Add("$fecha_precio", SqliteType.Text);
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
            cmd.Parameters["$descripcion"].Value = row.Descripcion ?? "";
            cmd.Parameters["$unidad"].Value = row.Unidad ?? "";
            cmd.Parameters["$precio_unit_uf"].Value = (double)row.PrecioUnitUf;
            cmd.Parameters["$estado"].Value = row.Estado ?? EstadoRecurso.OK;
            cmd.Parameters["$fuente_precio"].Value = row.FuentePrecio ?? "";
            cmd.Parameters["$proveedor"].Value = row.Proveedor ?? "";
            cmd.Parameters["$fecha_precio"].Value = (row.FechaPrecio == DateTime.MinValue ? DateTime.Today : row.FechaPrecio).ToString("yyyy-MM-dd");
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveVinculos(SqliteConnection connection, SqliteTransaction transaction, List<PartidaApuVinculoRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO partida_apu_vinculos (item_id, apu_id, json, updated_at)
            VALUES ($item_id, $apu_id, $json, $updated_at)
            ON CONFLICT(item_id) DO UPDATE SET
                apu_id = excluded.apu_id,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$item_id", SqliteType.Text);
        cmd.Parameters.Add("$apu_id", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ItemId)) continue;
            cmd.Parameters["$item_id"].Value = row.ItemId;
            cmd.Parameters["$apu_id"].Value = row.ApuId ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }
}
