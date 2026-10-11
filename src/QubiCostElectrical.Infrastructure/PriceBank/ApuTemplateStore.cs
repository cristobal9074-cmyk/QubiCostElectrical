using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

/// <summary>
/// Biblioteca de plantillas APU global, guardada en banco_precios.db junto al banco de precios.
/// </summary>
public sealed class ApuTemplateStore : IApuTemplateStore
{
    private static string? databasePathOverride;
    private readonly string databasePath;
    // Antes EnsureTable (CREATE TABLE/INDEX IF NOT EXISTS) corría en cada Open(), es decir en
    // cada tecla del filtro de Plantillas (GetAll/Search reabren conexión por llamada). El DDL
    // es idempotente pero no gratis; una vez asegurado el esquema para esta instancia (que vive
    // mientras dura la pantalla, ver PlantillasVm.templateStore), no hace falta repetirlo.
    private bool tableEnsured;

    public ApuTemplateStore(string? databasePath = null)
    {
        this.databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? GetDefaultDatabasePath()
            : databasePath;
    }

    public string DatabasePath => databasePath;

    public static IDisposable UseDatabasePathForTests(string path)
    {
        string? previous = databasePathOverride;
        databasePathOverride = path;
        return new DatabasePathScope(previous);
    }

    public IReadOnlyList<ApuTemplateRow> GetAll()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT id, nombre, categoria, descripcion, unidad, json_recursos, creado_en, actualizado_en FROM apu_templates ORDER BY categoria, nombre;";
        return ReadAll(cmd);
    }

    public IReadOnlyList<ApuTemplateRow> Search(string text)
    {
        string q = (text ?? "").Trim();
        if (string.IsNullOrEmpty(q)) return GetAll();

        // Filtrado en memoria con SearchTextNormalizer en vez de SQL UPPER()/LIKE — UPPER()
        // nativo de SQLite es ASCII-only, "tuberias" no encontraba "Tuberías".
        string normalizedQuery = SearchTextNormalizer.Normalize(q);
        return GetAll()
            .Where(t => SearchTextNormalizer.Normalize(t.Nombre).Contains(normalizedQuery)
                     || SearchTextNormalizer.Normalize(t.Categoria).Contains(normalizedQuery)
                     || SearchTextNormalizer.Normalize(t.Descripcion).Contains(normalizedQuery))
            .ToList();
    }

    public void Save(ApuTemplateRow t)
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        cmd.CommandText = """
            INSERT INTO apu_templates (id, nombre, categoria, descripcion, unidad, json_recursos, creado_en, actualizado_en)
            VALUES ($id, $nombre, $categoria, $descripcion, $unidad, $json, $creado, $act)
            ON CONFLICT(id) DO UPDATE SET
                nombre        = excluded.nombre,
                categoria     = excluded.categoria,
                descripcion   = excluded.descripcion,
                unidad        = excluded.unidad,
                json_recursos = excluded.json_recursos,
                actualizado_en = excluded.actualizado_en;
            """;
        cmd.Parameters.AddWithValue("$id", t.Id);
        cmd.Parameters.AddWithValue("$nombre", t.Nombre ?? "");
        cmd.Parameters.AddWithValue("$categoria", t.Categoria ?? "");
        cmd.Parameters.AddWithValue("$descripcion", t.Descripcion ?? "");
        cmd.Parameters.AddWithValue("$unidad", t.Unidad ?? "");
        cmd.Parameters.AddWithValue("$json", string.IsNullOrEmpty(t.JsonRecursos) ? "[]" : t.JsonRecursos);
        cmd.Parameters.AddWithValue("$creado", t.CreadoEn.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$act", now);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    public void Delete(string id)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "DELETE FROM apu_templates WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id ?? "");
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var cn = new SqliteConnection($"Data Source={databasePath}");
        try
        {
            cn.Open();
            if (!tableEnsured)
            {
                EnsureTable(cn);
                tableEnsured = true;
            }
            return cn;
        }
        catch
        {
            cn.Dispose();
            throw;
        }
    }

    private static void EnsureTable(SqliteConnection cn)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS apu_templates (
                id            TEXT PRIMARY KEY,
                nombre        TEXT NOT NULL DEFAULT '',
                categoria     TEXT NOT NULL DEFAULT '',
                descripcion   TEXT NOT NULL DEFAULT '',
                unidad        TEXT NOT NULL DEFAULT '',
                json_recursos TEXT NOT NULL DEFAULT '[]',
                creado_en     TEXT NOT NULL DEFAULT '',
                actualizado_en TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_apu_templates_cat ON apu_templates(categoria COLLATE NOCASE);
            """;
        cmd.ExecuteNonQuery();
    }

    private static List<ApuTemplateRow> ReadAll(SqliteCommand cmd)
    {
        var list = new List<ApuTemplateRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new ApuTemplateRow
            {
                Id = r.GetString(0),
                Nombre = r.IsDBNull(1) ? "" : r.GetString(1),
                Categoria = r.IsDBNull(2) ? "" : r.GetString(2),
                Descripcion = r.IsDBNull(3) ? "" : r.GetString(3),
                Unidad = r.IsDBNull(4) ? "" : r.GetString(4),
                JsonRecursos = r.IsDBNull(5) ? "[]" : r.GetString(5),
                CreadoEn = r.IsDBNull(6) ? DateTime.UtcNow : ParseDate(r.GetString(6)),
                ActualizadoEn = r.IsDBNull(7) ? DateTime.UtcNow : ParseDate(r.GetString(7))
            });
        return list;
    }

    private static DateTime ParseDate(string s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime d) ? d : DateTime.UtcNow;

    private static string GetDefaultDatabasePath()
    {
        if (!string.IsNullOrWhiteSpace(databasePathOverride))
            return databasePathOverride;

        return ProductPaths.PriceBankDatabaseFile();
    }

    private sealed class DatabasePathScope(string? previous) : IDisposable
    {
        public void Dispose() => databasePathOverride = previous;
    }
}
