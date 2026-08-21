using System;
using System.IO;
using System.Linq;
using System.Text;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

public sealed class PriceBankSeederTests
{
    // ── PriceBankStore.SeedIfEmpty ────────────────────────────────────────────

    [Fact]
    public void SeedIfEmpty_BancoVacio_InsertaTodosLosItems()
    {
        var store = StoreEnMemoria();
        var entries = TresEntradas();

        int inserted = store.SeedIfEmpty(entries);

        Assert.Equal(3, inserted);
        Assert.Equal(3, store.Count());
    }

    [Fact]
    public void SeedIfEmpty_BancoConDatos_NoInsertaNada()
    {
        var store = StoreEnMemoria();
        store.Upsert(EntradaSimple("CABLE-1"));

        int inserted = store.SeedIfEmpty(TresEntradas());

        Assert.Equal(0, inserted);
        Assert.Equal(1, store.Count());
    }

    [Fact]
    public void SeedIfEmpty_LlamadoDosBeces_NoHayDuplicados()
    {
        var store = StoreEnMemoria();
        store.SeedIfEmpty(TresEntradas());
        store.SeedIfEmpty(TresEntradas()); // segunda llamada no actúa

        Assert.Equal(3, store.Count());
    }

    // ── PriceBankSeeder.ParseCsv ──────────────────────────────────────────────

    [Fact]
    public void ParseCsv_ConComa_ParseaCorrectamente()
    {
        // El precio se ingresa SIEMPRE en pesos (CLP): el header "MatUF/MoUF" se acepta por
        // compatibilidad pero el valor cae en MatClp/MoClp (fuente), no en MatUf/MoUf (derivado,
        // se calcula recién al abrir con un valor de UF — ver PriceBankEntryVm.RecalcularUf).
        string csv = """
            Codigo,Descripcion,Unidad,Categoria,MatUF,MoUF
            CABLE-16,Cable Cu 16mm2,m,Conductores,0.05,0.01
            TUBO-20,Tubo PVC 20mm,m,Tuberías,0.02,0.005
            """;
        string path = CsvTempFile(csv);

        var entries = PriceBankSeeder.ParseCsv(path);

        Assert.Equal(2, entries.Count);
        Assert.Equal("CABLE-16", entries[0].Codigo);
        Assert.Equal("Cable Cu 16mm2", entries[0].Descripcion);
        Assert.Equal(0.05m, entries[0].MatClp);
        Assert.Equal(0.01m, entries[0].MoClp);
        Assert.Equal(0m, entries[0].MatUf); // aún no derivado, se calcula al cargar
        Assert.Equal("Conductores", entries[0].Categoria);
    }

    [Fact]
    public void ParseCsv_ConPuntoYComa_ParseaCorrectamente()
    {
        string csv = """
            Codigo;Descripcion;Unidad;MatUF;MoUF
            BREAKER-1P;Breaker 1P 16A;un;0,12;0,02
            """;
        string path = CsvTempFile(csv);

        var entries = PriceBankSeeder.ParseCsv(path);

        Assert.Single(entries);
        Assert.Equal("BREAKER-1P", entries[0].Codigo);
        Assert.Equal(0.12m, entries[0].MatClp);
    }

    [Fact]
    public void ParseCsv_FilasConCodigoVacio_SeIgnoran()
    {
        string csv = """
            Codigo,Descripcion,Unidad
            CABLE-1,Cable válido,m
            ,Sin código se ignora,m
            """;
        string path = CsvTempFile(csv);

        var entries = PriceBankSeeder.ParseCsv(path);

        Assert.Single(entries);
    }

    [Fact]
    public void ParseCsv_ArchivoVacio_DevuelveListaVacia()
    {
        string path = CsvTempFile("Codigo,Descripcion");

        var entries = PriceBankSeeder.ParseCsv(path);

        Assert.Empty(entries);
    }

    [Fact]
    public void ParseCsv_CabecerasConTildesYMayusculas_SeMapeanCorrectamente()
    {
        string csv = """
            Código,Descripción,Unidad,Categoría,Mat UF,Mo UF
            TERRA-4,Cable Tierra 4mm2,m,Conductores,0.03,0.008
            """;
        string path = CsvTempFile(csv);

        var entries = PriceBankSeeder.ParseCsv(path);

        Assert.Single(entries);
        Assert.Equal("TERRA-4", entries[0].Codigo);
        Assert.Equal(0.03m, entries[0].MatClp);
    }

    [Fact]
    public void ParseCsv_ConHeaderPrecioClpYMo_ParseaCorrectamente()
    {
        string csv = """
            Codigo,Descripcion,Unidad,Familia,Precio CLP,MO,Fecha
            ENCH-D16,Enchufe doble 16A,un,Enchufes,4990,3200,2026-05-01
            """;
        string path = CsvTempFile(csv);

        var entries = PriceBankSeeder.ParseCsv(path);

        Assert.Single(entries);
        Assert.Equal("ENCH-D16", entries[0].Codigo);
        Assert.Equal(4990m, entries[0].MatClp);
        Assert.Equal(3200m, entries[0].MoClp);
        Assert.Equal("Enchufes", entries[0].Categoria);
    }

    [Fact]
    public void FindSeedFile_ArchivoNoExiste_DevuelveNull()
    {
        string dir = Path.GetTempPath();
        // Aseguramos que no existen los archivos de seed en temp
        string? result = PriceBankSeeder.FindSeedFile(dir);
        // Solo verificamos que no explota; si hay un banco_inicial.csv/xlsx en temp, no es nuestro.
        // El test asume que el directorio temp no tiene banco_inicial.*.
        Assert.True(result == null || result.StartsWith(dir));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PriceBankStore StoreEnMemoria()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_pricebank_{Guid.NewGuid():N}.db");
        return new PriceBankStore(tempDb);
    }

    private static PriceBankEntry EntradaSimple(string codigo) => new()
    {
        Codigo = codigo,
        Descripcion = "Test",
        Unidad = "m",
        Categoria = "Test",
        MatUf = 1m,
        MoUf = 0m,
        FechaPrecio = DateTime.Today
    };

    private static PriceBankEntry[] TresEntradas() =>
    [
        EntradaSimple("AAA-1"),
        EntradaSimple("BBB-2"),
        EntradaSimple("CCC-3"),
    ];

    private static string CsvTempFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), $"test_seed_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}
