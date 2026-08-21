using System.IO;
using Microsoft.Data.Sqlite;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

/// <summary>
/// Banco corporativo: misma base SQLite en carpeta compartida (simulada con directorio local).
/// </summary>
public sealed class CorporatePriceBankTests
{
    [Fact]
    public void SharedDatabase_SecondStoreSeesWritesFromFirst()
    {
        var sharedDir = Path.Combine(Path.GetTempPath(), $"corp_bank_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sharedDir);
        var dbPath = Path.Combine(sharedDir, "banco_precios.db");

        try
        {
            var admin = new PriceBankStore(dbPath);
            admin.Upsert(new PriceBankEntry
            {
                Codigo = "THHN-14",
                Descripcion = "Cable THHN 14 AWG",
                Unidad = "m",
                Categoria = "Conductores",
                MatUf = 0.05m,
                FechaPrecio = new DateTime(2026, 6, 1)
            });

            var consultant = new PriceBankStore(dbPath);
            var entry = consultant.GetByCodigo("THHN-14");

            Assert.NotNull(entry);
            Assert.Equal(0.05m, entry!.MatUf);
            Assert.Equal(dbPath, admin.DatabasePath);
            Assert.Equal(admin.DatabasePath, consultant.DatabasePath);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
                if (Directory.Exists(sharedDir))
                    Directory.Delete(sharedDir, recursive: true);
            }
            catch
            {
                // Limpieza best-effort; el directorio temporal se elimina al reiniciar.
            }
        }
    }

    [Fact]
    public void ResolvePriceBankDatabaseFile_PreservesUncPath()
    {
        const string unc = @"\\servidor\Presupuestos\banco_precios.db";
        var resolved = ProductPaths.ResolvePriceBankDatabaseFile(unc);
        Assert.Equal(unc, resolved);
        Assert.True(ProductPaths.IsUncPath(resolved));
    }

    [Fact]
    public void SharedDatabase_SearchWorksAcrossInstances()
    {
        var sharedDir = Path.Combine(Path.GetTempPath(), $"corp_search_{Guid.NewGuid():N}");
        Directory.CreateDirectory(sharedDir);
        var dbPath = Path.Combine(sharedDir, "banco_precios.db");

        try
        {
            var writer = new PriceBankStore(dbPath);
            writer.UpsertMany(
            [
                new PriceBankEntry
                {
                    Codigo = "INT-10A",
                    Descripcion = "Interruptor 10A",
                    Unidad = "un",
                    Categoria = "Protecciones",
                    MatUf = 0.5m,
                    FechaPrecio = new DateTime(2026, 6, 1)
                },
                new PriceBankEntry
                {
                    Codigo = "INT-16A",
                    Descripcion = "Interruptor 16A",
                    Unidad = "un",
                    Categoria = "Protecciones",
                    MatUf = 0.6m,
                    FechaPrecio = new DateTime(2026, 6, 1)
                }
            ]);

            var reader = new PriceBankStore(dbPath);
            var results = reader.Search("interruptor");

            Assert.Equal(2, results.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
                if (Directory.Exists(sharedDir))
                    Directory.Delete(sharedDir, recursive: true);
            }
            catch
            {
            }
        }
    }
}
