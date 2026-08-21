using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteStoreHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    internal static void DeleteMissingKeys(SqliteConnection connection, SqliteTransaction transaction, string table, string keyColumn, IEnumerable<string?> currentKeys)
    {
        var current = new HashSet<string>(
            currentKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var existing = new List<string>();
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = $"SELECT {keyColumn} FROM {table};";
            using var reader = read.ExecuteReader();
            while (reader.Read())
                if (!reader.IsDBNull(0)) existing.Add(reader.GetString(0));
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = $"DELETE FROM {table} WHERE {keyColumn} = $key;";
        delete.Parameters.Add("$key", SqliteType.Text);
        delete.Prepare();

        foreach (string key in existing)
        {
            if (current.Contains(key)) continue;
            delete.Parameters["$key"].Value = key;
            delete.ExecuteNonQuery();
        }
    }

    internal static List<T> ReadJsonRows<T>(SqliteConnection connection, string sql)
    {
        var rows = new List<T>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOptions);
            if (row != null) rows.Add(row);
        }
        return rows;
    }

    internal static void UpsertMeta(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO app_meta (key, value) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    internal static string? ReadMeta(SqliteConnection connection, string key)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM app_meta WHERE key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        object? value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    internal static void AddCommonParameters(SqliteCommand cmd, string id, string sortOrder, string json, string updatedAt)
    {
        cmd.Parameters.Add(id, SqliteType.Text);
        cmd.Parameters.Add(sortOrder, SqliteType.Integer);
        cmd.Parameters.Add(json, SqliteType.Text);
        cmd.Parameters.Add(updatedAt, SqliteType.Text);
    }

    internal static void SetCommonParameters<T>(SqliteCommand cmd, string id, int sortOrder, T row, string now)
    {
        cmd.Parameters["$id"].Value = id;
        cmd.Parameters["$sort_order"].Value = sortOrder;
        cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, JsonOptions);
        cmd.Parameters["$updated_at"].Value = now;
    }

    internal static bool TableExists(SqliteConnection connection, string table)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        cmd.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    internal static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
