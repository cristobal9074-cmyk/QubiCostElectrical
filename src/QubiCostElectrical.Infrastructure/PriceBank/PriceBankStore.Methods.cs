using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QubiCostElectrical;

public sealed partial class PriceBankStore : IPriceBankStore
{
    public PriceBankStore(string? databasePath = null)
    {
        this.databasePath = ResolveDatabasePath(databasePath);
    }

    public string DatabasePath => databasePath;

    private const string SelectColumns =
        "codigo, descripcion, unidad, categoria, mat_uf, mo_uf, moneda, proveedor, marca, fecha_precio, origen_precio, proyecto_origen, actualizado_en, mat_clp, mo_clp";

    // orden ASC preserva el orden de llegada (import/creación) en vez de reordenar alfabético —
    // pedido explícito del usuario: la lista se ve "tal cual como está en el Excel".
    public IReadOnlyList<PriceBankEntry> GetAll()
    {
        if (allEntriesCache != null)
        {
            return allEntriesCache;
        }
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumns} FROM price_bank ORDER BY orden ASC, rowid ASC;";
        allEntriesCache = ReadAll(cmd);
        return allEntriesCache;
    }

    public PriceBankEntry? GetByCodigo(string codigo)
    {
        string code = (codigo ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return null;

        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumns} FROM price_bank WHERE codigo = $c;";
        cmd.Parameters.AddWithValue("$c", code);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<PriceBankEntry> Search(string text)
    {
        string q = (text ?? "").Trim();
        if (string.IsNullOrEmpty(q)) return GetAll();

        using var cn = Open();

        try
        {
            string ftsQuery = BuildFtsQuery(q);
            using var cmd = cn.CreateCommand();
            cmd.CommandText = """
                SELECT p.codigo, p.descripcion, p.unidad, p.categoria, p.mat_uf, p.mo_uf, p.moneda, p.proveedor, p.marca, p.fecha_precio, p.origen_precio, p.proyecto_origen, p.actualizado_en, p.mat_clp, p.mo_clp
                FROM price_bank p
                JOIN price_bank_fts f ON p.rowid = f.rowid
                WHERE price_bank_fts MATCH $q
                ORDER BY p.orden ASC, p.rowid ASC;
                """;
            cmd.Parameters.AddWithValue("$q", ftsQuery);
            return ReadAll(cmd);
        }
        catch (SqliteException ex)
        {
            AppLog.Warning("BancoPrecios", $"FTS5 search failed for '{q}'; falling back to LIKE scan", ex);
            return FilterByNormalizedText(GetAll(), q);
        }
    }

    // Filtrado en memoria con SearchTextNormalizer en vez de SQL UPPER()/LIKE — UPPER() nativo
    // de SQLite es ASCII-only, "proveedores" no encontraba "Proveedores especializados S.A." si
    // el texto tenía tildes. Extraído como método propio (en vez de inline en el catch) para
    // poder testearlo sin depender de forzar una SqliteException real desde FTS5.
    internal static List<PriceBankEntry> FilterByNormalizedText(IEnumerable<PriceBankEntry> entries, string query)
    {
        string normalizedQuery = SearchTextNormalizer.Normalize(query);
        return entries
            .Where(e => SearchTextNormalizer.Normalize(e.Codigo).Contains(normalizedQuery)
                     || SearchTextNormalizer.Normalize(e.Descripcion).Contains(normalizedQuery)
                     || SearchTextNormalizer.Normalize(e.Categoria).Contains(normalizedQuery))
            .ToList();
    }

    private static string BuildFtsQuery(string input)
    {
        var tokens = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parts = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            if (parts.Length > 0) parts.Append(' ');
            // FTS5 trata "(", ")" y ":" como sintaxis reservada (agrupación de expresiones y
            // filtro de columna). Sin comillas, un token como "(2x14)" produce un error de
            // sintaxis en vez de buscarse como texto literal — caso común en descripciones
            // eléctricas ("cable (2x14)", "tubo (20mm)").
            bool needsQuote = token.Any(c => "\"*^{}\\-+():".Contains(c));
            if (needsQuote)
                parts.Append('"').Append(token.Replace("\"", "\"\"")).Append('"');
            else
                parts.Append(token);
            parts.Append('*');
        }
        return parts.ToString();
    }

    /// <summary>Inserta o actualiza una entrada (upsert por código). Conserva su orden si ya
    /// existía; si es nueva, la agrega al final de la lista.</summary>
    public void Upsert(PriceBankEntry entry)
    {
        if (entry == null) return;
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        try
        {
            UpsertCore(cn, tx, entry, orden: null);
            tx.Commit();
            allEntriesCache = null;
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", $"Falló el upsert del código '{entry.Codigo}'", ex);
            tx.Rollback();
            throw;
        }
    }

    public bool Delete(string codigo)
    {
        string code = (codigo ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) return false;

        using var cn = Open();
        using var tx = cn.BeginTransaction();
        try
        {
            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM price_bank WHERE codigo = $codigo;";
            cmd.Parameters.AddWithValue("$codigo", code);
            int rows = cmd.ExecuteNonQuery();
            tx.Commit();
            allEntriesCache = null;
            return rows > 0;
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", $"Falló la eliminación del código '{code}'", ex);
            tx.Rollback();
            throw;
        }
    }

    /// <summary>Elimina TODOS los precios del banco. Irreversible — la UI debe confirmar antes.</summary>
    public int DeleteAll()
    {
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        try
        {
            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM price_bank;";
            int rows = cmd.ExecuteNonQuery();
            tx.Commit();
            allEntriesCache = null;
            return rows;
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", "Falló vaciar el banco de precios; se revierte la transacción", ex);
            tx.Rollback();
            throw;
        }
    }

    /// <summary>Upsert masivo en una sola transacción (importar un proyecto/archivo al banco).
    /// Asigna a cada entrada un "orden" creciente en el mismo orden en que aparece en
    /// <paramref name="entries"/> — así la grilla queda "tal cual como está en el Excel", no
    /// reordenada alfabéticamente.</summary>
    public int UpsertMany(IEnumerable<PriceBankEntry> entries)
    {
        if (entries == null) return 0;
        using var cn = Open();
        using var tx = cn.BeginTransaction();
        int count = 0;
        try
        {
            int nextOrden = NextOrden(cn, tx);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Codigo)) continue;
                UpsertCore(cn, tx, entry, orden: nextOrden++);
                count++;
            }
            tx.Commit();
            allEntriesCache = null;
            return count;
        }
        catch (Exception ex)
        {
            AppLog.Error("BancoPrecios", "Falló el upsert masivo de precios; se revierte la transacción", ex);
            tx.Rollback();
            throw;
        }
    }

    private static int NextOrden(SqliteConnection cn, SqliteTransaction tx)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COALESCE(MAX(orden), 0) + 1 FROM price_bank;";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int Count()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM price_bank;";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Checks whether the FTS5 virtual table exists in the database.</summary>
    public bool HasFtsIndex()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='price_bank_fts';";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    /// <summary>Rebuilds the FTS5 index from the content table. Use after bulk operations or suspected corruption.</summary>
    public void RebuildFtsIndex()
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "INSERT INTO price_bank_fts(price_bank_fts) VALUES('rebuild');";
        cmd.ExecuteNonQuery();
    }

    public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries)
    {
        if (entries == null) return 0;
        if (Count() > 0) return 0;
        return UpsertMany(entries);
    }

    // orden: posición explícita (p. ej. UpsertMany asignando el orden del Excel importado) o
    // null para conservar/asignar automático (Upsert de una sola fila: mantiene su posición si
    // ya existía, o se agrega al final si es nueva) — ver comentario de NextOrden más abajo.
    private void UpsertCore(SqliteConnection cn, SqliteTransaction tx, PriceBankEntry entry, int? orden)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO price_bank (codigo, descripcion, unidad, categoria, mat_uf, mo_uf, moneda, proveedor, marca, fecha_precio, origen_precio, proyecto_origen, actualizado_en, mat_clp, mo_clp, orden)
            VALUES ($codigo, $descripcion, $unidad, $categoria, $mat, $mo, $moneda, $proveedor, $marca, $fecha, $origen, $proyecto, $actualizado, $matclp, $moclp,
                COALESCE($orden, (SELECT orden FROM price_bank WHERE codigo = $codigo), (SELECT COALESCE(MAX(orden), 0) + 1 FROM price_bank)))
            ON CONFLICT(codigo) DO UPDATE SET
                descripcion = excluded.descripcion,
                unidad = excluded.unidad,
                categoria = excluded.categoria,
                mat_uf = excluded.mat_uf,
                mo_uf = excluded.mo_uf,
                moneda = excluded.moneda,
                proveedor = excluded.proveedor,
                marca = excluded.marca,
                fecha_precio = excluded.fecha_precio,
                origen_precio = excluded.origen_precio,
                proyecto_origen = excluded.proyecto_origen,
                actualizado_en = excluded.actualizado_en,
                mat_clp = excluded.mat_clp,
                mo_clp = excluded.mo_clp,
                orden = CASE WHEN $orden IS NOT NULL THEN excluded.orden ELSE price_bank.orden END;
            """;
        cmd.Parameters.AddWithValue("$codigo", (entry.Codigo ?? "").Trim().ToUpperInvariant());
        cmd.Parameters.AddWithValue("$descripcion", entry.Descripcion ?? "");
        cmd.Parameters.AddWithValue("$unidad", entry.Unidad ?? "");
        cmd.Parameters.AddWithValue("$categoria", entry.Categoria ?? "");
        cmd.Parameters.AddWithValue("$mat", entry.MatUf);
        cmd.Parameters.AddWithValue("$mo", entry.MoUf);
        cmd.Parameters.AddWithValue("$matclp", entry.MatClp);
        cmd.Parameters.AddWithValue("$moclp", entry.MoClp);
        cmd.Parameters.AddWithValue("$moneda", string.IsNullOrWhiteSpace(entry.MonedaPrecio) ? "CLP" : entry.MonedaPrecio);
        cmd.Parameters.AddWithValue("$proveedor", entry.Proveedor ?? "");
        cmd.Parameters.AddWithValue("$marca", entry.Marca ?? "");
        cmd.Parameters.AddWithValue("$fecha", entry.FechaPrecio.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$origen", string.IsNullOrWhiteSpace(entry.OrigenPrecio) ? "Banco de precios" : entry.OrigenPrecio);
        cmd.Parameters.AddWithValue("$proyecto", entry.ProyectoOrigen ?? "");
        cmd.Parameters.AddWithValue("$actualizado", AppClock.Now.ToString("o", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$orden", (object?)orden ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static List<PriceBankEntry> ReadAll(SqliteCommand cmd)
    {
        var list = new List<PriceBankEntry>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(Map(reader));
        return list;
    }

    private static PriceBankEntry Map(SqliteDataReader r) => new()
    {
        Codigo = ReadText(r, 0),
        Descripcion = ReadText(r, 1),
        Unidad = ReadText(r, 2),
        Categoria = ReadText(r, 3),
        MatUf = ReadDecimal(r, 4),
        MoUf = ReadDecimal(r, 5),
        MonedaPrecio = ReadText(r, 6),
        Proveedor = ReadText(r, 7),
        Marca = ReadText(r, 8),
        FechaPrecio = ReadDate(r, 9),
        OrigenPrecio = ReadText(r, 10),
        ProyectoOrigen = ReadText(r, 11),
        ActualizadoEn = ReadDate(r, 12),
        MatClp = ReadDecimal(r, 13),
        MoClp = ReadDecimal(r, 14)
    };

    private SqliteConnection Open()
    {
        return OpenWithFallback(DatabasePath, allowFallback: true);
    }

    private static SqliteConnection OpenWithFallback(string path, bool allowFallback)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var cn = new SqliteConnection($"Data Source={path}");
            cn.Open();
            EnsureSchema(cn);
            return cn;
        }
        catch (SqliteException ex) when (allowFallback && ex.SqliteErrorCode == 14)
        {
            return OpenWithFallback(CreateTempFallbackDatabasePath(ex), allowFallback: false);
        }
        catch (UnauthorizedAccessException ex) when (allowFallback)
        {
            return OpenWithFallback(CreateTempFallbackDatabasePath(ex), allowFallback: false);
        }
        catch (IOException ex) when (allowFallback)
        {
            return OpenWithFallback(CreateTempFallbackDatabasePath(ex), allowFallback: false);
        }
    }

    private static string ResolveDatabasePath(string? databasePath)
    {
        if (!string.IsNullOrWhiteSpace(databasePath))
        {
            EnsureDirectoryOrThrow(databasePath);
            return databasePath;
        }

        var defaultPath = GetDefaultDatabasePath();
        try
        {
            EnsureDirectoryOrThrow(defaultPath);
            return defaultPath;
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateTempFallbackDatabasePath(ex);
        }
        catch (IOException ex)
        {
            return CreateTempFallbackDatabasePath(ex);
        }
    }

    private static void EnsureDirectoryOrThrow(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    private static string CreateTempFallbackDatabasePath(Exception ex)
    {
        var fallbackDir = Path.Combine(Path.GetTempPath(), ProductIdentity.AppDataFolderName, "PriceBank");
        Directory.CreateDirectory(fallbackDir);
        AppLog.Warning("BancoPrecios", $"No se pudo usar la base de precios principal; se usará '{fallbackDir}'.", ex);
        return Path.Combine(fallbackDir, "banco_precios.db");
    }

    private static string GetDefaultDatabasePath()
    {
        return ProductPaths.PriceBankDatabaseFile();
    }

    private static void EnsureSchema(SqliteConnection cn)
    {
        using var pragma = cn.CreateCommand();
        pragma.CommandText = "PRAGMA user_version;";
        int version = Convert.ToInt32(pragma.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (version >= LatestSchemaVersion) return;

        if (version < 1)
        {
            using var tx = cn.BeginTransaction();
            try
            {
                Execute(cn, tx, """
                    CREATE TABLE IF NOT EXISTS price_bank (
                        codigo TEXT PRIMARY KEY,
                        descripcion TEXT NOT NULL DEFAULT '',
                        unidad TEXT NOT NULL DEFAULT '',
                        categoria TEXT NOT NULL DEFAULT '',
                        mat_uf REAL NOT NULL DEFAULT 0,
                        mo_uf REAL NOT NULL DEFAULT 0,
                        moneda TEXT NOT NULL DEFAULT 'CLP',
                        proveedor TEXT NOT NULL DEFAULT '',
                        marca TEXT NOT NULL DEFAULT '',
                        fecha_precio TEXT NOT NULL DEFAULT '',
                        origen_precio TEXT NOT NULL DEFAULT '',
                        proyecto_origen TEXT NOT NULL DEFAULT '',
                        actualizado_en TEXT NOT NULL DEFAULT ''
                    );
                    CREATE INDEX IF NOT EXISTS idx_price_bank_desc ON price_bank(descripcion COLLATE NOCASE);
                    CREATE INDEX IF NOT EXISTS idx_price_bank_cat ON price_bank(categoria COLLATE NOCASE);
                    """);
                Execute(cn, tx, "PRAGMA user_version = 1;");
                tx.Commit();
            }
            catch (Exception ex)
            {
                AppLog.Critical("BancoPrecios", "Falló la migración v1 del banco de precios; se revierte", ex);
                tx.Rollback();
                throw;
            }
        }

        if (version < 2)
        {
            using var tx = cn.BeginTransaction();
            try
            {
                Execute(cn, tx, """
                    CREATE VIRTUAL TABLE IF NOT EXISTS price_bank_fts
                        USING fts5(codigo, descripcion, categoria, content='price_bank', content_rowid='rowid');
                    INSERT INTO price_bank_fts(rowid, codigo, descripcion, categoria)
                        SELECT rowid, codigo, descripcion, categoria FROM price_bank;
                    CREATE TRIGGER IF NOT EXISTS price_bank_ai AFTER INSERT ON price_bank BEGIN
                        INSERT INTO price_bank_fts(rowid, codigo, descripcion, categoria)
                            VALUES (new.rowid, new.codigo, new.descripcion, new.categoria);
                    END;
                    CREATE TRIGGER IF NOT EXISTS price_bank_ad AFTER DELETE ON price_bank BEGIN
                        INSERT INTO price_bank_fts(price_bank_fts, rowid, codigo, descripcion, categoria)
                            VALUES ('delete', old.rowid, old.codigo, old.descripcion, old.categoria);
                    END;
                    CREATE TRIGGER IF NOT EXISTS price_bank_au AFTER UPDATE ON price_bank BEGIN
                        INSERT INTO price_bank_fts(price_bank_fts, rowid, codigo, descripcion, categoria)
                            VALUES ('delete', old.rowid, old.codigo, old.descripcion, old.categoria);
                        INSERT INTO price_bank_fts(rowid, codigo, descripcion, categoria)
                            VALUES (new.rowid, new.codigo, new.descripcion, new.categoria);
                    END;
                    """);
                Execute(cn, tx, "PRAGMA user_version = 2;");
                tx.Commit();
            }
            catch (Exception ex)
            {
                AppLog.Critical("BancoPrecios", "Falló la migración FTS5 del banco de precios; se revierte", ex);
                tx.Rollback();
                throw;
            }
        }

        if (version < 3)
        {
            // mat_clp/mo_clp: fuente en pesos del modelo de costos (antes solo se guardaba la UF
            // derivada — se perdía el precio en pesos al reabrir la app). orden: preserva el
            // orden de llegada (import/creación) para que la grilla se vea "tal cual como está
            // en el Excel" en vez de reordenarse alfabéticamente por código.
            using var tx = cn.BeginTransaction();
            try
            {
                Execute(cn, tx, """
                    ALTER TABLE price_bank ADD COLUMN mat_clp REAL NOT NULL DEFAULT 0;
                    ALTER TABLE price_bank ADD COLUMN mo_clp REAL NOT NULL DEFAULT 0;
                    ALTER TABLE price_bank ADD COLUMN orden INTEGER NOT NULL DEFAULT 0;
                    """);
                Execute(cn, tx, "PRAGMA user_version = 3;");
                tx.Commit();
            }
            catch (Exception ex)
            {
                AppLog.Critical("BancoPrecios", "Falló la migración v3 (orden/CLP) del banco de precios; se revierte", ex);
                tx.Rollback();
                throw;
            }
        }
    }

    private static void Execute(SqliteConnection cn, SqliteTransaction? tx, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string ReadText(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? "" : reader.GetString(index);

    private static decimal ReadDecimal(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? 0m : Convert.ToDecimal(reader.GetValue(index), CultureInfo.InvariantCulture);

    private static DateTime ReadDate(SqliteDataReader reader, int index)
    {
        string raw = ReadText(reader, index);
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value))
            return value.ToLocalTime();
        return DateTime.MinValue;
    }
}
