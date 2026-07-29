using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

internal static class SqliteMoIndirectaStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, List<MoIndirectaRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO mo_indirecta (id, sort_order, json, updated_at)
            VALUES ($id, $sort_order, $json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                sort_order = excluded.sort_order,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        SqliteStoreHelpers.AddCommonParameters(cmd, "$id", "$sort_order", "$json", "$updated_at");
        cmd.Prepare();

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Id)) row.Id = Guid.NewGuid().ToString("N");
            SqliteStoreHelpers.SetCommonParameters(cmd, row.Id, i, row, now);
            cmd.ExecuteNonQuery();
        }
    }
}
