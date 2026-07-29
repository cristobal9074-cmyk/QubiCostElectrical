using System;
using System.Data;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteMigratorHelpers
{
    internal static void AddColumnIfMissing(SqliteConnection connection, SqliteTransaction transaction, string table, string column, string definition)
    {
        if (!TableExists(connection, table, transaction)) return;
        if (ColumnExists(connection, table, column, transaction)) return;

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"ALTER TABLE {QuoteIdentifier(table)} ADD COLUMN {QuoteIdentifier(column)} {definition};";
        cmd.ExecuteNonQuery();
    }

    internal static bool ColumnExists(SqliteConnection connection, string table, string column, SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"PRAGMA table_info({QuoteIdentifier(table)});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    internal static bool TableExists(SqliteConnection connection, string table, SqliteTransaction? transaction = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        cmd.Parameters.AddWithValue("$name", table);
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0) > 0;
    }

    internal static string ExecuteScalarText(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        object? value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? "" : Convert.ToString(value) ?? "";
    }

    internal static void Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    internal static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        object? value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
    }

    internal static void SetUserVersion(SqliteConnection connection, int version, SqliteTransaction? transaction)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    internal static string QuoteIdentifier(string identifier)
    {
        if (identifier.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            throw new ArgumentException($"Identificador SQLite inválido: {identifier}", nameof(identifier));
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
