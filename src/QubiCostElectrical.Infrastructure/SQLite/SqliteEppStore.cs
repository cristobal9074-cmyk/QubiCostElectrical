using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteEppStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, EppBudgetData data, string now)
    {
        data ??= EppBudgetData.Default();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO epp_config (id, json, updated_at)
            VALUES (1, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(data, SqliteStoreHelpers.JsonOptions));
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.ExecuteNonQuery();
    }

    internal static EppBudgetData Load(SqliteConnection connection)
    {
        if (!SqliteStoreHelpers.TableExists(connection, "epp_config"))
            return EppBudgetData.Default();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM epp_config WHERE id = 1;";
        object? value = cmd.ExecuteScalar();
        if (value == null || value == DBNull.Value)
            return EppBudgetData.Default();

        return JsonSerializer.Deserialize<EppBudgetData>(Convert.ToString(value) ?? "{}", SqliteStoreHelpers.JsonOptions)
            ?? EppBudgetData.Default();
    }
}
