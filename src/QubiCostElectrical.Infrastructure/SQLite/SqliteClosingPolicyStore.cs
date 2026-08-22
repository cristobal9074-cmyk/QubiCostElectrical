using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteClosingPolicyStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, ClosingPolicySelection data, string now)
    {
        data ??= ClosingPolicySelection.Default();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO closing_policy (id, json, updated_at)
            VALUES (1, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(data, SqliteStoreHelpers.JsonOptions));
        cmd.Parameters.AddWithValue("$updated_at", now);
        cmd.ExecuteNonQuery();
    }

    internal static ClosingPolicySelection Load(SqliteConnection connection)
    {
        if (!SqliteStoreHelpers.TableExists(connection, "closing_policy"))
            return ClosingPolicySelection.Default();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT json FROM closing_policy WHERE id = 1;";
        object? value = cmd.ExecuteScalar();
        if (value == null || value == DBNull.Value)
            return ClosingPolicySelection.Default();

        return JsonSerializer.Deserialize<ClosingPolicySelection>(Convert.ToString(value) ?? "{}", SqliteStoreHelpers.JsonOptions)
            ?? ClosingPolicySelection.Default();
    }
}
