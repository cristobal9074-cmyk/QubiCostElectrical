using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical.Infrastructure;

/// <summary>
/// Biblioteca global de conjuntos eléctricos (plantillas de takeoff), guardada en
/// banco_precios.db junto a las plantillas APU. Persiste FUERA del .qce: los proyectos
/// nuevos la ven sin volver a crear plantillas. El .qce solo conserva una copia
/// (espejo) cuando el usuario guarda desde la pantalla Plantillas.
/// </summary>
public sealed class ConjuntoTemplateStore : IConjuntoTemplateStore
{
    private static string? databasePathOverride;
    private readonly string databasePath;

    // Mismo motivo que ApuTemplateStore: el DDL idempotente corre una vez por instancia,
    // no en cada GetAll (el filtro de la pantalla reabre conexión por tecla).
    private bool tableEnsured;

    public ConjuntoTemplateStore(string? databasePath = null)
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

    public IReadOnlyList<ConjuntoElectricoRow> GetAll()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT json FROM conjuntos_electricos ORDER BY sort_order, nombre;";
        var list = new List<ConjuntoElectricoRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            try
            {
                var row = JsonSerializer.Deserialize<ConjuntoElectricoRow>(
                    r.GetString(0), SqliteStoreHelpers.JsonOptions);
                if (row != null) list.Add(row);
            }
            catch
            {
                // fila corrupta — se ignora para no bloquear la pantalla completa
            }
        }
        return list;
    }

    public void Save(ConjuntoElectricoRow conjunto)
    {
        if (conjunto == null) return;
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        // En INSERT nuevo va al inicio (sort_order 0); en UPDATE no toca el orden ya
        // asignado por SaveAll. El próximo SaveAll de la pantalla reordena la colección.
        cmd.CommandText = """
            INSERT INTO conjuntos_electricos (id, sort_order, codigo, nombre, json, actualizado_en)
            VALUES ($id, 0, $codigo, $nombre, $json, $act)
            ON CONFLICT(id) DO UPDATE SET
                codigo        = excluded.codigo,
                nombre        = excluded.nombre,
                json          = excluded.json,
                actualizado_en = excluded.actualizado_en;
            """;
        AddParameters(cmd, conjunto);
        cmd.ExecuteNonQuery();
    }

    public void SaveAll(IReadOnlyList<ConjuntoElectricoRow> conjuntos)
    {
        var filas = (conjuntos ?? Array.Empty<ConjuntoElectricoRow>())
            .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id))
            .ToList();

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO conjuntos_electricos (id, sort_order, codigo, nombre, json, actualizado_en)
            VALUES ($id, $orden, $codigo, $nombre, $json, $act)
            ON CONFLICT(id) DO UPDATE SET
                sort_order    = excluded.sort_order,
                codigo        = excluded.codigo,
                nombre        = excluded.nombre,
                json          = excluded.json,
                actualizado_en = excluded.actualizado_en;
            """;
        AddParameters(cmd, filas.Count > 0 ? filas[0] : null);
        var pOrden = cmd.Parameters.Add("$orden", SqliteType.Integer);

        for (int i = 0; i < filas.Count; i++)
        {
            AddParameters(cmd, filas[i]);
            pOrden.Value = i;
            cmd.ExecuteNonQuery();
        }

        SqliteStoreHelpers.DeleteMissingKeys(cn, tx, "conjuntos_electricos", "id",
            filas.Select(c => c.Id));
        tx.Commit();
    }

    private static void AddParameters(SqliteCommand cmd, ConjuntoElectricoRow? conjunto)
    {
        void Set(string name, object value)
        {
            if (cmd.Parameters.Contains(name)) cmd.Parameters[name].Value = value;
            else cmd.Parameters.AddWithValue(name, value);
        }

        Set("$id", conjunto?.Id ?? "");
        Set("$codigo", conjunto?.Codigo ?? "");
        Set("$nombre", conjunto?.Nombre ?? "");
        Set("$json", conjunto == null
            ? "{}"
            : JsonSerializer.Serialize(conjunto, SqliteStoreHelpers.JsonOptions));
        Set("$act", AppClock.Now.ToString("o", CultureInfo.InvariantCulture));
    }

    // ── Helpers estáticos para consumo puntual sin instancia ─────────────────

    /// <summary>Lee la biblioteca global; devuelve vacío si la base no está disponible.</summary>
    public static IReadOnlyList<ConjuntoElectricoRow> TryGetAll()
    {
        try { return new ConjuntoTemplateStore().GetAll(); }
        catch { return Array.Empty<ConjuntoElectricoRow>(); }
    }

    /// <summary>Guarda un conjunto en la biblioteca global. false si la base no está disponible.</summary>
    public static bool TrySave(ConjuntoElectricoRow conjunto)
    {
        try { new ConjuntoTemplateStore().Save(conjunto); return true; }
        catch { return false; }
    }

    /// <summary>
    /// Une la biblioteca global con las filas de la sesión del proyecto (dedup por Id;
    /// la biblioteca gana). Usado por Cubicación para listar plantillas disponibles.
    /// </summary>
    public static List<ConjuntoElectricoRow> Combinar(
        IReadOnlyList<ConjuntoElectricoRow>? biblioteca,
        IEnumerable<ConjuntoElectricoRow>? sesion)
    {
        var resultado = new List<ConjuntoElectricoRow>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in biblioteca ?? Array.Empty<ConjuntoElectricoRow>())
            if (b != null && ids.Add(b.Id)) resultado.Add(b);
        foreach (var s in sesion ?? Array.Empty<ConjuntoElectricoRow>())
            if (s != null && ids.Add(s.Id)) resultado.Add(s);
        return resultado;
    }

    // ── Conexión / esquema ───────────────────────────────────────────────────

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
            CREATE TABLE IF NOT EXISTS conjuntos_electricos (
                id             TEXT PRIMARY KEY,
                sort_order     INTEGER NOT NULL DEFAULT 0,
                codigo         TEXT NOT NULL DEFAULT '',
                nombre         TEXT NOT NULL DEFAULT '',
                json           TEXT NOT NULL DEFAULT '{}',
                actualizado_en TEXT NOT NULL DEFAULT ''
            );
            CREATE INDEX IF NOT EXISTS idx_conjuntos_globales_nombre ON conjuntos_electricos(nombre COLLATE NOCASE);
            """;
        cmd.ExecuteNonQuery();
    }

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
