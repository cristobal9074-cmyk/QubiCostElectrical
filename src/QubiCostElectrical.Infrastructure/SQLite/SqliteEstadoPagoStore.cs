using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

/// <summary>
/// Persistencia de estados de pago (fase Ejecución): filas mensuales del cronograma de
/// cobros + la fila de configuración global (id='config') en la misma tabla
/// <c>estados_pago_mes</c> creada en Migration033. La tabla legacy <c>estados_pago</c>
/// (Migration014, feature retirada sin datos activos) no se toca.
/// </summary>
internal static class SqliteEstadoPagoStore
{
    internal const string ConfigRowId = "config";

    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, List<EstadoPagoMesRow> rows, EstadoPagoConfig config, string now)
    {
        rows ??= new List<EstadoPagoMesRow>();
        config ??= EstadoPagoConfig.Default();

        using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = $"DELETE FROM estados_pago_mes WHERE id <> '{ConfigRowId}';";
            deleteCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO estados_pago_mes (id, sort_order, anio, mes, estado, json, updated_at)
            VALUES ($id, $sort_order, $anio, $mes, $estado, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                anio = excluded.anio,
                mes = excluded.mes,
                estado = excluded.estado,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$id", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$anio", SqliteType.Integer);
        cmd.Parameters.Add("$mes", SqliteType.Integer);
        cmd.Parameters.Add("$estado", SqliteType.Text);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.Equals(row.Id, ConfigRowId, StringComparison.OrdinalIgnoreCase))
                continue;

            cmd.Parameters["$id"].Value = row.Id;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$anio"].Value = row.Anio;
            cmd.Parameters["$mes"].Value = row.Mes;
            cmd.Parameters["$estado"].Value = row.Estado ?? "";
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }

        cmd.Parameters["$id"].Value = ConfigRowId;
        cmd.Parameters["$sort_order"].Value = -1;
        cmd.Parameters["$anio"].Value = config.Anio;
        cmd.Parameters["$mes"].Value = 0;
        cmd.Parameters["$estado"].Value = "";
        cmd.Parameters["$json"].Value = JsonSerializer.Serialize(config, SqliteStoreHelpers.JsonOptions);
        cmd.Parameters["$updated_at"].Value = now;
        cmd.ExecuteNonQuery();
    }

    internal static List<EstadoPagoMesRow> Load(SqliteConnection connection)
        => SqliteStoreHelpers.ReadJsonRows<EstadoPagoMesRow>(
            connection,
            $"SELECT json FROM estados_pago_mes WHERE id <> '{ConfigRowId}' ORDER BY anio, mes;");

    internal static EstadoPagoConfig LoadConfig(SqliteConnection connection)
    {
        if (!SqliteStoreHelpers.TableExists(connection, "estados_pago_mes"))
            return EstadoPagoConfig.Default();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT json FROM estados_pago_mes WHERE id = '{ConfigRowId}';";
        object? value = cmd.ExecuteScalar();
        if (value == null || value == DBNull.Value)
            return EstadoPagoConfig.Default();

        try
        {
            return JsonSerializer.Deserialize<EstadoPagoConfig>(Convert.ToString(value) ?? "{}", SqliteStoreHelpers.JsonOptions)
                ?? EstadoPagoConfig.Default();
        }
        catch (JsonException)
        {
            return EstadoPagoConfig.Default();
        }
    }
}
