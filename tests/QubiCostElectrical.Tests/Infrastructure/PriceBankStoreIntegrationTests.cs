using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Integration tests for PriceBankStore against an isolated SQLite database.
/// </summary>
public sealed class PriceBankStoreIntegrationTests
{
    private static PriceBankStore CreateStore()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pricebank_integration_{Guid.NewGuid():N}.db");
        return new PriceBankStore(dbPath);
    }

    [Fact]
    public void Search_ByDescripcion_ReturnsMatchingEntry()
    {
        var store = CreateStore();
        store.Upsert(new PriceBankEntry
        {
            Codigo = "CABLE-14",
            Descripcion = "Cable THHN 14 AWG",
            Unidad = "m",
            Categoria = "Conductores",
            MatUf = 0.05m,
            FechaPrecio = DateTime.Today
        });

        var results = store.Search("cable");

        Assert.Single(results);
        Assert.Equal("CABLE-14", results[0].Codigo);
    }

    [Fact]
    public void GetByCodigo_ExistingEntry_ReturnsEntry()
    {
        var store = CreateStore();
        store.Upsert(new PriceBankEntry
        {
            Codigo = "emt-20",
            Descripcion = "Tubo EMT 20mm",
            Unidad = "tramo",
            Categoria = "Canalizaciones",
            MatUf = 0.15m,
            FechaPrecio = DateTime.Today
        });

        var entry = store.GetByCodigo("EMT-20");

        Assert.NotNull(entry);
        Assert.Equal("EMT-20", entry!.Codigo);
        Assert.Equal("Tubo EMT 20mm", entry.Descripcion);
    }

    [Fact]
    public void UpsertMany_InsertsAllEntries()
    {
        var store = CreateStore();
        int count = store.UpsertMany(new[]
        {
            new PriceBankEntry
            {
                Codigo = "THHN-14",
                Descripcion = "Cable THHN 14 AWG",
                Unidad = "m",
                Categoria = "Conductores",
                MatUf = 0.05m,
                FechaPrecio = DateTime.Today
            },
            new PriceBankEntry
            {
                Codigo = "INT-10A",
                Descripcion = "Interruptor 10A",
                Unidad = "un",
                Categoria = "Protecciones",
                MatUf = 0.5m,
                FechaPrecio = DateTime.Today
            }
        });

        Assert.Equal(2, count);
        Assert.Equal(2, store.Count());
        Assert.NotNull(store.GetByCodigo("THHN-14"));
        Assert.NotNull(store.GetByCodigo("INT-10A"));
    }
}
