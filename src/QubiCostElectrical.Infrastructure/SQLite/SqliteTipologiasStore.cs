using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class SqliteTipologiasStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, TipologiasEdificioData data, string now)
    {
        data ??= TipologiasEdificioData.Default();

        using var createCmd = connection.CreateCommand();
        createCmd.Transaction = transaction;
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS tipologias_edificio (
                id INTEGER PRIMARY KEY,
                json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        createCmd.ExecuteNonQuery();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO tipologias_edificio (id, json, updated_at)
            VALUES (1, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(data, SqliteStoreHelpers.JsonOptions));
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.ExecuteNonQuery();
    }

    internal static TipologiasEdificioData Load(SqliteConnection connection)
    {
        if (!SqliteStoreHelpers.TableExists(connection, "tipologias_edificio"))
            return TipologiasEdificioData.Default();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM tipologias_edificio WHERE id = 1;";
        object? value = cmd.ExecuteScalar();
        if (value == null || value == DBNull.Value)
            return TipologiasEdificioData.Default();

        return JsonSerializer.Deserialize<TipologiasEdificioData>(Convert.ToString(value) ?? "{}", SqliteStoreHelpers.JsonOptions)
            ?? TipologiasEdificioData.Default();
    }
}
