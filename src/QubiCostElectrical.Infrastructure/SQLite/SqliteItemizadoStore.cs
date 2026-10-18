using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class SqliteItemizadoStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, List<ItemizadoRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO itemizado (id, sort_order, item, descripcion, unidad, cantidad, es_titulo, json, updated_at)
            VALUES ($id, $sort_order, $item, $descripcion, $unidad, $cantidad, $es_titulo, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                item = excluded.item,
                descripcion = excluded.descripcion,
                unidad = excluded.unidad,
                cantidad = excluded.cantidad,
                es_titulo = excluded.es_titulo,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        SqliteStoreHelpers.AddCommonParameters(cmd, "$id", "$sort_order", "$json", "$updated_at");
        cmd.Parameters.Add("$item", SqliteType.Text);
        cmd.Parameters.Add("$descripcion", SqliteType.Text);
        cmd.Parameters.Add("$unidad", SqliteType.Text);
        cmd.Parameters.Add("$cantidad", SqliteType.Real);
        cmd.Parameters.Add("$es_titulo", SqliteType.Integer);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            SqliteStoreHelpers.SetCommonParameters(cmd, row.Id, i, row, now);
            cmd.Parameters["$item"].Value = row.Item ?? "";
            cmd.Parameters["$descripcion"].Value = row.Descripcion ?? "";
            cmd.Parameters["$unidad"].Value = row.Unidad ?? "";
            cmd.Parameters["$cantidad"].Value = (double)row.Cantidad;
            cmd.Parameters["$es_titulo"].Value = row.EsTitulo ? 1 : 0;
            cmd.ExecuteNonQuery();
        }
    }
}
