using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

internal static class SqliteMaterialesStore
{
    internal static void Save(SqliteConnection connection, SqliteTransaction transaction, List<MaterialRow> rows, string now)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO materiales (codigo, sort_order, descripcion, unidad, categoria, subcategoria, subcategoria2, tipo_instalacion, especificacion_tecnica, mat_uf, mo_uf, mat_clp, mo_clp, json, updated_at)
            VALUES ($codigo, $sort_order, $descripcion, $unidad, $categoria, $subcategoria, $subcategoria2, $tipo_instalacion, $especificacion_tecnica, $mat_uf, $mo_uf, $mat_clp, $mo_clp, $json, $updated_at)
            ON CONFLICT(codigo) DO UPDATE SET
                sort_order = excluded.sort_order,
                descripcion = excluded.descripcion,
                unidad = excluded.unidad,
                categoria = excluded.categoria,
                subcategoria = excluded.subcategoria,
                subcategoria2 = excluded.subcategoria2,
                tipo_instalacion = excluded.tipo_instalacion,
                especificacion_tecnica = excluded.especificacion_tecnica,
                mat_uf = excluded.mat_uf,
                mo_uf = excluded.mo_uf,
                mat_clp = excluded.mat_clp,
                mo_clp = excluded.mo_clp,
                json = excluded.json,
                updated_at = excluded.updated_at;
            """;
        cmd.Parameters.Add("$codigo", SqliteType.Text);
        cmd.Parameters.Add("$sort_order", SqliteType.Integer);
        cmd.Parameters.Add("$descripcion", SqliteType.Text);
        cmd.Parameters.Add("$unidad", SqliteType.Text);
        cmd.Parameters.Add("$categoria", SqliteType.Text);
        cmd.Parameters.Add("$subcategoria", SqliteType.Text);
        cmd.Parameters.Add("$subcategoria2", SqliteType.Text);
        cmd.Parameters.Add("$tipo_instalacion", SqliteType.Text);
        cmd.Parameters.Add("$especificacion_tecnica", SqliteType.Text);
        cmd.Parameters.Add("$mat_uf", SqliteType.Real);
        cmd.Parameters.Add("$mo_uf", SqliteType.Real);
        cmd.Parameters.Add("$mat_clp", SqliteType.Real);
        cmd.Parameters.Add("$mo_clp", SqliteType.Real);
        cmd.Parameters.Add("$json", SqliteType.Text);
        cmd.Parameters.Add("$updated_at", SqliteType.Text);
        cmd.Prepare();

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            string code = string.IsNullOrWhiteSpace(row.Codigo) ? $"MAT_{i + 1:000000}" : row.Codigo.Trim();
            while (!used.Add(code)) code = code + "_" + used.Count.ToString("000");
            row.Codigo = code;

            cmd.Parameters["$codigo"].Value = row.Codigo;
            cmd.Parameters["$sort_order"].Value = i;
            cmd.Parameters["$descripcion"].Value = row.Descripcion ?? "";
            cmd.Parameters["$unidad"].Value = row.Unidad ?? "";
            cmd.Parameters["$categoria"].Value = row.Categoria ?? "";
            cmd.Parameters["$subcategoria"].Value = row.Subcategoria ?? "";
            cmd.Parameters["$subcategoria2"].Value = row.Subcategoria2 ?? "";
            cmd.Parameters["$tipo_instalacion"].Value = row.TipoInstalacion ?? "";
            cmd.Parameters["$especificacion_tecnica"].Value = row.EspecificacionTecnica ?? "";
            cmd.Parameters["$mat_uf"].Value = (double)row.MatUf;
            cmd.Parameters["$mo_uf"].Value = (double)row.MoUf;
            cmd.Parameters["$mat_clp"].Value = (double)row.MatClp;
            cmd.Parameters["$mo_clp"].Value = (double)row.MoClp;
            cmd.Parameters["$json"].Value = JsonSerializer.Serialize(row, SqliteStoreHelpers.JsonOptions);
            cmd.Parameters["$updated_at"].Value = now;
            cmd.ExecuteNonQuery();
        }
    }
}
