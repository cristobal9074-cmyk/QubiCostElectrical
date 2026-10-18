using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class SqliteRevisionStore
{
    internal static void SaveVersiones(SqliteConnection connection, SqliteTransaction transaction, List<BudgetVersionRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO versiones (id, sort_order, codigo, nombre, estado, json, updated_at)
            VALUES ($id, $sort_order, $codigo, $nombre, $estado, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                codigo = excluded.codigo,
                nombre = excluded.nombre,
                Estado = excluded.Estado,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$codigo", SqliteType.Text);
        cmd.Parameters.Add("$nombre", SqliteType.Text);
        cmd.Parameters.Add("$estado", SqliteType.Text);
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
            cmd.Parameters["$estado"].Value = row.Estado ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }

    internal static void SaveOrdenesCambio(SqliteConnection connection, SqliteTransaction transaction, List<ChangeOrderRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO ordenes_cambio (id, sort_order, numero, titulo, estado, delta_costo_uf, delta_venta_uf, json, updated_at)
            VALUES ($id, $sort_order, $numero, $titulo, $estado, $delta_costo_uf, $delta_venta_uf, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                numero = excluded.numero,
                titulo = excluded.titulo,
                Estado = excluded.Estado,
                delta_costo_uf = excluded.delta_costo_uf,
                delta_venta_uf = excluded.delta_venta_uf,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$numero", SqliteType.Text);
        cmd.Parameters.Add("$titulo", SqliteType.Text);
        cmd.Parameters.Add("$estado", SqliteType.Text);
        cmd.Parameters.Add("$delta_costo_uf", SqliteType.Real);
        cmd.Parameters.Add("$delta_venta_uf", SqliteType.Real);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(row.Numero)) row.Numero = $"OC-{i + 1:000}";
            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$numero"].Value = row.Numero ?? "";
            cmd.Parameters["$titulo"].Value = row.Titulo ?? "";
            cmd.Parameters["$estado"].Value = row.Estado ?? EstadoPartida.Borrador;
            cmd.Parameters["$delta_costo_uf"].Value = (double)row.DeltaCostoUf;
            cmd.Parameters["$delta_venta_uf"].Value = (double)row.DeltaVentaUf;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }
}
