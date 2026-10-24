using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class PriceBankStoreTests
{
    [Fact]
    public void Delete_RowExists_EliminaElRegistro()
    {
        var store = StoreEnMemoria();
        store.Upsert(new PriceBankEntry
        {
            Codigo = "CABLE-1",
            Descripcion = "Cable",
            Unidad = "m",
            Categoria = "Conductores",
            MatUf = 1m,
            FechaPrecio = DateTime.Today
        });

        bool deleted = store.Delete("CABLE-1");

        Assert.True(deleted);
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Delete_EntryMissing_ReturnsFalse()
    {
        var store = StoreEnMemoria();

        bool deleted = store.Delete("INEXISTENTE");

        Assert.False(deleted);
    }

    // Regresión de bug encontrado en esta sesión: el fallback de Search() (cuando FTS5 lanza
    // SqliteException) usaba SQL UPPER()/LIKE — ASCII-only sin extensión ICU, no encontraba
    // texto acentuado. Se extrajo a FilterByNormalizedText (internal) para poder testearlo sin
    // forzar una SqliteException real desde FTS5.
    [Fact]
    public void FilterByNormalizedText_MatchesAccentedText_RegardlessOfAccentsInQuery()
    {
        var entries = new[]
        {
            new PriceBankEntry { Codigo = "COND-1", Descripcion = "Canalización eléctrica", Categoria = "Tuberías" }
        };

        Assert.Single(PriceBankStore.FilterByNormalizedText(entries, "tuberias"));
        Assert.Single(PriceBankStore.FilterByNormalizedText(entries, "CANALIZACIÓN"));
        Assert.Empty(PriceBankStore.FilterByNormalizedText(entries, "no-existe"));
    }

    // ── orden: preserva el orden de llegada (import/creación), no alfabético por código ──────

    [Fact]
    public void UpsertMany_PreservaElOrdenDeLlegada_NoAlfabetico()
    {
        var store = StoreEnMemoria();

        store.UpsertMany(new[]
        {
            new PriceBankEntry { Codigo = "ZZZ-1", Descripcion = "Último alfabético, primero en el Excel" },
            new PriceBankEntry { Codigo = "AAA-1", Descripcion = "Primero alfabético, segundo en el Excel" },
            new PriceBankEntry { Codigo = "MMM-1", Descripcion = "Medio" },
        });

        var all = store.GetAll();

        Assert.Equal(new[] { "ZZZ-1", "AAA-1", "MMM-1" }, all.Select(e => e.Codigo));
    }

    [Fact]
    public void Upsert_FilaNueva_SeAgregaAlFinalDelOrden()
    {
        var store = StoreEnMemoria();
        store.UpsertMany(new[]
        {
            new PriceBankEntry { Codigo = "A" },
            new PriceBankEntry { Codigo = "B" },
        });

        store.Upsert(new PriceBankEntry { Codigo = "C" });

        Assert.Equal(new[] { "A", "B", "C" }, store.GetAll().Select(e => e.Codigo));
    }

    [Fact]
    public void Upsert_FilaExistente_ConservaSuPosicion()
    {
        var store = StoreEnMemoria();
        store.UpsertMany(new[]
        {
            new PriceBankEntry { Codigo = "A", MatClp = 100m },
            new PriceBankEntry { Codigo = "B" },
            new PriceBankEntry { Codigo = "C" },
        });

        // Editar el precio de "A" (primera fila) no debe moverla al final.
        store.Upsert(new PriceBankEntry { Codigo = "A", MatClp = 999m });

        Assert.Equal(new[] { "A", "B", "C" }, store.GetAll().Select(e => e.Codigo));
        Assert.Equal(999m, store.GetByCodigo("A")!.MatClp);
    }

    // ── MatClp/MoClp: la fuente en pesos debe persistir (no solo la UF derivada) ─────────────

    [Fact]
    public void UpsertYGetAll_PersisteMatClpYMoClp()
    {
        var store = StoreEnMemoria();
        store.Upsert(new PriceBankEntry { Codigo = "A", MatClp = 5000m, MoClp = 2000m });

        var reloaded = store.GetByCodigo("A");

        Assert.NotNull(reloaded);
        Assert.Equal(5000m, reloaded!.MatClp);
        Assert.Equal(2000m, reloaded.MoClp);
    }

    // ── DeleteAll: vacía todo el banco ────────────────────────────────────────────────────────

    [Fact]
    public void DeleteAll_EliminaTodosLosRegistros()
    {
        var store = StoreEnMemoria();
        store.UpsertMany(new[]
        {
            new PriceBankEntry { Codigo = "A" },
            new PriceBankEntry { Codigo = "B" },
            new PriceBankEntry { Codigo = "C" },
        });

        int deleted = store.DeleteAll();

        Assert.Equal(3, deleted);
        Assert.Empty(store.GetAll());
        Assert.Equal(0, store.Count());
    }

    [Fact]
    public void DeleteAll_BancoVacio_RetornaCero()
    {
        var store = StoreEnMemoria();

        Assert.Equal(0, store.DeleteAll());
    }

    // ── Sanidad de esquema: una DB con user_version de otro build (número mayor que
    // LatestSchemaVersion) pero sin las columnas de este esquema debe sanearse al abrir.
    // Regresión: el arranque de 1.1.1 moría con "no such column: rendimiento_dh".
    [Fact]
    public void Constructor_DbConUserVersionAjeno_SinColumnasNuevas_LasAnadeYSigueOperativo()
    {
        string db = Path.Combine(Path.GetTempPath(), $"test_pricebank_foreign_{Guid.NewGuid():N}.db");
        try
        {
            using (var cn = new SqliteConnection($"Data Source={db}"))
            {
                cn.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE price_bank (
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
                        actualizado_en TEXT NOT NULL DEFAULT '',
                        mat_clp REAL NOT NULL DEFAULT 0,
                        mo_clp REAL NOT NULL DEFAULT 0,
                        orden INTEGER NOT NULL DEFAULT 0,
                        mo_formula_tipo TEXT NOT NULL DEFAULT '',
                        mo_porcentaje_material REAL NOT NULL DEFAULT 0,
                        mo_factor_aplicacion REAL NOT NULL DEFAULT 0,
                        grupo TEXT NOT NULL DEFAULT ''
                    );
                    PRAGMA user_version = 5;
                    """;
                cmd.ExecuteNonQuery();
            }

            var store = new PriceBankStore(db);
            store.Upsert(new PriceBankEntry { Codigo = "HEAL-1", RendimientoDh = 464m, DiaXPareja = 10m });

            var reloaded = store.GetByCodigo("HEAL-1");
            Assert.NotNull(reloaded);
            Assert.Equal(464m, reloaded!.RendimientoDh);
            Assert.Equal(10m, reloaded.DiaXPareja);

            using (var cn2 = new SqliteConnection($"Data Source={db}"))
            {
                cn2.Open();
                using var v = cn2.CreateCommand();
                v.CommandText = "PRAGMA user_version;";
                Assert.Equal(PriceBankStore.LatestSchemaVersion, Convert.ToInt32(v.ExecuteScalar()));
            }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(db + suffix); } catch { /* archivo temporal */ }
            }
        }
    }

    // ── M.O. por %: columnas de fórmula (migración v5 + roundtrip) ───────────

    [Fact]
    public void Upsert_MoFormulaPorcentaje_RoundTripsEnEsquemaPropio()
    {
        var store = StoreEnMemoria();
        store.Upsert(new PriceBankEntry
        {
            Codigo = "PCT-1",
            MatClp = 1000m,
            MoClp = 350m,
            MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
            MoPorcentajeMaterial = 3.75m
        });

        var reloaded = store.GetByCodigo("PCT-1");
        Assert.NotNull(reloaded);
        Assert.Equal(MaterialMoResolver.MoFormulaPorcentaje, reloaded!.MoFormulaTipo);
        Assert.Equal(3.75m, reloaded.MoPorcentajeMaterial);
    }

    [Fact]
    public void Upsert_SinFormula_LlegaVacia_RoundTrips()
    {
        var store = StoreEnMemoria();
        store.Upsert(new PriceBankEntry { Codigo = "FIJ-1", MoClp = 200m });

        var reloaded = store.GetByCodigo("FIJ-1");
        Assert.NotNull(reloaded);
        Assert.Equal("", reloaded!.MoFormulaTipo);
        Assert.Equal(2.5m, reloaded.MoPorcentajeMaterial); // default de la columna
    }

    [Fact]
    public void Constructor_DbV4_MigraAV5_AnadeColumnasDeFormulaMOPorcentaje()
    {
        string db = Path.Combine(Path.GetTempPath(), $"test_pricebank_v4_{Guid.NewGuid():N}.db");
        try
        {
            using (var cn = new SqliteConnection($"Data Source={db}"))
            {
                cn.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE price_bank (
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
                        actualizado_en TEXT NOT NULL DEFAULT '',
                        mat_clp REAL NOT NULL DEFAULT 0,
                        mo_clp REAL NOT NULL DEFAULT 0,
                        orden INTEGER NOT NULL DEFAULT 0,
                        rendimiento_dh REAL NOT NULL DEFAULT 0,
                        dia_pareja REAL NOT NULL DEFAULT 0
                    );
                    PRAGMA user_version = 4;
                    """;
                cmd.ExecuteNonQuery();
            }

            var store = new PriceBankStore(db);
            store.Upsert(new PriceBankEntry
            {
                Codigo = "V4-1",
                MoFormulaTipo = MaterialMoResolver.MoFormulaPorcentaje,
                MoPorcentajeMaterial = 4m
            });

            var reloaded = store.GetByCodigo("V4-1");
            Assert.NotNull(reloaded);
            Assert.Equal(MaterialMoResolver.MoFormulaPorcentaje, reloaded!.MoFormulaTipo);
            Assert.Equal(4m, reloaded.MoPorcentajeMaterial);

            using (var cn2 = new SqliteConnection($"Data Source={db}"))
            {
                cn2.Open();
                using var v = cn2.CreateCommand();
                v.CommandText = "PRAGMA user_version;";
                Assert.Equal(PriceBankStore.LatestSchemaVersion, Convert.ToInt32(v.ExecuteScalar()));
            }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(db + suffix); } catch { /* archivo temporal */ }
            }
        }
    }

    [Fact]
    public void Constructor_DbV5_MigraAV6_AnadeColumnaMoFactorBase()
    {
        string db = Path.Combine(Path.GetTempPath(), $"test_v5_to_v6_{Guid.NewGuid():N}.db");
        try
        {
            using (var cn = new SqliteConnection($"Data Source={db}"))
            {
                cn.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE price_bank (
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
                        actualizado_en TEXT NOT NULL DEFAULT '',
                        mat_clp REAL NOT NULL DEFAULT 0,
                        mo_clp REAL NOT NULL DEFAULT 0,
                        orden INTEGER NOT NULL DEFAULT 0,
                        rendimiento_dh REAL NOT NULL DEFAULT 0,
                        dia_pareja REAL NOT NULL DEFAULT 0,
                        mo_formula_tipo TEXT NOT NULL DEFAULT '',
                        mo_porcentaje_material REAL NOT NULL DEFAULT 0
                    );
                    PRAGMA user_version = 5;
                    """;
                cmd.ExecuteNonQuery();
            }

            var store = new PriceBankStore(db);
            store.Upsert(new PriceBankEntry
            {
                Codigo = "V6-1",
                MoFormulaTipo = MaterialMoResolver.MoFormulaFactor,
                MoFactorBase = 190m
            });

            var reloaded = store.GetByCodigo("V6-1");
            Assert.NotNull(reloaded);
            Assert.Equal(MaterialMoResolver.MoFormulaFactor, reloaded!.MoFormulaTipo);
            Assert.Equal(190m, reloaded.MoFactorBase);

            using (var cn2 = new SqliteConnection($"Data Source={db}"))
            {
                cn2.Open();
                using var v = cn2.CreateCommand();
                v.CommandText = "PRAGMA user_version;";
                Assert.Equal(6, Convert.ToInt32(v.ExecuteScalar()));
            }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(db + suffix); } catch { /* ignore */ }
            }
        }
    }

    private static PriceBankStore StoreEnMemoria()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_pricebank_delete_{Guid.NewGuid():N}.db");
        return new PriceBankStore(tempDb);
    }
}
