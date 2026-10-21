using System.Collections.Generic;
using System.Linq;
using QubiCostElectrical;
using QubiCostElectrical.Wpf.Features.Costo;
using Xunit;

namespace QubiCostElectrical.Tests.Wpf;

public sealed class GestionFamiliasViewModelTests
{
    [Fact]
    public void CargarFamilias_ListaFamiliasEstandarYCuentaMateriales()
    {
        var session = new BudgetProjectSession();
        session.Materiales.Add(new MaterialRow { Codigo = "TRA-001", Categoria = "Transformadores" });
        session.Materiales.Add(new MaterialRow { Codigo = "TRA-002", Categoria = "Transformadores" });
        session.Materiales.Add(new MaterialRow { Codigo = "DB-001", Categoria = "Ducto barra" });

        var vm = new GestionFamiliasViewModel(session, null, () => { });

        var transf = vm.Familias.FirstOrDefault(f => f.Nombre == "Transformadores");
        Assert.NotNull(transf);
        Assert.Equal(2, transf.MaterialesCount);
        Assert.True(transf.EnUso);
        Assert.True(transf.EsEstandar);

        var ducto = vm.Familias.FirstOrDefault(f => f.Nombre == "Ducto de barra");
        Assert.NotNull(ducto);
        Assert.Equal(1, ducto.MaterialesCount);

        var gen = vm.Familias.FirstOrDefault(f => f.Nombre == "Grupo electrógeno");
        Assert.NotNull(gen);
        Assert.Equal(0, gen.MaterialesCount);
        Assert.False(gen.EnUso);
    }

    [Fact]
    public void CrearFamilia_AgregaFamiliaPersonalizadaYNotifica()
    {
        var session = new BudgetProjectSession();
        bool callbackCalled = false;
        var vm = new GestionFamiliasViewModel(session, null, () => callbackCalled = true);

        vm.NuevaFamiliaNombre = "Control y Automatización";
        vm.CrearFamiliaCommand.Execute(null);

        Assert.True(callbackCalled);
        Assert.Contains(session.ProjectInfo.FamiliasPersonalizadas, p => p == "Control y Automatización");
        Assert.Contains(vm.Familias, f => f.Nombre == "Control y Automatización" && !f.EsEstandar);
        Assert.Empty(vm.NuevaFamiliaNombre);
    }

    [Fact]
    public void GuardarEdicion_ActualizaCascadaEnMaterialesDelProyecto()
    {
        var session = new BudgetProjectSession();
        session.Materiales.Add(new MaterialRow { Codigo = "GEN-001", Categoria = "Generadores" });
        session.Materiales.Add(new MaterialRow { Codigo = "GEN-002", Categoria = "Generadores" });

        bool callbackCalled = false;
        var vm = new GestionFamiliasViewModel(session, null, () => callbackCalled = true);

        var item = vm.Familias.FirstOrDefault(f => f.Nombre == "Grupo electrógeno");
        Assert.NotNull(item);

        item.NombreEditado = "Generadores Diésel";
        vm.GuardarEdicionCommand.Execute(item);

        Assert.True(callbackCalled);
        // Ambos materiales deben tener la categoría actualizada
        Assert.All(session.Materiales, m => Assert.Equal("Generadores Diésel", m.Categoria));
        Assert.Contains(session.ProjectInfo.FamiliasPersonalizadas, p => p == "Generadores Diésel");
    }

    [Fact]
    public void EliminarFamilia_EstandarSinMateriales_LaAgregaAOcultas()
    {
        var session = new BudgetProjectSession();
        bool callbackCalled = false;
        var vm = new GestionFamiliasViewModel(session, null, () => callbackCalled = true);

        var item = vm.Familias.FirstOrDefault(f => f.Nombre == "Iluminación");
        Assert.NotNull(item);
        Assert.Equal(0, item.MaterialesCount);

        vm.EliminarFamiliaCommand.Execute(item);

        Assert.True(callbackCalled);
        Assert.Contains(session.ProjectInfo.FamiliasOcultas, o => o == "Iluminación");
        Assert.DoesNotContain(vm.Familias, f => f.Nombre == "Iluminación");
        Assert.True(vm.HayFamiliasOcultas);
    }

    [Fact]
    public void RestaurarEstandar_LimpiaFamiliasOcultas()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.FamiliasOcultas = new List<string> { "Iluminación", "Protecciones" };

        bool callbackCalled = false;
        var vm = new GestionFamiliasViewModel(session, null, () => callbackCalled = true, confirmFn: (t, m, w) => true);

