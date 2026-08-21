using System;
using System.IO;
using System.Linq;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Integration tests that validate the FTS5 full-text search path in PriceBankStore
/// against a real SQLite database (temp file, not :memory:).
/// </summary>
public sealed class PriceBankStoreFtsTests : IDisposable
{
    private readonly string dbPath;
    private readonly PriceBankStore store;

    public PriceBankStoreFtsTests()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"test_pricebank_fts_{Guid.NewGuid():N}.db");
        store = new PriceBankStore(dbPath);
        SeedTestData();
    }

    public void Dispose()
    {
        try { File.Delete(dbPath); } catch { /* best-effort cleanup */ }
    }

    // ── Search by description ───────────────────────────────────────────────

    [Fact]
    public void Search_ByDescripcion_UsaFts5()
    {
        var results = store.Search("cable");

        Assert.NotEmpty(results);
        Assert.All(results, r =>
            Assert.Contains("cable", r.Descripcion, StringComparison.OrdinalIgnoreCase));
    }

    // ── Search by category ──────────────────────────────────────────────────

    [Fact]
    public void Search_ByCategoria_UsaFts5()
    {
        var results = store.Search("conductor");

        Assert.NotEmpty(results);
        Assert.All(results, r =>
            Assert.Contains("conductor", r.Categoria, StringComparison.OrdinalIgnoreCase));
    }

    // ── Search by code ──────────────────────────────────────────────────────

    [Fact]
    public void Search_ByCodigo_UsaFts5()
    {
        var results = store.Search("THHN");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Codigo.Contains("THHN", StringComparison.OrdinalIgnoreCase));
    }

    // ── Multi-word search (AND semantics via prefix tokens) ─────────────────

    [Fact]
    public void Search_MultiWord_UsaFts5()
    {
        var results = store.Search("cable 14");

        Assert.NotEmpty(results);
        // Must match entries that contain BOTH "cable" AND "14" across indexed columns
        Assert.All(results, r =>
        {
            string combined = $"{r.Codigo} {r.Descripcion} {r.Categoria}";
            Assert.Contains("cable", combined, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("14", combined, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── Special characters don't crash ──────────────────────────────────────

    [Fact]
    public void Search_SpecialChars_NoFalla()
    {
        // These should not throw; they may return empty results
        var ex1 = Record.Exception(() => store.Search("cable (2x14)"));
        var ex2 = Record.Exception(() => store.Search("tubo 3/4\""));
        var ex3 = Record.Exception(() => store.Search("item+extra"));
        var ex4 = Record.Exception(() => store.Search("*"));

        Assert.Null(ex1);
        Assert.Null(ex2);
        Assert.Null(ex3);
        Assert.Null(ex4);
    }

    [Fact]
    public void Search_TokenWithParentheses_UsaFts5RealYNoFallback()
    {
        // "(" y ")" son sintaxis reservada de FTS5 (agrupación de expresiones). Un término
        // como "(AWG)" — común al describir calibres, ej. "cable (2x14)" — debe encontrar
        // coincidencias reales vía FTS5, no solo evitar la excepción.
        var results = store.Search("cable (AWG)");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Descripcion.Contains("AWG", StringComparison.OrdinalIgnoreCase));
    }

    // ── After Delete, entry disappears from FTS index ───────────────────────

    [Fact]
    public void Search_AfterDelete_NoRetorna()
    {
        // Verify it exists first
        var before = store.Search("THHN-14");
        Assert.NotEmpty(before);

        // Delete and search again
        store.Delete("THHN-14");
        var after = store.Search("THHN-14");

        Assert.DoesNotContain(after, r =>
            string.Equals(r.Codigo, "THHN-14", StringComparison.OrdinalIgnoreCase));
    }

    // ── After Upsert with new description, search finds new text ────────────

    [Fact]
    public void Search_AfterUpsert_Actualiza()
    {
        // Update description from "Cable THHN 14 AWG" to something unique
        var entry = store.GetByCodigo("THHN-14");
        Assert.NotNull(entry);

        entry!.Descripcion = "Alambre especial blindado XYZ";
        store.Upsert(entry);

        // Old description should not match this entry anymore
        var oldResults = store.Search("THHN 14 AWG");
        Assert.DoesNotContain(oldResults, r =>
            string.Equals(r.Codigo, "THHN-14", StringComparison.OrdinalIgnoreCase));

        // New description should match
        var newResults = store.Search("blindado XYZ");
        Assert.Contains(newResults, r =>
            string.Equals(r.Codigo, "THHN-14", StringComparison.OrdinalIgnoreCase));
    }

    // ── RebuildFtsIndex works without error ─────────────────────────────────

    [Fact]
    public void RebuildFtsIndex_Funciona()
    {
        // Should not throw
        var ex = Record.Exception(() => store.RebuildFtsIndex());
        Assert.Null(ex);

        // Search should still work after rebuild
        var results = store.Search("cable");
        Assert.NotEmpty(results);
    }

    // ── HasFtsIndex returns true for a properly migrated database ────────────

    [Fact]
    public void HasFtsIndex_ReturnsTrue()
    {
        Assert.True(store.HasFtsIndex());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private void SeedTestData()
    {
        store.UpsertMany(new[]
        {
            new PriceBankEntry
            {
                Codigo = "THHN-14",
                Descripcion = "Cable THHN 14 AWG",
                Unidad = "m",
                Categoria = "Conductores",
                MatUf = 0.05m,
                MoUf = 0.02m,
                FechaPrecio = DateTime.Today
            },
            new PriceBankEntry
            {
                Codigo = "THHN-12",
                Descripcion = "Cable THHN 12 AWG",
                Unidad = "m",
                Categoria = "Conductores",
                MatUf = 0.08m,
                MoUf = 0.02m,
                FechaPrecio = DateTime.Today
            },
            new PriceBankEntry
            {
                Codigo = "EMT-20",
                Descripcion = "Tubo EMT 20mm",
                Unidad = "tramo",
                Categoria = "Canalizaciones",
                MatUf = 0.15m,
                MoUf = 0.05m,
                FechaPrecio = DateTime.Today
            },
            new PriceBankEntry
            {
                Codigo = "INT-10A",
                Descripcion = "Interruptor termomagnético 10A",
                Unidad = "un",
                Categoria = "Protecciones",
                MatUf = 0.5m,
                MoUf = 0.1m,
                FechaPrecio = DateTime.Today
            },
            new PriceBankEntry
            {
                Codigo = "TOMA-DOBLE",
                Descripcion = "Tomacorriente doble con tierra",
                Unidad = "un",
                Categoria = "Artefactos",
                MatUf = 0.12m,
                MoUf = 0.08m,
                FechaPrecio = DateTime.Today
            }
        });
    }
}
