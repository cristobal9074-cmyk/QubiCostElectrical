using System;
using System.IO;
using System.Linq;
using Xunit;

namespace QubiCostElectrical.Tests.Infrastructure;

// Biblioteca global de conjuntos eléctricos (banco_precios.db). Sin UseDatabasePathForTests:
// cada store usa su propia base temporal por constructor (misma estrategia que
// ApuTemplateStoreTests) para no cruzarse con los tests WPF que fijan la ruta estática.
public sealed class ConjuntoTemplateStoreTests
{
    [Fact]
    public void SaveAll_ThenGetAll_RoundTripsRowWithRecursos()
    {
        var store = StoreEnMemoria();
        var conjunto = MakeConjunto("CE-01", "Punto de enchufe");
        conjunto.Recursos.Add(new ConjuntoRecursoRow
        {
            Codigo = "CABLE25",
            Descripcion = "Cable THW 2.5 mm2",
            Unidad = "ML",
            Rendimiento = 2.5m,
            Factor = 1.5m
        });

        store.SaveAll(new[] { conjunto });

        var all = store.GetAll();
        var leido = Assert.Single(all);
        Assert.Equal(conjunto.Id, leido.Id);
        Assert.Equal("CE-01", leido.Codigo);
        Assert.Equal("Punto de enchufe", leido.Nombre);
        var recurso = Assert.Single(leido.Recursos);
        Assert.Equal("CABLE25", recurso.Codigo);
        Assert.Equal(2.5m, recurso.Rendimiento);
        Assert.Equal(1.5m, recurso.Factor);
    }

    [Fact]
    public void SaveAll_PreservaOrdenDeLaColeccion()
    {
        var store = StoreEnMemoria();
        var a = MakeConjunto("CE-A", "Alfa");
        var b = MakeConjunto("CE-B", "Beta");

        store.SaveAll(new[] { b, a });

        var all = store.GetAll();
        Assert.Equal(new[] { "CE-B", "CE-A" }, all.Select(c => c.Codigo).ToArray());
    }

    [Fact]
    public void SaveAll_ReemplazaColeccion_YBorraLasQueFaltan()
    {
        var store = StoreEnMemoria();
        var a = MakeConjunto("CE-A", "Queda");
        var b = MakeConjunto("CE-B", "Se va");
        store.SaveAll(new[] { a, b });
        Assert.Equal(2, store.GetAll().Count);

        store.SaveAll(new[] { a });

        var all = store.GetAll();
        Assert.Single(all);
        Assert.Equal("CE-A", all[0].Codigo);
    }

    [Fact]
    public void Save_MismoId_SinDuplicar_ActualizaDatos()
    {
        var store = StoreEnMemoria();
        var conjunto = MakeConjunto("CE-01", "Original");
        store.Save(conjunto);

        conjunto.Nombre = "Renombrada";
        store.Save(conjunto);

        var all = store.GetAll();
        Assert.Single(all);
        Assert.Equal("Renombrada", all[0].Nombre);
    }

    [Fact]
    public void Combinar_UnePorId_YLaBibliotecaGana()
    {
        // Misma fila (mismo Id) en biblioteca y en sesión: gana la biblioteca.
        var enBiblio = MakeConjunto("CE-1", "Nombre de la biblioteca");
        var enBiblio2 = MakeConjunto("CE-2", "Solo biblioteca");
        var enSesion = MakeConjunto("CE-1", "Nombre viejo del .qce");
        enSesion.Id = enBiblio.Id;
        var enSesion2 = MakeConjunto("CE-3", "Solo sesión");

        var resultado = ConjuntoTemplateStore.Combinar(
            new[] { enBiblio, enBiblio2 },
            new[] { enSesion, enSesion2 });

        Assert.Equal(3, resultado.Count);
        Assert.Equal("Nombre de la biblioteca", resultado.Single(c => c.Codigo == "CE-1").Nombre);
        Assert.Contains(resultado, c => c.Codigo == "CE-2");
        Assert.Contains(resultado, c => c.Codigo == "CE-3");
    }

    [Fact]
    public void Combinar_EntradasNulas_OVacias_DevuelveVacio()
    {
        Assert.Empty(ConjuntoTemplateStore.Combinar(null, null));
        Assert.Empty(ConjuntoTemplateStore.Combinar(
            Array.Empty<ConjuntoElectricoRow>(), null));
    }

    private static ConjuntoElectricoRow MakeConjunto(string codigo, string nombre) =>
        new() { Codigo = codigo, Nombre = nombre };

    private static ConjuntoTemplateStore StoreEnMemoria()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"test_conjuntos_{Guid.NewGuid():N}.db");
        return new ConjuntoTemplateStore(tempDb);
    }
}
