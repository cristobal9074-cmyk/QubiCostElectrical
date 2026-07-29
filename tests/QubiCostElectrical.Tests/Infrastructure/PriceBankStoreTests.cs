using System;
using System.IO;
using System.Linq;
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

    private static PriceBankStore StoreEnMemoria()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_pricebank_delete_{Guid.NewGuid():N}.db");
        return new PriceBankStore(tempDb);
    }
}