        Assert.DoesNotContain(vm.Familias, f => f.Nombre == "Iluminación");
        Assert.True(vm.HayFamiliasOcultas);

        vm.RestaurarEstandarCommand.Execute(null);

        Assert.True(callbackCalled);
        Assert.Empty(session.ProjectInfo.FamiliasOcultas);
        Assert.Contains(vm.Familias, f => f.Nombre == "Iluminación");
        Assert.Contains(vm.Familias, f => f.Nombre == "Protecciones");
    }

    [Fact]
    public void EliminarFamilia_ConMateriales_PideConfirmacionYReasignaAGeneral()
    {
        var session = new BudgetProjectSession();
        session.Materiales.Add(new MaterialRow { Codigo = "AUT-001", Categoria = "Automatización" });
        session.ProjectInfo.FamiliasPersonalizadas = new List<string> { "Automatización" };

        bool confirmAsked = false;
        bool callbackCalled = false;
        var vm = new GestionFamiliasViewModel(
            session,
            null,
            () => callbackCalled = true,
            confirmFn: (t, m, w) =>
            {
                confirmAsked = true;
                return true;
            });

        var item = vm.Familias.FirstOrDefault(f => f.Nombre == "Automatización");
        Assert.NotNull(item);
        Assert.Equal(1, item.MaterialesCount);

        vm.EliminarFamiliaCommand.Execute(item);

        Assert.True(confirmAsked);
        Assert.True(callbackCalled);
        Assert.Equal("General", session.Materiales[0].Categoria);
        Assert.DoesNotContain(session.ProjectInfo.FamiliasPersonalizadas, p => p == "Automatización");
    }

    private sealed class TestPriceBankStore : IPriceBankStore
    {
        private readonly List<PriceBankEntry> items = new();
        public TestPriceBankStore(params PriceBankEntry[] initial) => items.AddRange(initial);
        public IReadOnlyList<PriceBankEntry> GetAll() => items;
        public PriceBankEntry? GetByCodigo(string codigo) => items.FirstOrDefault(i => string.Equals(i.Codigo, codigo, System.StringComparison.OrdinalIgnoreCase));
        public IReadOnlyList<PriceBankEntry> Search(string text) => items;
        public void Upsert(PriceBankEntry entry) => items.Add(entry);
        public bool Delete(string codigo) => true;
        public int DeleteAll() => 0;
        public int UpsertMany(IEnumerable<PriceBankEntry> entries) => 0;
        public int Count() => items.Count;
        public int SeedIfEmpty(IEnumerable<PriceBankEntry> entries) => 0;
    }

    [Fact]
    public void FiltroOrigen_FiltraPorCreadasYListaPrecios()
    {
        var session = new BudgetProjectSession();
        session.ProjectInfo.FamiliasPersonalizadas = new List<string> { "Domótica Especial" };

        var fakeStore = new TestPriceBankStore(
            new PriceBankEntry { Codigo = "P-1", Descripcion = "Foco", Categoria = "Iluminación Industrial", MatClp = 100m }
        );

        var vm = new GestionFamiliasViewModel(session, fakeStore, () => { });

        Assert.True(vm.TotalFamilias > 0);
        Assert.Equal(1, vm.FamiliasCreadasCount);
        Assert.Equal(1, vm.FamiliasListaPreciosCount);

        // Filtrar por creadas
        vm.FiltroOrigen = FamiliaOrigenFiltro.Creadas;
        var creadas = vm.FilteredFamilias.Cast<GestionFamiliaItemVm>().ToList();
        Assert.Single(creadas);
        Assert.Equal("Domótica Especial", creadas[0].Nombre);
        Assert.True(creadas[0].EsPersonalizada);

        // Filtrar por lista de precios
        vm.FiltroOrigen = FamiliaOrigenFiltro.ListaPrecios;
        var lista = vm.FilteredFamilias.Cast<GestionFamiliaItemVm>().ToList();
        Assert.Single(lista);
        Assert.Equal("Iluminación Industrial", lista[0].Nombre);
        Assert.True(lista[0].VieneDeListaPrecios);

        // Filtrar por estándar
        vm.FiltroOrigen = FamiliaOrigenFiltro.Estandar;
        var estandar = vm.FilteredFamilias.Cast<GestionFamiliaItemVm>().ToList();
        Assert.All(estandar, f => Assert.True(f.EsEstandar));

        // Volver a todas
        vm.FiltroOrigen = FamiliaOrigenFiltro.Todas;
        var todas = vm.FilteredFamilias.Cast<GestionFamiliaItemVm>().ToList();
        Assert.Equal(vm.TotalFamilias, todas.Count);
    }
}
