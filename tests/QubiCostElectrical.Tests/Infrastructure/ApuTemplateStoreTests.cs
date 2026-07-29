using System;
using System.IO;
using QubiCostElectrical;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

// Regresión para la Fase 3 del plan de auditoría
// (~/.claude/plans/glittery-stargazing-hippo.md): Open() dejó de ejecutar
// CREATE TABLE/INDEX IF NOT EXISTS en cada llamada, solo la primera vez por instancia.
// Estos tests confirman que Save/GetAll/Search/Delete siguen funcionando igual a través de
// múltiples llamadas (antes y después del primer Open que crea el esquema).
public sealed class ApuTemplateStoreTests
{
    [Fact]
    public void GetAll_MultipleCallsOnFreshDatabase_AllSucceedAndReturnSavedRows()
    {
        var store = StoreEnMemoria();

        // Primera llamada crea el esquema; las siguientes deben seguir funcionando igual
        // aunque ya no repitan el DDL.
        Assert.Empty(store.GetAll());
        Assert.Empty(store.GetAll());

        store.Save(new ApuTemplateRow { Nombre = "Plantilla A", Categoria = "APU", Descripcion = "", Unidad = "GL", JsonRecursos = "[]" });

        var all = store.GetAll();
        Assert.Single(all);
        Assert.Equal("Plantilla A", all[0].Nombre);
    }

    [Fact]
    public void Search_MatchesByNombreCategoriaOrDescripcion_AcrossRepeatedCalls()
    {
        var store = StoreEnMemoria();
        store.Save(new ApuTemplateRow { Nombre = "Canalizacion PVC", Categoria = "Tuberias", Descripcion = "Instalacion estandar", Unidad = "ML", JsonRecursos = "[]" });
        store.Save(new ApuTemplateRow { Nombre = "Bandeja metalica", Categoria = "Bandejas", Descripcion = "Perforada", Unidad = "ML", JsonRecursos = "[]" });

        // Simula varias teclas del filtro: cada llamada abre y cierra su propia conexión.
        Assert.Single(store.Search("canaliz"));
        Assert.Single(store.Search("TUBERIAS"));
        Assert.Single(store.Search("perforada"));
        Assert.Equal(2, store.Search("").Count);
    }

    // Regresión de bug encontrado en esta sesión: Search() usaba SQL UPPER()/LIKE, y UPPER()
    // nativo de SQLite es ASCII-only sin la extensión ICU — buscar sin tilde no encontraba una
    // plantilla guardada CON tilde. Ahora filtra en memoria con SearchTextNormalizer.
    [Fact]
    public void Search_MatchesAccentedText_RegardlessOfAccentsInQuery()
    {
        var store = StoreEnMemoria();
        store.Save(new ApuTemplateRow { Nombre = "Canalización eléctrica", Categoria = "Tuberías", Descripcion = "Instalación estándar", Unidad = "ML", JsonRecursos = "[]" });

        Assert.Single(store.Search("tuberias"));
        Assert.Single(store.Search("TUBERÍAS"));
        Assert.Single(store.Search("canalizacion"));
        Assert.Single(store.Search("estandar"));
    }

    [Fact]
    public void Delete_ThenGetAll_RowIsGone()
    {
        var store = StoreEnMemoria();
        var row = new ApuTemplateRow { Nombre = "Temporal", Categoria = "APU", Descripcion = "", Unidad = "GL", JsonRecursos = "[]" };
        store.Save(row);
        Assert.Single(store.GetAll());

        store.Delete(row.Id);

        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void Save_SameInstanceTwice_UpsertsWithoutDuplicating()
    {
        var store = StoreEnMemoria();
        var row = new ApuTemplateRow { Nombre = "Original", Categoria = "APU", Descripcion = "", Unidad = "GL", JsonRecursos = "[]" };
        store.Save(row);

        row.Nombre = "Renombrada";
        store.Save(row);

        var all = store.GetAll();
        Assert.Single(all);
        Assert.Equal("Renombrada", all[0].Nombre);
    }

    private static ApuTemplateStore StoreEnMemoria()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_apu_templates_{Guid.NewGuid():N}.db");
        return new ApuTemplateStore(tempDb);
    }
}
